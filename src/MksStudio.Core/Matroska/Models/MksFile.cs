namespace MksStudio.Core.Matroska.Models;

/// <summary>
/// Represents a complete .mks file container including tracks, attachments, and segment metadata.
/// </summary>
public class MksFile
{
    public string? FilePath { get; set; }
    public string Title { get; set; } = "Matroska Subtitles";
    public ulong TimecodeScale { get; set; } = 1_000_000; // 1 millisecond
    public double DurationMs { get; set; }
    public string MuxingApp { get; set; } = "MksStudio v1.0";
    public string WritingApp { get; set; } = "MksStudio Core Engine";

    public List<MksTrack> Tracks { get; } = [];
    public List<MksAttachment> Attachments { get; } = [];

    public TimeSpan TotalDuration
    {
        get
        {
            if (Tracks.Count == 0)
                return TimeSpan.FromMilliseconds(DurationMs);

            var maxEnd = Tracks
                .SelectMany(t => t.Subtitles.Cues)
                .Select(c => c.EndTime)
                .DefaultIfEmpty(TimeSpan.Zero)
                .Max();

            return maxEnd > TimeSpan.Zero ? maxEnd : TimeSpan.FromMilliseconds(DurationMs);
        }
    }

    public MksTrack? GetTrackByNumber(ulong trackNumber)
    {
        return Tracks.FirstOrDefault(t => t.TrackNumber == trackNumber);
    }
}
