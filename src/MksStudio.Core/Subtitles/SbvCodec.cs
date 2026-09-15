using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Subtitles;

/// <summary>
/// Parser and Serializer for YouTube SubViewer format (.sbv).
/// Standard text format used by YouTube Studio.
/// </summary>
public static class SbvCodec
{
    private static readonly Regex TimecodeRegex = new(
        @"^(\d{1,2}):(\d{2}):(\d{2})\.(\d{3}),(\d{1,2}):(\d{2}):(\d{2})\.(\d{3})$",
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

            var match = TimecodeRegex.Match(line);
            if (match.Success)
            {
                int sh = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                int sm = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                int ss = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                int sms = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
                var startTime = new TimeSpan(0, sh, sm, ss, sms);

                int eh = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
                int em = int.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture);
                int es = int.Parse(match.Groups[7].Value, CultureInfo.InvariantCulture);
                int ems = int.Parse(match.Groups[8].Value, CultureInfo.InvariantCulture);
                var endTime = new TimeSpan(0, eh, em, es, ems);

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

        foreach (var cue in document.Cues.OrderBy(c => c.StartTime))
        {
            sb.AppendLine($"{FormatSbvTimestamp(cue.StartTime)},{FormatSbvTimestamp(cue.EndTime)}");

            string text = cue.RawText
                .Replace("\r\n", "\n")
                .Replace("\\N", "\n")
                .Replace("\\n", "\n");

            sb.AppendLine(text);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatSbvTimestamp(TimeSpan time)
    {
        int hours = (int)time.TotalHours;
        int minutes = time.Minutes;
        int seconds = time.Seconds;
        int milliseconds = time.Milliseconds;
        return $"{hours}:{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
    }
}
