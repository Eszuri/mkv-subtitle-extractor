using System.IO;
using System.Text.RegularExpressions;
using MksStudio.Core.Ebml;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Subtitles;

/// <summary>
/// Central registry and router for detecting, parsing, and serializing all supported subtitle formats.
/// Supported formats: SRT, ASS, SSA, WebVTT, TTML, DFXP, SAMI, MicroDVD, YouTube SBV, Timed Lyrics (LRC), SubViewer.
/// </summary>
public static class SubtitleFormatRouter
{
    public static readonly string[] SupportedExtensions =
    [
        ".srt", ".ass", ".ssa", ".vtt",
        ".ttml", ".dfxp", ".xml",
        ".smi", ".sami",
        ".sub",
        ".sbv",
        ".lrc"
    ];

    public static bool IsSupportedExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return false;
        string ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
        return SupportedExtensions.Contains(ext);
    }

    /// <summary>
    /// Parses subtitle text from any format, using file extension hint if available or content sniffing.
    /// </summary>
    public static SubtitleDocument Parse(string content, string? extensionOrPath = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new SubtitleDocument();

        string ext = string.Empty;
        if (!string.IsNullOrEmpty(extensionOrPath))
        {
            ext = Path.GetExtension(extensionOrPath).ToLowerInvariant();
        }

        switch (ext)
        {
            case ".ass":
            case ".ssa":
                return AssCodec.Parse(content);

            case ".vtt":
                return VttCodec.Parse(content);

            case ".ttml":
            case ".dfxp":
                return TtmlCodec.Parse(content);

            case ".smi":
            case ".sami":
                return SamiCodec.Parse(content);

            case ".sbv":
                return SbvCodec.Parse(content);

            case ".lrc":
                return LrcCodec.Parse(content);

            case ".sub":
                // Check if MicroDVD frame syntax {0}{100} or SubViewer hh:mm:ss
                if (Regex.IsMatch(content, @"\{\d+\}\{\d+\}"))
                {
                    return MicroDvdCodec.Parse(content);
                }
                return SubViewerCodec.Parse(content);

            case ".xml":
                // If XML contains TTML namespace or <p begin=
                if (content.Contains("<tt", StringComparison.OrdinalIgnoreCase) || content.Contains("http://www.w3.org/ns/ttml", StringComparison.OrdinalIgnoreCase))
                {
                    return TtmlCodec.Parse(content);
                }
                break;

            case ".srt":
                return SrtCodec.Parse(content);
        }

        // Auto-detect by content sniffing if extension is ambiguous or missing
        return AutoDetectAndParse(content);
    }

    /// <summary>
    /// Auto-detects subtitle format from raw text signatures and parses it.
    /// </summary>
    public static SubtitleDocument AutoDetectAndParse(string content)
    {
        if (content.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
        {
            return VttCodec.Parse(content);
        }

        if (content.Contains("[Script Info]", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("[V4+ Styles]", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Format: Layer, Start, End", StringComparison.OrdinalIgnoreCase))
        {
            return AssCodec.Parse(content);
        }

        if (content.Contains("<SAMI>", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("<SYNC Start=", StringComparison.OrdinalIgnoreCase))
        {
            return SamiCodec.Parse(content);
        }

        if (content.Contains("<tt", StringComparison.OrdinalIgnoreCase) &&
            (content.Contains("ttp:timeBase", StringComparison.OrdinalIgnoreCase) || content.Contains("<p begin=", StringComparison.OrdinalIgnoreCase)))
        {
            return TtmlCodec.Parse(content);
        }

        if (Regex.IsMatch(content, @"\{\d+\}\{\d+\}"))
        {
            return MicroDvdCodec.Parse(content);
        }

        if (Regex.IsMatch(content, @"\[\d{1,2}:\d{2}(?:[.:]\d{2,3})?\]"))
        {
            return LrcCodec.Parse(content);
        }

        if (Regex.IsMatch(content, @"^\d{1,2}:\d{2}:\d{2}\.\d{3},\d{1,2}:\d{2}:\d{2}\.\d{3}", RegexOptions.Multiline))
        {
            return SbvCodec.Parse(content);
        }

        if (content.Contains("[INFORMATION]", StringComparison.OrdinalIgnoreCase) &&
            content.Contains("[SUBTITLE]", StringComparison.OrdinalIgnoreCase))
        {
            return SubViewerCodec.Parse(content);
        }

        // Default fallback to SubRip
        return SrtCodec.Parse(content);
    }

    /// <summary>
    /// Serializes subtitle document into the requested target format.
    /// </summary>
    public static string Serialize(SubtitleDocument doc, string formatOrExtension)
    {
        string ext = formatOrExtension.StartsWith('.') ? formatOrExtension.ToLowerInvariant() : $".{formatOrExtension.ToLowerInvariant()}";

        return ext switch
        {
            ".ass" or ".ssa" => AssCodec.Serialize(SubtitleConverter.ConvertToAss(doc)),
            ".vtt" => VttCodec.Serialize(SubtitleConverter.ConvertToVtt(doc)),
            ".ttml" or ".dfxp" or ".xml" => TtmlCodec.Serialize(doc),
            ".smi" or ".sami" => SamiCodec.Serialize(doc),
            ".sub" => MicroDvdCodec.Serialize(doc),
            ".sbv" => SbvCodec.Serialize(doc),
            ".lrc" => LrcCodec.Serialize(doc),
            _ => SrtCodec.Serialize(SubtitleConverter.ConvertToSrt(doc))
        };
    }

    /// <summary>
    /// Returns default Matroska CodecId and descriptive CodecName for an imported subtitle format.
    /// </summary>
    public static (string CodecId, string CodecName) GetMatroskaCodecInfo(string extension)
    {
        string ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";

        return ext switch
        {
            ".ass" => (EbmlConstants.CodecAss, "Advanced SubStation Alpha"),
            ".ssa" => (EbmlConstants.CodecSsa, "SubStation Alpha"),
            ".vtt" => (EbmlConstants.CodecVtt, "WebVTT"),
            ".ttml" or ".dfxp" or ".xml" => (EbmlConstants.CodecSrt, "Timed Text (TTML)"),
            ".smi" or ".sami" => (EbmlConstants.CodecSrt, "SAMI Subtitles"),
            ".sub" => (EbmlConstants.CodecSrt, "MicroDVD Subtitles"),
            ".sbv" => (EbmlConstants.CodecSrt, "YouTube SBV"),
            ".lrc" => (EbmlConstants.CodecSrt, "Timed Lyrics (LRC)"),
            _ => (EbmlConstants.CodecSrt, "SubRip")
        };
    }
}
