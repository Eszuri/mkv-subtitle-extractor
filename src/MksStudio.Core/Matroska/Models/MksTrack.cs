using MksStudio.Core.Ebml;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Matroska.Models;

/// <summary>
/// Represents a subtitle track in the .mks container.
/// </summary>
public class MksTrack
{
    public ulong TrackNumber { get; set; } = 1;
    public ulong TrackUid { get; set; } = (ulong)Random.Shared.Next(100000, 9999999);
    public string CodecId { get; set; } = EbmlConstants.CodecSrt;
    public string CodecName { get; set; } = "SubRip";
    public string Name { get; set; } = "Subtitle Track";
    public string Language { get; set; } = "und"; // Undetermined
    public string? LanguageIetf { get; set; }
    public bool IsDefault { get; set; } = true;
    public bool IsForced { get; set; } = false;
    public byte[]? CodecPrivate { get; set; }

    public SubtitleDocument Subtitles { get; set; } = new();

    public int CueCount => Subtitles.Cues.Count;

    public bool IsAss => CodecId.Equals(EbmlConstants.CodecAss, StringComparison.OrdinalIgnoreCase) ||
                         CodecId.Equals(EbmlConstants.CodecSsa, StringComparison.OrdinalIgnoreCase);

    public bool IsSrt => CodecId.Equals(EbmlConstants.CodecSrt, StringComparison.OrdinalIgnoreCase);
    public bool IsVtt => CodecId.Equals(EbmlConstants.CodecVtt, StringComparison.OrdinalIgnoreCase);
}
