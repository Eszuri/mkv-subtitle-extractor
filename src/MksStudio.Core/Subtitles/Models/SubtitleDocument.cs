namespace MksStudio.Core.Subtitles.Models;

/// <summary>
/// Represents a structured subtitle document containing cues and styling metadata.
/// </summary>
public class SubtitleDocument
{
    public List<SubtitleCue> Cues { get; } = [];
    public List<AssStyle> Styles { get; } = [];
    public Dictionary<string, string> ScriptInfo { get; } = new(StringComparer.OrdinalIgnoreCase);

    public SubtitleDocument()
    {
        // Standard default script info
        ScriptInfo["Title"] = "MKS Subtitle Track";
        ScriptInfo["ScriptType"] = "v4.00+";
        ScriptInfo["WrapStyle"] = "0";
        ScriptInfo["ScaledBorderAndShadow"] = "yes";
        ScriptInfo["PlayResX"] = "1920";
        ScriptInfo["PlayResY"] = "1080";

        // Add standard default style
        Styles.Add(new AssStyle());
    }

    /// <summary>
    /// Sorts all cues by StartTime and updates their 1-based Index numbers.
    /// </summary>
    public void Reindex()
    {
        var sorted = Cues.OrderBy(c => c.StartTime).ThenBy(c => c.EndTime).ToList();
        Cues.Clear();
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Index = i + 1;
            Cues.Add(sorted[i]);
        }
    }

    /// <summary>
    /// Finds all cues active at a specific timestamp.
    /// </summary>
    public IEnumerable<SubtitleCue> GetActiveCuesAt(TimeSpan time)
    {
        return Cues.Where(c => c.StartTime <= time && c.EndTime >= time);
    }
}
