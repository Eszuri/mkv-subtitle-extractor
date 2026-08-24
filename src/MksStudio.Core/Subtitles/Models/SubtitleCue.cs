namespace MksStudio.Core.Subtitles.Models;

/// <summary>
/// Represents an individual subtitle line/cue with timing, text, and styling information.
/// </summary>
public class SubtitleCue
{
    public int Index { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public TimeSpan Duration => EndTime >= StartTime ? EndTime - StartTime : TimeSpan.Zero;

    public int Layer { get; set; } = 0;
    public string Style { get; set; } = "Default";
    public string Actor { get; set; } = string.Empty;
    public int MarginL { get; set; } = 0;
    public int MarginR { get; set; } = 0;
    public int MarginV { get; set; } = 0;
    public string Effect { get; set; } = string.Empty;

    public string RawText { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable plain text without formatting tags.
    /// </summary>
    public string PlainText
    {
        get => CleanTags(RawText);
        set => RawText = value;
    }

    public SubtitleCue Clone()
    {
        return new SubtitleCue
        {
            Index = Index,
            StartTime = StartTime,
            EndTime = EndTime,
            Layer = Layer,
            Style = Style,
            Actor = Actor,
            MarginL = MarginL,
            MarginR = MarginR,
            MarginV = MarginV,
            Effect = Effect,
            RawText = RawText
        };
    }

    public static string CleanTags(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Replace ASS linebreaks \N or \n
        string text = input.Replace("\\N", "\n").Replace("\\n", "\n").Replace("\\h", " ");

        // Remove ASS override tags: {...}
        while (true)
        {
            int start = text.IndexOf('{');
            if (start < 0) break;
            int end = text.IndexOf('}', start);
            if (end < 0) break;
            text = text.Remove(start, end - start + 1);
        }

        // Remove HTML tags: <b>, <i>, <u>, <font ...>, etc.
        while (true)
        {
            int start = text.IndexOf('<');
            if (start < 0) break;
            int end = text.IndexOf('>', start);
            if (end < 0) break;
            text = text.Remove(start, end - start + 1);
        }

        return text.Trim();
    }

    public override string ToString() => $"[{StartTime:hh\\:mm\\:ss\\.fff} --> {EndTime:hh\\:mm\\:ss\\.fff}] {PlainText}";
}
