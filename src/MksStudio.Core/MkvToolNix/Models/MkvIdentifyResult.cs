using System.Text.Json.Serialization;

namespace MksStudio.Core.MkvToolNix.Models;

public class MkvIdentifyResult
{
    [JsonPropertyName("container")]
    public MkvContainer? Container { get; set; }

    [JsonPropertyName("tracks")]
    public List<MkvTrack> Tracks { get; set; } = [];

    [JsonPropertyName("attachments")]
    public List<MkvAttachment> Attachments { get; set; } = [];

    [JsonIgnore]
    public IEnumerable<MkvTrack> SubtitleTracks =>
        Tracks.Where(t => t.Type?.Equals("subtitles", StringComparison.OrdinalIgnoreCase) == true);
}

public class MkvContainer
{
    [JsonPropertyName("recognized")]
    public bool Recognized { get; set; }

    [JsonPropertyName("supported")]
    public bool Supported { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("properties")]
    public MkvContainerProperties? Properties { get; set; }
}

public class MkvContainerProperties
{
    [JsonPropertyName("duration")]
    public ulong? DurationNanoseconds { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonIgnore]
    public TimeSpan Duration => DurationNanoseconds.HasValue
        ? TimeSpan.FromTicks((long)(DurationNanoseconds.Value / 100))
        : TimeSpan.Zero;
}

public class MkvTrack
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("codec")]
    public string? Codec { get; set; }

    [JsonPropertyName("properties")]
    public MkvTrackProperties? Properties { get; set; }

    [JsonIgnore]
    public string TrackName => Properties?.TrackName ?? string.Empty;

    [JsonIgnore]
    public string Language => Properties?.Language ?? "und";

    [JsonIgnore]
    public string? LanguageIetf => Properties?.LanguageIetf;

    [JsonIgnore]
    public string CodecId => Properties?.CodecId ?? string.Empty;

    [JsonIgnore]
    public bool IsDefault => Properties?.DefaultTrack ?? false;

    [JsonIgnore]
    public bool IsForced => Properties?.ForcedTrack ?? false;

    [JsonIgnore]
    public string DefaultExtension
    {
        get
        {
            string cid = CodecId.ToUpperInvariant();
            if (cid.Contains("UTF8") || cid.Contains("SRT") || Codec?.Contains("SubRip") == true) return ".srt";
            if (cid.Contains("ASS") || cid.Contains("SSA") || Codec?.Contains("SubStation") == true) return ".ass";
            if (cid.Contains("WEBVTT") || cid.Contains("VTT")) return ".vtt";
            if (cid.Contains("PGS") || cid.Contains("HDMV")) return ".sup";
            if (cid.Contains("VOBSUB")) return ".idx";
            return ".srt";
        }
    }
}

public class MkvTrackProperties
{
    [JsonPropertyName("codec_id")]
    public string? CodecId { get; set; }

    [JsonPropertyName("track_name")]
    public string? TrackName { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("language_ietf")]
    public string? LanguageIetf { get; set; }

    [JsonPropertyName("default_track")]
    public bool DefaultTrack { get; set; }

    [JsonPropertyName("forced_track")]
    public bool ForcedTrack { get; set; }
}

public class MkvAttachment
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonIgnore]
    public string SizeFormatted
    {
        get
        {
            if (Size < 1024) return $"{Size} B";
            if (Size < 1024 * 1024) return $"{Size / 1024.0:F1} KB";
            return $"{Size / (1024.0 * 1024.0):F2} MB";
        }
    }
}
