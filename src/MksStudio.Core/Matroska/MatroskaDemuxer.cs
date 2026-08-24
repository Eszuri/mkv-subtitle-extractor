using System.Buffers.Binary;
using System.Text;
using MksStudio.Core.Ebml;
using MksStudio.Core.Matroska.Models;
using MksStudio.Core.Subtitles;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Matroska;

/// <summary>
/// Demuxer to parse .mks (and .mkv subtitle streams) into structured MksFile objects.
/// </summary>
public class MatroskaDemuxer
{
    public MksFile Demux(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var mks = Demux(fs);
        mks.FilePath = filePath;
        return mks;
    }

    public MksFile Demux(Stream stream)
    {
        var reader = new EbmlReader(stream);
        var mks = new MksFile();

        // 1. Read EBML Header
        var ebmlHeader = reader.ReadNextHeader();
        if (ebmlHeader == null || ebmlHeader.Id != EbmlConstants.Ebml)
            throw new InvalidDataException("Invalid file: Missing EBML Header.");

        long ebmlHeaderEnd = ebmlHeader.DataOffset + (long)ebmlHeader.DataSize;
        while (reader.Position < ebmlHeaderEnd)
        {
            var elem = reader.ReadNextHeader(ebmlHeaderEnd);
            if (elem == null) break;

            if (elem.Id == EbmlConstants.DocType)
            {
                string docType = reader.ReadAsciiString(elem.DataSize);
                if (!docType.Equals("matroska", StringComparison.OrdinalIgnoreCase) &&
                    !docType.Equals("webm", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Unsupported DocType: '{docType}'. Expected 'matroska'.");
                }
            }
            else
            {
                reader.Skip(elem);
            }
        }

        // 2. Locate Segment Element
        EbmlElementHeader? segmentHeader = null;
        while (reader.Position < reader.Length)
        {
            var header = reader.ReadNextHeader();
            if (header == null) break;

            if (header.Id == EbmlConstants.Segment)
            {
                segmentHeader = header;
                break;
            }

            reader.Skip(header);
        }

        if (segmentHeader == null)
            throw new InvalidDataException("Invalid Matroska file: Missing Segment element.");

        long segmentEnd = segmentHeader.IsUnknownSize ? reader.Length : segmentHeader.DataOffset + (long)segmentHeader.DataSize;

        // 3. Scan Segment Children (Info, Tracks, Attachments, Clusters)
        while (reader.Position < segmentEnd && reader.Position < reader.Length)
        {
            var sectionHeader = reader.ReadNextHeader(segmentEnd);
            if (sectionHeader == null) break;

            long sectionEnd = sectionHeader.IsUnknownSize ? segmentEnd : sectionHeader.DataOffset + (long)sectionHeader.DataSize;

            switch (sectionHeader.Id)
            {
                case EbmlConstants.Info:
                    ParseInfo(reader, sectionEnd, mks);
                    break;

                case EbmlConstants.Tracks:
                    ParseTracks(reader, sectionEnd, mks);
                    break;

                case EbmlConstants.Attachments:
                    ParseAttachments(reader, sectionEnd, mks);
                    break;

                case EbmlConstants.Cluster:
                    ParseCluster(reader, sectionEnd, mks);
                    break;

                default:
                    reader.Skip(sectionHeader);
                    break;
            }
        }

        // 4. Reindex and finalize all tracks
        foreach (var track in mks.Tracks)
        {
            track.Subtitles.Reindex();
        }

        return mks;
    }

    private void ParseInfo(EbmlReader reader, long maxPosition, MksFile mks)
    {
        while (reader.Position < maxPosition)
        {
            var elem = reader.ReadNextHeader(maxPosition);
            if (elem == null) break;

            switch (elem.Id)
            {
                case EbmlConstants.TimecodeScale:
                    mks.TimecodeScale = reader.ReadUInt(elem.DataSize);
                    break;
                case EbmlConstants.Duration:
                    mks.DurationMs = reader.ReadFloat(elem.DataSize);
                    break;
                case EbmlConstants.Title:
                    mks.Title = reader.ReadUtf8String(elem.DataSize);
                    break;
                case EbmlConstants.MuxingApp:
                    mks.MuxingApp = reader.ReadUtf8String(elem.DataSize);
                    break;
                case EbmlConstants.WritingApp:
                    mks.WritingApp = reader.ReadUtf8String(elem.DataSize);
                    break;
                default:
                    reader.Skip(elem);
                    break;
            }
        }
    }

    private void ParseTracks(EbmlReader reader, long maxPosition, MksFile mks)
    {
        while (reader.Position < maxPosition)
        {
            var trackEntry = reader.ReadNextHeader(maxPosition);
            if (trackEntry == null) break;

            if (trackEntry.Id == EbmlConstants.TrackEntry)
            {
                long entryEnd = trackEntry.DataOffset + (long)trackEntry.DataSize;
                var track = new MksTrack();

                while (reader.Position < entryEnd)
                {
                    var elem = reader.ReadNextHeader(entryEnd);
                    if (elem == null) break;

                    switch (elem.Id)
                    {
                        case EbmlConstants.TrackNumber:
                            track.TrackNumber = reader.ReadUInt(elem.DataSize);
                            break;
                        case EbmlConstants.TrackUid:
                            track.TrackUid = reader.ReadUInt(elem.DataSize);
                            break;
                        case EbmlConstants.TrackType:
                            // byte type = (byte)reader.ReadUInt(elem.DataSize);
                            reader.ReadUInt(elem.DataSize);
                            break;
                        case EbmlConstants.FlagDefault:
                            track.IsDefault = reader.ReadUInt(elem.DataSize) != 0;
                            break;
                        case EbmlConstants.FlagForced:
                            track.IsForced = reader.ReadUInt(elem.DataSize) != 0;
                            break;
                        case EbmlConstants.Name:
                            track.Name = reader.ReadUtf8String(elem.DataSize);
                            break;
                        case EbmlConstants.Language:
                            track.Language = reader.ReadAsciiString(elem.DataSize);
                            break;
                        case EbmlConstants.LanguageIetf:
                            track.LanguageIetf = reader.ReadAsciiString(elem.DataSize);
                            break;
                        case EbmlConstants.CodecId:
                            track.CodecId = reader.ReadAsciiString(elem.DataSize);
                            break;
                        case EbmlConstants.CodecName:
                            track.CodecName = reader.ReadUtf8String(elem.DataSize);
                            break;
                        case EbmlConstants.CodecPrivate:
                            track.CodecPrivate = reader.ReadBinary(elem.DataSize);
                            if (track.IsAss && track.CodecPrivate.Length > 0)
                            {
                                string headerStr = Encoding.UTF8.GetString(track.CodecPrivate);
                                var headerDoc = AssCodec.Parse(headerStr);
                                foreach (var kv in headerDoc.ScriptInfo)
                                    track.Subtitles.ScriptInfo[kv.Key] = kv.Value;
                                if (headerDoc.Styles.Count > 0)
                                {
                                    track.Subtitles.Styles.Clear();
                                    track.Subtitles.Styles.AddRange(headerDoc.Styles);
                                }
                            }
                            break;
                        default:
                            reader.Skip(elem);
                            break;
                    }
                }

                mks.Tracks.Add(track);
            }
            else
            {
                reader.Skip(trackEntry);
            }
        }
    }

    private void ParseAttachments(EbmlReader reader, long maxPosition, MksFile mks)
    {
        while (reader.Position < maxPosition)
        {
            var attachedFileElem = reader.ReadNextHeader(maxPosition);
            if (attachedFileElem == null) break;

            if (attachedFileElem.Id == EbmlConstants.AttachedFile)
            {
                long fileEnd = attachedFileElem.DataOffset + (long)attachedFileElem.DataSize;
                var attachment = new MksAttachment();

                while (reader.Position < fileEnd)
                {
                    var elem = reader.ReadNextHeader(fileEnd);
                    if (elem == null) break;

                    switch (elem.Id)
                    {
                        case EbmlConstants.FileName:
                            attachment.FileName = reader.ReadUtf8String(elem.DataSize);
                            break;
                        case EbmlConstants.FileMimeType:
                            attachment.FileMimeType = reader.ReadAsciiString(elem.DataSize);
                            break;
                        case EbmlConstants.FileDescription:
                            attachment.Description = reader.ReadUtf8String(elem.DataSize);
                            break;
                        case EbmlConstants.FileUid:
                            attachment.FileUid = reader.ReadUInt(elem.DataSize);
                            break;
                        case EbmlConstants.FileData:
                            attachment.Data = reader.ReadBinary(elem.DataSize);
                            break;
                        default:
                            reader.Skip(elem);
                            break;
                    }
                }

                mks.Attachments.Add(attachment);
            }
            else
            {
                reader.Skip(attachedFileElem);
            }
        }
    }

    private void ParseCluster(EbmlReader reader, long maxPosition, MksFile mks)
    {
        ulong clusterTimecode = 0;

        while (reader.Position < maxPosition)
        {
            var elem = reader.ReadNextHeader(maxPosition);
            if (elem == null) break;

            switch (elem.Id)
            {
                case EbmlConstants.Timecode:
                    clusterTimecode = reader.ReadUInt(elem.DataSize);
                    break;

                case EbmlConstants.BlockGroup:
                    ParseBlockGroup(reader, elem.DataOffset + (long)elem.DataSize, clusterTimecode, mks);
                    break;

                case EbmlConstants.SimpleBlock:
                    ParseBlockData(reader.ReadBinary(elem.DataSize), clusterTimecode, TimeSpan.FromSeconds(3), mks);
                    break;

                default:
                    reader.Skip(elem);
                    break;
            }
        }
    }

    private void ParseBlockGroup(EbmlReader reader, long maxPosition, ulong clusterTimecode, MksFile mks)
    {
        byte[]? blockData = null;
        ulong durationScaled = 0;
        bool hasDuration = false;

        while (reader.Position < maxPosition)
        {
            var elem = reader.ReadNextHeader(maxPosition);
            if (elem == null) break;

            switch (elem.Id)
            {
                case EbmlConstants.Block:
                    blockData = reader.ReadBinary(elem.DataSize);
                    break;
                case EbmlConstants.BlockDuration:
                    durationScaled = reader.ReadUInt(elem.DataSize);
                    hasDuration = true;
                    break;
                default:
                    reader.Skip(elem);
                    break;
            }
        }

        if (blockData != null)
        {
            TimeSpan duration = hasDuration
                ? TimeSpan.FromMilliseconds(durationScaled * (mks.TimecodeScale / 1_000_000.0))
                : TimeSpan.FromSeconds(3); // Default 3s if duration not specified

            ParseBlockData(blockData, clusterTimecode, duration, mks);
        }
    }

    private void ParseBlockData(byte[] blockData, ulong clusterTimecode, TimeSpan duration, MksFile mks)
    {
        if (blockData.Length < 4) return;

        using var ms = new MemoryStream(blockData);
        var trackVint = Vint.ReadSize(ms);
        if (trackVint == null) return;

        ulong trackNumber = trackVint.Value.Value;
        var targetTrack = mks.GetTrackByNumber(trackNumber);
        if (targetTrack == null) return;

        Span<byte> timecodeBytes = stackalloc byte[2];
        if (ms.Read(timecodeBytes) != 2) return;
        short timecodeOffset = BinaryPrimitives.ReadInt16BigEndian(timecodeBytes);

        int flags = ms.ReadByte(); // Flags byte (keyframe / lacing)
        if (flags == -1) return;

        int payloadLen = (int)(ms.Length - ms.Position);
        if (payloadLen <= 0) return;

        byte[] payloadBytes = new byte[payloadLen];
        ms.ReadExactly(payloadBytes);

        string text = Encoding.UTF8.GetString(payloadBytes);

        long blockTimeMs = (long)((clusterTimecode + (ulong)timecodeOffset) * (mks.TimecodeScale / 1_000_000.0));
        TimeSpan startTime = TimeSpan.FromMilliseconds(Math.Max(0, blockTimeMs));

        if (targetTrack.IsAss)
        {
            var cue = AssCodec.ParseMatroskaBlock(text, startTime, duration);
            targetTrack.Subtitles.Cues.Add(cue);
        }
        else
        {
            var cue = new SubtitleCue
            {
                StartTime = startTime,
                EndTime = startTime + duration,
                RawText = text
            };
            targetTrack.Subtitles.Cues.Add(cue);
        }
    }
}
