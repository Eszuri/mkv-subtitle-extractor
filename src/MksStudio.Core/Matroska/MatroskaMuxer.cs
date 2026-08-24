using System.Buffers.Binary;
using System.Text;
using MksStudio.Core.Ebml;
using MksStudio.Core.Matroska.Models;
using MksStudio.Core.Subtitles;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Matroska;

/// <summary>
/// Muxer to serialize MksFile models into standard Matroska (.mks) binary streams.
/// </summary>
public class MatroskaMuxer
{
    private record CueEntry(ulong TrackNumber, SubtitleCue Cue, int ReadOrder, bool IsAss);

    public void Mux(MksFile mks, string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        Mux(mks, fs);
    }

    public void Mux(MksFile mks, Stream stream)
    {
        var writer = new EbmlWriter(stream);

        // 1. Write EBML Header
        writer.WriteMasterElement(EbmlConstants.Ebml, ebml =>
        {
            ebml.WriteUInt(EbmlConstants.EbmlVersion, 1);
            ebml.WriteUInt(EbmlConstants.EbmlReadVersion, 1);
            ebml.WriteUInt(EbmlConstants.EbmlMaxIdLength, 4);
            ebml.WriteUInt(EbmlConstants.EbmlMaxSizeLength, 8);
            ebml.WriteAsciiString(EbmlConstants.DocType, "matroska");
            ebml.WriteUInt(EbmlConstants.DocTypeVersion, 4);
            ebml.WriteUInt(EbmlConstants.DocTypeReadVersion, 2);
        });

        // 2. Write Segment (Using Unknown Size to permit streaming/standard playback)
        writer.WriteId(EbmlConstants.Segment);
        writer.WriteSize(Vint.UnknownSize);

        // 3. Write Info Element
        writer.WriteMasterElement(EbmlConstants.Info, info =>
        {
            ulong timecodeScale = mks.TimecodeScale > 0 ? mks.TimecodeScale : 1_000_000;
            info.WriteUInt(EbmlConstants.TimecodeScale, timecodeScale);
            info.WriteFloat(EbmlConstants.Duration, mks.TotalDuration.TotalMilliseconds);
            if (!string.IsNullOrWhiteSpace(mks.Title))
                info.WriteUtf8String(EbmlConstants.Title, mks.Title);
            info.WriteUtf8String(EbmlConstants.MuxingApp, mks.MuxingApp ?? "MksStudio v1.0");
            info.WriteUtf8String(EbmlConstants.WritingApp, mks.WritingApp ?? "MksStudio Core Engine");
        });

        // 4. Write Tracks Element
        writer.WriteMasterElement(EbmlConstants.Tracks, tracksElem =>
        {
            ulong trackNum = 1;
            foreach (var track in mks.Tracks)
            {
                track.TrackNumber = trackNum++;
                tracksElem.WriteMasterElement(EbmlConstants.TrackEntry, trackEntry =>
                {
                    trackEntry.WriteUInt(EbmlConstants.TrackNumber, track.TrackNumber);
                    trackEntry.WriteUInt(EbmlConstants.TrackUid, track.TrackUid > 0 ? track.TrackUid : track.TrackNumber * 1000 + 1);
                    trackEntry.WriteUInt(EbmlConstants.TrackType, EbmlConstants.TrackTypeSubtitle);
                    trackEntry.WriteUInt(EbmlConstants.FlagDefault, track.IsDefault ? 1UL : 0UL);
                    trackEntry.WriteUInt(EbmlConstants.FlagForced, track.IsForced ? 1UL : 0UL);
                    trackEntry.WriteUInt(EbmlConstants.FlagLacing, 0);

                    if (!string.IsNullOrWhiteSpace(track.Name))
                        trackEntry.WriteUtf8String(EbmlConstants.Name, track.Name);

                    string lang = string.IsNullOrWhiteSpace(track.Language) ? "und" : track.Language;
                    trackEntry.WriteAsciiString(EbmlConstants.Language, lang);

                    if (!string.IsNullOrWhiteSpace(track.LanguageIetf))
                        trackEntry.WriteAsciiString(EbmlConstants.LanguageIetf, track.LanguageIetf);

                    trackEntry.WriteAsciiString(EbmlConstants.CodecId, track.CodecId);

                    if (track.IsAss)
                    {
                        string headerText = AssCodec.GenerateCodecPrivate(track.Subtitles);
                        byte[] privateData = Encoding.UTF8.GetBytes(headerText);
                        trackEntry.WriteBinary(EbmlConstants.CodecPrivate, privateData);
                    }
                    else if (track.CodecPrivate != null && track.CodecPrivate.Length > 0)
                    {
                        trackEntry.WriteBinary(EbmlConstants.CodecPrivate, track.CodecPrivate);
                    }
                });
            }
        });

        // 5. Write Attachments (Fonts, Images)
        if (mks.Attachments.Count > 0)
        {
            writer.WriteMasterElement(EbmlConstants.Attachments, attachElem =>
            {
                ulong uid = 1000;
                foreach (var att in mks.Attachments)
                {
                    attachElem.WriteMasterElement(EbmlConstants.AttachedFile, fileElem =>
                    {
                        if (!string.IsNullOrWhiteSpace(att.Description))
                            fileElem.WriteUtf8String(EbmlConstants.FileDescription, att.Description);
                        fileElem.WriteUtf8String(EbmlConstants.FileName, att.FileName);
                        fileElem.WriteAsciiString(EbmlConstants.FileMimeType, att.FileMimeType);
                        fileElem.WriteBinary(EbmlConstants.FileData, att.Data);
                        fileElem.WriteUInt(EbmlConstants.FileUid, att.FileUid > 0 ? att.FileUid : uid++);
                    });
                }
            });
        }

        // 6. Write Clusters
        // Collect all cues from all tracks and sort chronologically
        var allCues = new List<CueEntry>();
        foreach (var track in mks.Tracks)
        {
            int order = 0;
            foreach (var cue in track.Subtitles.Cues)
            {
                allCues.Add(new CueEntry(track.TrackNumber, cue, order++, track.IsAss));
            }
        }

        allCues.Sort((a, b) => a.Cue.StartTime.CompareTo(b.Cue.StartTime));

        // Group into clusters of 10-second intervals
        const int ClusterIntervalMs = 10000;
        int currentClusterBaseMs = 0;
        var clusterCues = new List<CueEntry>();

        void FlushCluster()
        {
            if (clusterCues.Count == 0) return;

            writer.WriteMasterElement(EbmlConstants.Cluster, clusterElem =>
            {
                clusterElem.WriteUInt(EbmlConstants.Timecode, (ulong)currentClusterBaseMs);

                foreach (var entry in clusterCues)
                {
                    clusterElem.WriteMasterElement(EbmlConstants.BlockGroup, blockGroup =>
                    {
                        // Build Block Payload
                        string payloadText = entry.IsAss
                            ? AssCodec.FormatMatroskaBlock(entry.Cue, entry.ReadOrder)
                            : entry.Cue.RawText;

                        byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadText);
                        long cueStartMs = (long)entry.Cue.StartTime.TotalMilliseconds;
                        short relTimecode = (short)Math.Clamp(cueStartMs - currentClusterBaseMs, short.MinValue, short.MaxValue);

                        using var blockMs = new MemoryStream();
                        // 1. Track Number as VINT
                        byte[] trackVint = Vint.EncodeSize(entry.TrackNumber);
                        blockMs.Write(trackVint);

                        // 2. Relative Timecode (Int16 Big-Endian)
                        Span<byte> tcBytes = stackalloc byte[2];
                        BinaryPrimitives.WriteInt16BigEndian(tcBytes, relTimecode);
                        blockMs.Write(tcBytes);

                        // 3. Flags (0x80 = Keyframe, no lacing)
                        blockMs.WriteByte(0x80);

                        // 4. Payload
                        blockMs.Write(payloadBytes);

                        blockGroup.WriteBinary(EbmlConstants.Block, blockMs.ToArray());

                        // BlockDuration in ms
                        ulong durationMs = (ulong)Math.Max(1, (long)entry.Cue.Duration.TotalMilliseconds);
                        blockGroup.WriteUInt(EbmlConstants.BlockDuration, durationMs);
                    });
                }
            });

            clusterCues.Clear();
        }

        foreach (var entry in allCues)
        {
            long startMs = (long)entry.Cue.StartTime.TotalMilliseconds;
            if (clusterCues.Count > 0 && startMs >= currentClusterBaseMs + ClusterIntervalMs)
            {
                FlushCluster();
                currentClusterBaseMs = (int)(startMs / ClusterIntervalMs) * ClusterIntervalMs;
            }
            else if (clusterCues.Count == 0)
            {
                currentClusterBaseMs = (int)(startMs / ClusterIntervalMs) * ClusterIntervalMs;
            }

            clusterCues.Add(entry);
        }

        FlushCluster();
    }
}
