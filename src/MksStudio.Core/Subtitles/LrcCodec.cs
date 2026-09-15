using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Subtitles;

/// <summary>
/// Parser and Serializer for LRC (Timed Lyrics / Synchronized Lyrics, .lrc).
/// Standard format for music players, karaoke, and audio-video subtitle syncing.
/// </summary>
public static class LrcCodec
{
    private static readonly Regex TimestampRegex = new(
        @"\[(\d{1,2}):(\d{2})(?:[.:](\d{2,3}))?\]",
        RegexOptions.Compiled);

    public static SubtitleDocument Parse(string content)
    {
        var doc = new SubtitleDocument();
        if (string.IsNullOrWhiteSpace(content))
            return doc;

        string[] lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var parsedItems = new List<(TimeSpan Time, string Text)>();

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var matches = TimestampRegex.Matches(line);
            if (matches.Count == 0)
                continue; // Metadata tags like [ti:...], [ar:...]

            // Remove all timestamps from the line to extract lyric text
            string text = TimestampRegex.Replace(line, string.Empty).Trim();

            foreach (Match m in matches)
            {
                int min = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                int sec = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                int ms = 0;

                if (m.Groups[3].Success)
                {
                    string fractionStr = m.Groups[3].Value;
                    if (fractionStr.Length == 2)
                    {
                        // Hundredths / Centiseconds (00-99 -> 0-990ms)
                        ms = int.Parse(fractionStr, CultureInfo.InvariantCulture) * 10;
                    }
                    else if (fractionStr.Length >= 3)
                    {
                        ms = int.Parse(fractionStr[..3], CultureInfo.InvariantCulture);
                    }
                }

                var time = new TimeSpan(0, 0, min, sec, ms);
                if (!string.IsNullOrEmpty(text))
                {
                    parsedItems.Add((time, text));
                }
            }
        }

        // Sort by time
        var sorted = parsedItems.OrderBy(p => p.Time).ToList();
        int cueIndex = 1;

        for (int i = 0; i < sorted.Count; i++)
        {
            var current = sorted[i];
            TimeSpan endTime;

            if (i + 1 < sorted.Count)
            {
                var next = sorted[i + 1];
                var diff = next.Time - current.Time;
                if (diff > TimeSpan.Zero && diff <= TimeSpan.FromSeconds(8))
                {
                    endTime = next.Time;
                }
                else
                {
                    endTime = current.Time + TimeSpan.FromSeconds(3.5);
                }
            }
            else
            {
                endTime = current.Time + TimeSpan.FromSeconds(3.5);
            }

            doc.Cues.Add(new SubtitleCue
            {
                Index = cueIndex++,
                StartTime = current.Time,
                EndTime = endTime,
                RawText = current.Text
            });
        }

        doc.Reindex();
        return doc;
    }

    public static string Serialize(SubtitleDocument document)
    {
        var sb = new StringBuilder();

        // Optional metadata header
        if (document.ScriptInfo.TryGetValue("Title", out var title) && !string.IsNullOrWhiteSpace(title))
        {
            sb.AppendLine($"[ti:{title}]");
        }

        foreach (var cue in document.Cues.OrderBy(c => c.StartTime))
        {
            int min = (int)cue.StartTime.TotalMinutes;
            int sec = cue.StartTime.Seconds;
            int cs = cue.StartTime.Milliseconds / 10;

            string text = cue.PlainText.Replace("\r\n", " ").Replace('\n', ' ');
            sb.AppendLine($"[{min:D2}:{sec:D2}.{cs:D2}]{text}");
        }

        return sb.ToString().TrimEnd();
    }
}
