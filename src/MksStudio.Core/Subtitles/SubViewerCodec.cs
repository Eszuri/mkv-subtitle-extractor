using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Subtitles;

/// <summary>
/// Parser and Serializer for SubViewer (.sub) format (v1.0 & v2.0).
/// Characterized by [INFORMATION] block and timecodes: hh:mm:ss.ff,hh:mm:ss.ff.
/// </summary>
public static class SubViewerCodec
{
    private static readonly Regex TimecodeRegex = new(
        @"^(\d{1,2}):(\d{2}):(\d{2})[.,](\d{2,3}),(\d{1,2}):(\d{2}):(\d{2})[.,](\d{2,3})$",
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
            if (string.IsNullOrEmpty(line) || line.StartsWith('[') && !TimecodeRegex.IsMatch(line))
            {
                i++;
                continue;
            }

            var match = TimecodeRegex.Match(line);
            if (match.Success)
            {
                var startTime = ParseTime(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value);
                var endTime = ParseTime(match.Groups[5].Value, match.Groups[6].Value, match.Groups[7].Value, match.Groups[8].Value);

                i++;
                var textBuilder = new StringBuilder();
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                {
                    if (TimecodeRegex.IsMatch(lines[i].Trim()))
                        break;

                    if (textBuilder.Length > 0)
                        textBuilder.Append('\n');
                    textBuilder.Append(lines[i].TrimEnd());
                    i++;
                }

                string raw = textBuilder.ToString().Replace("[br]", "\n", StringComparison.OrdinalIgnoreCase);

                doc.Cues.Add(new SubtitleCue
                {
                    Index = cueIndex++,
                    StartTime = startTime,
                    EndTime = endTime,
                    RawText = raw
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
        sb.AppendLine("[INFORMATION]");
        sb.AppendLine("[TITLE]MKS Subtitle Studio");
        sb.AppendLine("[AUTHOR]MksStudio");
        sb.AppendLine("[DELAY]0");
        sb.AppendLine("[CD TRACK]0");
        sb.AppendLine("[COMMENT]");
        sb.AppendLine("[END INFORMATION]");
        sb.AppendLine("[SUBTITLE]");
        sb.AppendLine();

        foreach (var cue in document.Cues.OrderBy(c => c.StartTime))
        {
            sb.AppendLine($"{FormatTime(cue.StartTime)},{FormatTime(cue.EndTime)}");

            string text = cue.RawText
                .Replace("\r\n", "[br]")
                .Replace("\n", "[br]")
                .Replace("\\N", "[br]")
                .Replace("\\n", "[br]");

            sb.AppendLine(text);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static TimeSpan ParseTime(string h, string m, string s, string frac)
    {
        int hour = int.Parse(h, CultureInfo.InvariantCulture);
        int min = int.Parse(m, CultureInfo.InvariantCulture);
        int sec = int.Parse(s, CultureInfo.InvariantCulture);
        int ms = frac.Length == 2
            ? int.Parse(frac, CultureInfo.InvariantCulture) * 10
            : int.Parse(frac[..3], CultureInfo.InvariantCulture);
        return new TimeSpan(0, hour, min, sec, ms);
    }

    private static string FormatTime(TimeSpan time)
    {
        int h = (int)time.TotalHours;
        int m = time.Minutes;
        int s = time.Seconds;
        int cs = time.Milliseconds / 10;
        return $"{h:D2}:{m:D2}:{s:D2}.{cs:D2}";
    }
}
