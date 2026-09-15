using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Subtitles;

/// <summary>
/// Parser and Serializer for MicroDVD (.sub) subtitle format.
/// Timecodes are represented as frame numbers: {start_frame}{end_frame}Subtitle text|Second line.
/// </summary>
public static class MicroDvdCodec
{
    private static readonly Regex MicroDvdLineRegex = new(
        @"^\{(\d+)\}\{(\d+)\}(.*)$",
        RegexOptions.Compiled);

    public const double DefaultFps = 25.0;

    public static SubtitleDocument Parse(string content, double defaultFps = DefaultFps)
    {
        var doc = new SubtitleDocument();
        if (string.IsNullOrWhiteSpace(content))
            return doc;

        double fps = defaultFps;
        string[] lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int cueIndex = 1;

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var match = MicroDvdLineRegex.Match(line);
            if (!match.Success)
                continue;

            long startFrame = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            long endFrame = long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            string text = match.Groups[3].Value;

            // MicroDVD FPS header detection: e.g. {1}{1}25.000 or {1}{1}23.976
            if (startFrame == 1 && endFrame == 1)
            {
                if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedFps) && parsedFps > 0)
                {
                    fps = parsedFps;
                    continue;
                }
            }

            var startTime = TimeSpan.FromSeconds(startFrame / fps);
            var endTime = TimeSpan.FromSeconds(endFrame / fps);

            // Replace pipe '|' with newline and strip MicroDVD style tags like {Y:i}, {Y:b}, {C:$...}
            string cleanedText = text.Replace('|', '\n');
            cleanedText = Regex.Replace(cleanedText, @"\{[A-Za-z]:[^}]*\}", string.Empty);
            cleanedText = cleanedText.Trim();

            if (!string.IsNullOrEmpty(cleanedText))
            {
                doc.Cues.Add(new SubtitleCue
                {
                    Index = cueIndex++,
                    StartTime = startTime,
                    EndTime = endTime,
                    RawText = cleanedText
                });
            }
        }

        doc.Reindex();
        return doc;
    }

    public static string Serialize(SubtitleDocument document, double fps = DefaultFps)
    {
        var sb = new StringBuilder();
        // Standard MicroDVD framerate definition header
        sb.AppendLine($"{{1}}{{1}}{fps.ToString("0.000", CultureInfo.InvariantCulture)}");

        foreach (var cue in document.Cues.OrderBy(c => c.StartTime))
        {
            long startFrame = (long)Math.Round(cue.StartTime.TotalSeconds * fps);
            long endFrame = (long)Math.Round(cue.EndTime.TotalSeconds * fps);

            if (endFrame <= startFrame)
            {
                endFrame = startFrame + (long)Math.Round(fps * 2.0); // minimum 2 seconds
            }

            // Convert newlines to pipe '|'
            string text = cue.RawText
                .Replace("\r\n", "|")
                .Replace("\n", "|")
                .Replace("\\N", "|")
                .Replace("\\n", "|");

            sb.AppendLine($"{{{startFrame}}}{{{endFrame}}}{text}");
        }

        return sb.ToString().TrimEnd();
    }
}
