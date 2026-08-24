using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Operations;

/// <summary>
/// Service to perform time-shifting and framerate scaling on subtitle cues.
/// </summary>
public static class TimeShiftService
{
    /// <summary>
    /// Shifts cue timestamps by the given millisecond offset.
    /// </summary>
    public static void Shift(IEnumerable<SubtitleCue> cues, long offsetMs)
    {
        var offset = TimeSpan.FromMilliseconds(offsetMs);
        foreach (var cue in cues)
        {
            var newStart = cue.StartTime + offset;
            var newEnd = cue.EndTime + offset;

            cue.StartTime = newStart < TimeSpan.Zero ? TimeSpan.Zero : newStart;
            cue.EndTime = newEnd < TimeSpan.Zero ? TimeSpan.Zero : newEnd;
        }
    }

    /// <summary>
    /// Stretches/Scales cue timestamps by a multiplier (e.g., sourceFps / targetFps).
    /// </summary>
    public static void Scale(IEnumerable<SubtitleCue> cues, double factor)
    {
        if (factor <= 0) return;

        foreach (var cue in cues)
        {
            long startMs = (long)Math.Round(cue.StartTime.TotalMilliseconds * factor);
            long endMs = (long)Math.Round(cue.EndTime.TotalMilliseconds * factor);

            cue.StartTime = TimeSpan.FromMilliseconds(Math.Max(0, startMs));
            cue.EndTime = TimeSpan.FromMilliseconds(Math.Max(0, endMs));
        }
    }

    /// <summary>
    /// Converts timestamps between common video frame rates (e.g. 23.976 to 25.0).
    /// </summary>
    public static void ConvertFrameRate(IEnumerable<SubtitleCue> cues, double sourceFps, double targetFps)
    {
        if (sourceFps <= 0 || targetFps <= 0) return;
        double factor = sourceFps / targetFps;
        Scale(cues, factor);
    }
}
