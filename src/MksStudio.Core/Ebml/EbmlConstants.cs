namespace MksStudio.Core.Ebml;

/// <summary>
/// Contains standard EBML and Matroska element IDs and constant values.
/// </summary>
public static class EbmlConstants
{
    // --- EBML Header Elements ---
    public const uint Ebml = 0x1A45DFA3;
    public const uint EbmlVersion = 0x4286;
    public const uint EbmlReadVersion = 0x42F7;
    public const uint EbmlMaxIdLength = 0x42F2;
    public const uint EbmlMaxSizeLength = 0x42F3;
    public const uint DocType = 0x4282;
    public const uint DocTypeVersion = 0x4287;
    public const uint DocTypeReadVersion = 0x4285;

    // --- Matroska Segment & Top-Level Elements ---
    public const uint Segment = 0x18538067;
    public const uint SeekHead = 0x114D9B74;
    public const uint Seek = 0x4DBB;
    public const uint SeekId = 0x53AB;
    public const uint SeekPosition = 0x53AC;

    // --- Segment Information Elements ---
    public const uint Info = 0x1549A966;
    public const uint SegmentUid = 0x73A4;
    public const uint TimecodeScale = 0x2AD7B1;
    public const uint Duration = 0x4489;
    public const uint DateUtc = 0x4461;
    public const uint Title = 0x7BA9;
    public const uint MuxingApp = 0x4D80;
    public const uint WritingApp = 0x5741;

    // --- Tracks Elements ---
    public const uint Tracks = 0x1654AE6B;
    public const uint TrackEntry = 0xAE;
    public const uint TrackNumber = 0xD7;
    public const uint TrackUid = 0x73C5;
    public const uint TrackType = 0x83;
    public const uint FlagEnabled = 0xB9;
    public const uint FlagDefault = 0x88;
    public const uint FlagForced = 0x55AA;
    public const uint FlagLacing = 0x9C;
    public const uint DefaultDuration = 0x23E383;
    public const uint Name = 0x536E;
    public const uint Language = 0x22B59C;
    public const uint LanguageIetf = 0x22B59D;
    public const uint CodecId = 0x86;
    public const uint CodecPrivate = 0x63A2;
    public const uint CodecName = 0x258688;

    // Track Types
    public const byte TrackTypeVideo = 0x01;
    public const byte TrackTypeAudio = 0x02;
    public const byte TrackTypeComplex = 0x03;
    public const byte TrackTypeLogo = 0x10;
    public const byte TrackTypeSubtitle = 0x11;
    public const byte TrackTypeButtons = 0x12;
    public const byte TrackTypeControl = 0x20;

    // Standard Subtitle Codec IDs
    public const string CodecSrt = "S_TEXT/UTF8";
    public const string CodecAss = "S_TEXT/ASS";
    public const string CodecSsa = "S_TEXT/SSA";
    public const string CodecVtt = "S_TEXT/WEBVTT";
    public const string CodecPgs = "S_HDMV/PGS";
    public const string CodecVobSub = "S_VOBSUB";

    // --- Attachments Elements ---
    public const uint Attachments = 0x1941A469;
    public const uint AttachedFile = 0x61A7;
    public const uint FileDescription = 0x467E;
    public const uint FileName = 0x466E;
    public const uint FileMimeType = 0x4660;
    public const uint FileData = 0x465C;
    public const uint FileUid = 0x46AE;

    // --- Clusters & Blocks ---
    public const uint Cluster = 0x1F43B675;
    public const uint Timecode = 0xE7;
    public const uint PrevSize = 0xAB;
    public const uint BlockGroup = 0xA0;
    public const uint Block = 0xA1;
    public const uint BlockDuration = 0x9B;
    public const uint SimpleBlock = 0xA3;

    // --- Cues ---
    public const uint Cues = 0x1C53BB6B;
    public const uint CuePoint = 0xBB;
    public const uint CueTime = 0xB3;
    public const uint CueTrackPositions = 0xB7;
    public const uint CueTrack = 0xF7;
    public const uint CueClusterPosition = 0xF1;
}
