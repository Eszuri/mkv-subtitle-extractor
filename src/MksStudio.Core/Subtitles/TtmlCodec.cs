using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Subtitles;

/// <summary>
/// Parser and Serializer for W3C Timed Text Markup Language (TTML / DFXP / XML).
/// Widely used by streaming platforms (Netflix, YouTube, broadcast).
/// </summary>
public static class TtmlCodec
{
    private static readonly Regex TimecodeColonRegex = new(
        @"^(?:(\d{1,2}):)?(\d{1,2}):(\d{2})(?:[.,](\d{1,3}))?$",
        RegexOptions.Compiled);

    public static SubtitleDocument Parse(string content)
    {
        var doc = new SubtitleDocument();
        if (string.IsNullOrWhiteSpace(content))
            return doc;

        try
        {
            // Normalize XML content
            var xdoc = XDocument.Parse(content);
            int cueIndex = 1;

            var pElements = xdoc.Descendants().Where(e => e.Name.LocalName.Equals("p", StringComparison.OrdinalIgnoreCase));

            foreach (var p in pElements)
            {
                string? beginAttr = p.Attribute("begin")?.Value ?? p.Attribute("start")?.Value;
                string? endAttr = p.Attribute("end")?.Value;
                string? durAttr = p.Attribute("dur")?.Value ?? p.Attribute("duration")?.Value;

                if (string.IsNullOrWhiteSpace(beginAttr))
                    continue;

                var startTime = ParseTtmlTime(beginAttr);
                TimeSpan endTime;

                if (!string.IsNullOrWhiteSpace(endAttr))
                {
                    endTime = ParseTtmlTime(endAttr);
                }
                else if (!string.IsNullOrWhiteSpace(durAttr))
                {
                    endTime = startTime + ParseTtmlTime(durAttr);
                }
                else
                {
                    // Default fallback duration 3 seconds
                    endTime = startTime + TimeSpan.FromSeconds(3);
                }

                // Extract text and preserve line breaks
                string rawText = ExtractText(p);

                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    doc.Cues.Add(new SubtitleCue
                    {
                        Index = cueIndex++,
                        StartTime = startTime,
                        EndTime = endTime,
                        RawText = rawText.Trim()
                    });
                }
            }
        }
        catch
        {
            // Fallback: Regex-based parsing if XML is slightly malformed
            doc = ParseRegexFallback(content);
        }

        doc.Reindex();
        return doc;
    }

    public static string Serialize(SubtitleDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<tt xmlns=\"http://www.w3.org/ns/ttml\" xmlns:ttp=\"http://www.w3.org/ns/ttml#parameter\" ttp:timeBase=\"media\" xmlns:tts=\"http://www.w3.org/ns/ttml#styling\" xml:lang=\"en\">");
        sb.AppendLine("  <head>");
        sb.AppendLine("    <styling>");
        sb.AppendLine("      <style xml:id=\"defaultStyle\" tts:fontFamily=\"sansSerif\" tts:fontSize=\"100%\" tts:color=\"white\" tts:textAlign=\"center\" />");
        sb.AppendLine("    </styling>");
        sb.AppendLine("  </head>");
        sb.AppendLine("  <body>");
        sb.AppendLine("    <div>");

        foreach (var cue in document.Cues.OrderBy(c => c.StartTime))
        {
            string startStr = FormatTtmlTimestamp(cue.StartTime);
            string endStr = FormatTtmlTimestamp(cue.EndTime);

            // Convert newline / \N to <br/>
            string textXml = System.Security.SecurityElement.Escape(cue.RawText)
                .Replace("\r\n", "<br/>")
                .Replace("\n", "<br/>")
                .Replace("\\N", "<br/>")
                .Replace("\\n", "<br/>");

            sb.AppendLine($"      <p begin=\"{startStr}\" end=\"{endStr}\" style=\"defaultStyle\">{textXml}</p>");
        }

        sb.AppendLine("    </div>");
        sb.AppendLine("  </body>");
        sb.AppendLine("</tt>");

        return sb.ToString();
    }

    public static string FormatTtmlTimestamp(TimeSpan time)
    {
        int hours = (int)time.TotalHours;
        int minutes = time.Minutes;
        int seconds = time.Seconds;
        int milliseconds = time.Milliseconds;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
    }

    private static string ExtractText(XElement element)
    {
        var sb = new StringBuilder();
        foreach (var node in element.Nodes())
        {
            if (node is XText textNode)
            {
                sb.Append(textNode.Value);
            }
            else if (node is XElement child)
            {
                if (child.Name.LocalName.Equals("br", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append('\n');
                }
                else
                {
                    sb.Append(ExtractText(child));
                }
            }
        }
        return sb.ToString();
    }

    public static TimeSpan ParseTtmlTime(string val)
    {
        val = val.Trim();

        // 1. Seconds format: e.g. "12.345s" or "12s"
        if (val.EndsWith("s", StringComparison.OrdinalIgnoreCase) && !val.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            string num = val[..^1];
            if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double secs))
            {
                return TimeSpan.FromSeconds(secs);
            }
        }

        // 2. Milliseconds format: e.g. "1234ms"
        if (val.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            string num = val[..^2];
            if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double ms))
            {
                return TimeSpan.FromMilliseconds(ms);
            }
        }

        // 3. Colon format: hh:mm:ss.fff or mm:ss.fff
        var match = TimecodeColonRegex.Match(val);
        if (match.Success)
        {
            int h = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            int m = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            int s = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            int ms = 0;
            if (match.Groups[4].Success)
            {
                string msStr = match.Groups[4].Value.PadRight(3, '0')[..3];
                ms = int.Parse(msStr, CultureInfo.InvariantCulture);
            }
            return new TimeSpan(0, h, m, s, ms);
        }

        // 4. Raw seconds fallback
        if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double rawSecs))
        {
            return TimeSpan.FromSeconds(rawSecs);
        }

        return TimeSpan.Zero;
    }

    private static SubtitleDocument ParseRegexFallback(string content)
    {
        var doc = new SubtitleDocument();
        var regex = new Regex(@"<p\b[^>]*begin=[""']([^""']+)[""'][^>]*end=[""']([^""']+)[""'][^>]*>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        int cueIndex = 1;

        foreach (Match match in regex.Matches(content))
        {
            var start = ParseTtmlTime(match.Groups[1].Value);
            var end = ParseTtmlTime(match.Groups[2].Value);
            string raw = match.Groups[3].Value;
            raw = Regex.Replace(raw, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            raw = Regex.Replace(raw, @"<[^>]+>", string.Empty);
            raw = System.Net.WebUtility.HtmlDecode(raw).Trim();

            if (!string.IsNullOrEmpty(raw))
            {
                doc.Cues.Add(new SubtitleCue
                {
                    Index = cueIndex++,
                    StartTime = start,
                    EndTime = end,
                    RawText = raw
                });
            }
        }

        return doc;
    }
}
