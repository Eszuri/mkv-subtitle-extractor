using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Subtitles;

/// <summary>
/// Parser and Serializer for SubRip (.srt / S_TEXT/UTF8) subtitle format.
/// </summary>
public static class SrtCodec
{
    private static readonly Regex TimecodeRegex = new(
        @"(\d{1,2}):(\d{2}):(\d{2})[,.](\d{3})\s*-->\s*(\d{1,2}):(\d{2}):(\d{2})[,.](\d{3})",
        RegexOptions.Compiled);

    public static SubtitleDocument Parse(string content)
    {
        var doc = new SubtitleDocument();
        if (string.IsNullOrWhiteSpace(content))
            return doc;

        string[] lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int i = 0;
        int cueIndex = 1;

        while (i < lines.Length)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                i++;
                continue;
            }

            // Check if this line is an index number
            if (int.TryParse(line, out _))
            {
                i++;
                if (i >= lines.Length) break;
                line = lines[i].Trim();
            }

            // Check timecode line
            var match = TimecodeRegex.Match(line);
            if (match.Success)
            {
                var startTime = ParseTimestamp(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value);
                var endTime = ParseTimestamp(match.Groups[5].Value, match.Groups[6].Value, match.Groups[7].Value, match.Groups[8].Value);

                i++;
                var textBuilder = new StringBuilder();
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                {
                    // Check if we hit the next cue's timecode directly (in malformed srt)
                    if (TimecodeRegex.IsMatch(lines[i]))
                    {
                        // Backtrack so outer loop can parse it
                        break;
                    }
                    if (textBuilder.Length > 0)
                        textBuilder.Append('\n');
                    textBuilder.Append(lines[i].TrimEnd());
                    i++;
                }

                doc.Cues.Add(new SubtitleCue
                {
                    Index = cueIndex++,
                    StartTime = startTime,
                    EndTime = endTime,
                    RawText = textBuilder.ToString()
                });
            }
            else
            {
                i++;
            }
        }

        doc.Reindex();
        return doc;
    }

    public static string Serialize(SubtitleDocument document)
    {
        var sb = new StringBuilder();
        int index = 1;

        foreach (var cue in document.Cues.OrderBy(c => c.StartTime))
        {
            sb.AppendLine(index.ToString(CultureInfo.InvariantCulture));
            sb.Append(FormatTimestamp(cue.StartTime));
            sb.Append(" --> ");
            sb.AppendLine(FormatTimestamp(cue.EndTime));

            // Format text: convert \N to newlines and strip ASS-specific tag brackets for pure SRT
            string text = cue.RawText.Replace("\\N", "\n").Replace("\\n", "\n").Replace("\\h", " ");
            sb.AppendLine(text);
            sb.AppendLine();
            index++;
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatTimestamp(TimeSpan time)
    {
        int hours = (int)time.TotalHours;
        int minutes = time.Minutes;
        int seconds = time.Seconds;
        int milliseconds = time.Milliseconds;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2},{milliseconds:D3}";
    }

    private static TimeSpan ParseTimestamp(string h, string m, string s, string ms)
    {
        int hours = int.Parse(h, CultureInfo.InvariantCulture);
        int minutes = int.Parse(m, CultureInfo.InvariantCulture);
        int seconds = int.Parse(s, CultureInfo.InvariantCulture);
        int milliseconds = int.Parse(ms, CultureInfo.InvariantCulture);
        return new TimeSpan(0, hours, minutes, seconds, milliseconds);
    }
}
