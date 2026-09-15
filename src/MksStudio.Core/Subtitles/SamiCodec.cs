using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Subtitles;

/// <summary>
/// Parser and Serializer for SAMI (Synchronized Accessible Media Interchange, .smi, .sami).
/// Popular in Windows Media Player and Asian / Korean drama subtitle releases.
/// </summary>
public static class SamiCodec
{
    private static readonly Regex SyncRegex = new(
        @"<SYNC\s+Start\s*=\s*[""']?(\d+)[""']?[^>]*>(.*?)(?=<SYNC|\Z|</BODY>)",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    public static SubtitleDocument Parse(string content)
    {
        var doc = new SubtitleDocument();
        if (string.IsNullOrWhiteSpace(content))
            return doc;

        var matches = SyncRegex.Matches(content);
        if (matches.Count == 0)
            return doc;

        var rawCues = new List<(TimeSpan Start, string Text)>();

        foreach (Match match in matches)
        {
            if (!long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long startMs))
                continue;

            string block = match.Groups[2].Value;

            // Remove <P ...> opening tags
            block = Regex.Replace(block, @"<P\b[^>]*>", string.Empty, RegexOptions.IgnoreCase);
            // Replace <br> with newlines
            block = Regex.Replace(block, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            // Strip remaining HTML tags
            block = Regex.Replace(block, @"<[^>]+>", string.Empty);
            // Clean HTML entities (&nbsp; => non-breaking space or blank)
            string cleaned = System.Net.WebUtility.HtmlDecode(block).Trim();

            // In SAMI, an empty text or &nbsp; serves as the terminator/clear screen for the previous subtitle
            if (string.Equals(cleaned, "\u00A0", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(cleaned))
            {
                rawCues.Add((TimeSpan.FromMilliseconds(startMs), string.Empty));
            }
            else
            {
                rawCues.Add((TimeSpan.FromMilliseconds(startMs), cleaned));
            }
        }

        int cueIndex = 1;
        for (int i = 0; i < rawCues.Count; i++)
        {
            var current = rawCues[i];
            if (string.IsNullOrEmpty(current.Text))
                continue; // Blank cue used as end delimiter

            // Determine EndTime: look for the next cue's timestamp
            TimeSpan endTime;
            if (i + 1 < rawCues.Count)
            {
                var next = rawCues[i + 1];
                var gap = next.Start - current.Start;
                if (gap > TimeSpan.Zero && gap <= TimeSpan.FromSeconds(10))
                {
                    endTime = next.Start;
                }
                else
                {
                    endTime = current.Start + TimeSpan.FromSeconds(3.5);
                }
            }
            else
            {
                endTime = current.Start + TimeSpan.FromSeconds(3.5);
            }

            doc.Cues.Add(new SubtitleCue
            {
                Index = cueIndex++,
                StartTime = current.Start,
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
        sb.AppendLine("<SAMI>");
        sb.AppendLine("<HEAD>");
        sb.AppendLine("<TITLE>MKS Studio SAMI Subtitles</TITLE>");
        sb.AppendLine("<STYLE TYPE=\"text/css\">");
        sb.AppendLine("<!--");
        sb.AppendLine("P { font-family: Arial; font-weight: normal; color: white; background-color: black; text-align: center; }");
        sb.AppendLine(".ENUSCC { Name: English; lang: en-US; SAMIType: CC; }");
        sb.AppendLine("-->");
        sb.AppendLine("</STYLE>");
        sb.AppendLine("</HEAD>");
        sb.AppendLine("<BODY>");

        foreach (var cue in document.Cues.OrderBy(c => c.StartTime))
        {
            long startMs = (long)cue.StartTime.TotalMilliseconds;
            long endMs = (long)cue.EndTime.TotalMilliseconds;

            string textHtml = cue.RawText
                .Replace("\r\n", "<br>")
                .Replace("\n", "<br>")
                .Replace("\\N", "<br>")
                .Replace("\\n", "<br>");

            sb.AppendLine($"<SYNC Start={startMs}><P Class=ENUSCC>{textHtml}</P></SYNC>");
            sb.AppendLine($"<SYNC Start={endMs}><P Class=ENUSCC>&nbsp;</P></SYNC>");
        }

        sb.AppendLine("</BODY>");
        sb.AppendLine("</SAMI>");

        return sb.ToString();
    }
}
