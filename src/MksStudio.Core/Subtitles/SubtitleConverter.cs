using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Subtitles;

/// <summary>
/// Converts subtitle documents between different formats (ASS, SRT, VTT).
/// </summary>
public static class SubtitleConverter
{
    /// <summary>
    /// Converts any subtitle document to pure SRT.
    /// </summary>
    public static SubtitleDocument ConvertToSrt(SubtitleDocument source) => ConvertToPlainTextFormat(source);

    /// <summary>
    /// Converts any subtitle document to ASS format with standard styles.
    /// </summary>
    public static SubtitleDocument ConvertToAss(SubtitleDocument source)
    {
        var doc = new SubtitleDocument();
        foreach (var kv in source.ScriptInfo)
        {
            doc.ScriptInfo[kv.Key] = kv.Value;
        }

        if (source.Styles.Count > 0)
        {
            doc.Styles.Clear();
            foreach (var style in source.Styles)
            {
                doc.Styles.Add(new AssStyle
                {
                    Name = style.Name,
                    Fontname = style.Fontname,
                    Fontsize = style.Fontsize,
                    PrimaryColour = style.PrimaryColour,
                    SecondaryColour = style.SecondaryColour,
                    OutlineColour = style.OutlineColour,
                    BackColour = style.BackColour,
                    Bold = style.Bold,
                    Italic = style.Italic,
                    Underline = style.Underline,
                    StrikeOut = style.StrikeOut,
                    ScaleX = style.ScaleX,
                    ScaleY = style.ScaleY,
                    Spacing = style.Spacing,
                    Angle = style.Angle,
                    BorderStyle = style.BorderStyle,
                    Outline = style.Outline,
                    Shadow = style.Shadow,
                    Alignment = style.Alignment,
                    MarginL = style.MarginL,
                    MarginR = style.MarginR,
                    MarginV = style.MarginV,
                    Encoding = style.Encoding
                });
            }
        }

        foreach (var cue in source.Cues)
        {
            var newCue = cue.Clone();
            // Convert standard HTML tags to ASS if present
            newCue.RawText = ConvertHtmlToAssTags(newCue.RawText);
            doc.Cues.Add(newCue);
        }

        doc.Reindex();
        return doc;
    }

    /// <summary>
    /// Converts any subtitle document to WebVTT.
    /// </summary>
    public static SubtitleDocument ConvertToVtt(SubtitleDocument source) => ConvertToPlainTextFormat(source);

    /// <summary>
    /// Converts any subtitle document to plain text cues suitable for TTML, SAMI, SBV, MicroDVD, or LRC.
    /// </summary>
    public static SubtitleDocument ConvertToPlainTextFormat(SubtitleDocument source)
    {
        var doc = new SubtitleDocument();
        foreach (var cue in source.Cues)
        {
            var newCue = cue.Clone();
            newCue.RawText = cue.PlainText;
            doc.Cues.Add(newCue);
        }
        doc.Reindex();
        return doc;
    }

    /// <summary>
    /// Converts subtitle document to the specified target format.
    /// </summary>
    public static SubtitleDocument ConvertToFormat(SubtitleDocument source, string formatOrExtension)
    {
        string ext = formatOrExtension.StartsWith('.') ? formatOrExtension.ToLowerInvariant() : $".{formatOrExtension.ToLowerInvariant()}";
        return ext switch
        {
            ".ass" or ".ssa" => ConvertToAss(source),
            ".vtt" => ConvertToVtt(source),
            _ => ConvertToPlainTextFormat(source)
        };
    }

    private static string ConvertHtmlToAssTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        return text
            .Replace("<b>", "{\\b1}")
            .Replace("</b>", "{\\b0}")
            .Replace("<i>", "{\\i1}")
            .Replace("</i>", "{\\i0}")
            .Replace("<u>", "{\\u1}")
            .Replace("</u>", "{\\u0}")
            .Replace("\r\n", "\\N")
            .Replace("\n", "\\N");
    }
}
