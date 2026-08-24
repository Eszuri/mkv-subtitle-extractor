using System.Globalization;
using System.Text;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Subtitles;

/// <summary>
/// Parser and Serializer for Advanced SubStation Alpha (.ass / S_TEXT/ASS) format.
/// </summary>
public static class AssCodec
{
    public static SubtitleDocument Parse(string content)
    {
        var doc = new SubtitleDocument();
        if (string.IsNullOrWhiteSpace(content))
            return doc;

        doc.Styles.Clear();
        string currentSection = string.Empty;
        string[] lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(';'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line.Trim('[', ']').Trim();
                continue;
            }

            if (currentSection.Equals("Script Info", StringComparison.OrdinalIgnoreCase))
            {
                int colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                {
                    string key = line.Substring(0, colonIdx).Trim();
                    string val = line.Substring(colonIdx + 1).Trim();
                    doc.ScriptInfo[key] = val;
                }
            }
            else if (currentSection.Equals("V4+ Styles", StringComparison.OrdinalIgnoreCase) ||
                     currentSection.Equals("V4 Styles", StringComparison.OrdinalIgnoreCase))
            {
                if (line.StartsWith("Style:", StringComparison.OrdinalIgnoreCase))
                {
                    doc.Styles.Add(AssStyle.Parse(line));
                }
            }
            else if (currentSection.Equals("Events", StringComparison.OrdinalIgnoreCase))
            {
                if (line.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
                {
                    var cue = ParseDialogueLine(line);
                    if (cue != null)
                        doc.Cues.Add(cue);
                }
            }
        }

        if (doc.Styles.Count == 0)
            doc.Styles.Add(new AssStyle());

        doc.Reindex();
        return doc;
    }

    public static SubtitleCue? ParseDialogueLine(string line)
    {
        // Dialogue: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
        string content = line;
        if (content.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
            content = content.Substring(9).Trim();

        string[] parts = SplitCsv(content, 10);
        if (parts.Length < 10)
            return null;

        var cue = new SubtitleCue();
        if (int.TryParse(parts[0].Trim(), out var layer)) cue.Layer = layer;
        cue.StartTime = ParseAssTimestamp(parts[1].Trim());
        cue.EndTime = ParseAssTimestamp(parts[2].Trim());
        cue.Style = parts[3].Trim();
        cue.Actor = parts[4].Trim();
        if (int.TryParse(parts[5].Trim(), out var ml)) cue.MarginL = ml;
        if (int.TryParse(parts[6].Trim(), out var mr)) cue.MarginR = mr;
        if (int.TryParse(parts[7].Trim(), out var mv)) cue.MarginV = mv;
        cue.Effect = parts[8].Trim();
        cue.RawText = parts[9];

        return cue;
    }

    /// <summary>
    /// Parses a Matroska ASS Block payload (format: ReadOrder,Layer,Style,Name,MarginL,MarginR,MarginV,Effect,Text).
    /// </summary>
    public static SubtitleCue ParseMatroskaBlock(string payload, TimeSpan startTime, TimeSpan duration)
    {
        string[] parts = SplitCsv(payload, 9);
        var cue = new SubtitleCue
        {
            StartTime = startTime,
            EndTime = startTime + duration
        };

        if (parts.Length >= 9)
        {
            // parts[0] is ReadOrder
            if (int.TryParse(parts[1].Trim(), out var layer)) cue.Layer = layer;
            cue.Style = parts[2].Trim();
            cue.Actor = parts[3].Trim();
            if (int.TryParse(parts[4].Trim(), out var ml)) cue.MarginL = ml;
            if (int.TryParse(parts[5].Trim(), out var mr)) cue.MarginR = mr;
            if (int.TryParse(parts[6].Trim(), out var mv)) cue.MarginV = mv;
            cue.Effect = parts[7].Trim();
            cue.RawText = parts[8];
        }
        else
        {
            cue.RawText = payload;
        }

        return cue;
    }

    /// <summary>
    /// Formats a SubtitleCue into Matroska ASS Block payload.
    /// </summary>
    public static string FormatMatroskaBlock(SubtitleCue cue, int readOrder)
    {
        // ReadOrder,Layer,Style,Name,MarginL,MarginR,MarginV,Effect,Text
        return $"{readOrder},{cue.Layer},{cue.Style},{cue.Actor},{cue.MarginL:D4},{cue.MarginR:D4},{cue.MarginV:D4},{cue.Effect},{cue.RawText}";
    }

    public static string Serialize(SubtitleDocument document)
    {
        var sb = new StringBuilder();

        // [Script Info]
        sb.AppendLine("[Script Info]");
        foreach (var kv in document.ScriptInfo)
        {
            sb.AppendLine($"{kv.Key}: {kv.Value}");
        }
        sb.AppendLine();

        // [V4+ Styles]
        sb.AppendLine("[V4+ Styles]");
        sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
        foreach (var style in document.Styles)
        {
            sb.AppendLine(style.ToAssString());
        }
        sb.AppendLine();

        // [Events]
        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");
        foreach (var cue in document.Cues.OrderBy(c => c.StartTime))
        {
            sb.AppendLine($"Dialogue: {cue.Layer},{FormatAssTimestamp(cue.StartTime)},{FormatAssTimestamp(cue.EndTime)},{cue.Style},{cue.Actor},{cue.MarginL:D4},{cue.MarginR:D4},{cue.MarginV:D4},{cue.Effect},{cue.RawText}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates the CodecPrivate string for S_TEXT/ASS in Matroska (Header + Styles).
    /// </summary>
    public static string GenerateCodecPrivate(SubtitleDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Script Info]");
        foreach (var kv in document.ScriptInfo)
        {
            sb.AppendLine($"{kv.Key}: {kv.Value}");
        }
        sb.AppendLine();

        sb.AppendLine("[V4+ Styles]");
        sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
        foreach (var style in document.Styles)
        {
            sb.AppendLine(style.ToAssString());
        }
        sb.AppendLine();

        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        return sb.ToString();
    }

    public static string FormatAssTimestamp(TimeSpan time)
    {
        int hours = (int)time.TotalHours;
        int minutes = time.Minutes;
        int seconds = time.Seconds;
        int centiseconds = time.Milliseconds / 10;
        return $"{hours}:{minutes:D2}:{seconds:D2}.{centiseconds:D2}";
    }

    public static TimeSpan ParseAssTimestamp(string timestamp)
    {
        // Format: H:MM:SS.cc or HH:MM:SS.mmm
        string[] parts = timestamp.Split(':');
        if (parts.Length != 3) return TimeSpan.Zero;

        if (!int.TryParse(parts[0], CultureInfo.InvariantCulture, out int h)) return TimeSpan.Zero;
        if (!int.TryParse(parts[1], CultureInfo.InvariantCulture, out int m)) return TimeSpan.Zero;

        string[] secParts = parts[2].Split('.');
        if (!int.TryParse(secParts[0], CultureInfo.InvariantCulture, out int s)) return TimeSpan.Zero;

        int ms = 0;
        if (secParts.Length > 1)
        {
            string msStr = secParts[1].PadRight(3, '0');
            if (msStr.Length > 3) msStr = msStr.Substring(0, 3);
            int.TryParse(msStr, CultureInfo.InvariantCulture, out ms);
        }

        return new TimeSpan(0, h, m, s, ms);
    }

    private static string[] SplitCsv(string line, int maxParts)
    {
        var result = new List<string>(maxParts);
        int currentPos = 0;

        for (int i = 0; i < maxParts - 1; i++)
        {
            int commaPos = line.IndexOf(',', currentPos);
            if (commaPos < 0)
                break;

            result.Add(line.Substring(currentPos, commaPos - currentPos));
            currentPos = commaPos + 1;
        }

        if (currentPos <= line.Length)
        {
            result.Add(line.Substring(currentPos));
        }

        return result.ToArray();
    }
}
