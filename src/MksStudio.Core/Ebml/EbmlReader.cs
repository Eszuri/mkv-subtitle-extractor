using System.Buffers.Binary;
using System.Text;

namespace MksStudio.Core.Ebml;

/// <summary>
/// Forward-reading parser for EBML binary streams.
/// </summary>
public class EbmlReader
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    public EbmlReader(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
    }

    public long Position => _stream.Position;
    public long Length => _stream.Length;

    /// <summary>
    /// Reads the next EBML Element Header at current position.
    /// Returns null if end of stream or parent element limit is reached.
    /// </summary>
    public EbmlElementHeader? ReadNextHeader(long maxPosition = -1)
    {
        if (maxPosition >= 0 && _stream.Position >= maxPosition)
            return null;

        if (_stream.Position >= _stream.Length)
            return null;

        long headerOffset = _stream.Position;

        var idVint = Vint.ReadId(_stream);
        if (idVint == null)
            return null;

        var sizeVint = Vint.ReadSize(_stream);
        if (sizeVint == null)
            throw new EndOfStreamException($"Unexpected end of stream while reading size for element ID 0x{idVint.Value.RawId:X}");

        long dataOffset = _stream.Position;

        return new EbmlElementHeader(idVint.Value.RawId, sizeVint.Value.Value, headerOffset, dataOffset);
    }

    /// <summary>
    /// Reads an unsigned integer of specified byte size (1 to 8 bytes, Big-Endian).
    /// </summary>
    public ulong ReadUInt(ulong size)
    {
        if (size == 0) return 0;
        if (size > 8) throw new ArgumentOutOfRangeException(nameof(size), "UInt size cannot exceed 8 bytes.");

        Span<byte> buffer = stackalloc byte[(int)size];
        _stream.ReadExactly(buffer);

        ulong val = 0;
        for (int i = 0; i < (int)size; i++)
        {
            val = (val << 8) | buffer[i];
        }
        return val;
    }

    /// <summary>
    /// Reads a signed integer of specified byte size (1 to 8 bytes, Big-Endian).
    /// </summary>
    public long ReadInt(ulong size)
    {
        if (size == 0) return 0;
        if (size > 8) throw new ArgumentOutOfRangeException(nameof(size), "Int size cannot exceed 8 bytes.");

        Span<byte> buffer = stackalloc byte[(int)size];
        _stream.ReadExactly(buffer);

        long val = (sbyte)buffer[0]; // Sign-extend first byte
        for (int i = 1; i < (int)size; i++)
        {
            val = (val << 8) | buffer[i];
        }
        return val;
    }

    /// <summary>
    /// Reads a float of 4 or 8 bytes (Big-Endian IEEE 754).
    /// </summary>
    public double ReadFloat(ulong size)
    {
        if (size == 0) return 0.0;
        if (size == 4)
        {
            Span<byte> buffer = stackalloc byte[4];
            _stream.ReadExactly(buffer);
            if (BitConverter.IsLittleEndian)
                buffer.Reverse();
            return BinaryPrimitives.ReadSingleLittleEndian(buffer);
        }
        if (size == 8)
        {
            Span<byte> buffer = stackalloc byte[8];
            _stream.ReadExactly(buffer);
            if (BitConverter.IsLittleEndian)
                buffer.Reverse();
            return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
        }
        throw new NotSupportedException($"Float size of {size} bytes is not supported.");
    }

    /// <summary>
    /// Reads an ASCII string of specified length.
    /// </summary>
    public string ReadAsciiString(ulong size)
    {
        if (size == 0) return string.Empty;
        byte[] bytes = new byte[size];
        _stream.ReadExactly(bytes);
        int end = Array.IndexOf(bytes, (byte)0);
        if (end < 0) end = bytes.Length;
        return Encoding.ASCII.GetString(bytes, 0, end);
    }

    /// <summary>
    /// Reads a UTF-8 string of specified length.
    /// </summary>
    public string ReadUtf8String(ulong size)
    {
        if (size == 0) return string.Empty;
        byte[] bytes = new byte[size];
        _stream.ReadExactly(bytes);
        int end = Array.IndexOf(bytes, (byte)0);
        if (end < 0) end = bytes.Length;
        return Encoding.UTF8.GetString(bytes, 0, end);
    }

    /// <summary>
    /// Reads binary bytes of specified length.
    /// </summary>
    public byte[] ReadBinary(ulong size)
    {
        if (size == 0) return [];
        if (size > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(size), "Binary size exceeds maximum memory allocation.");

        byte[] bytes = new byte[size];
        _stream.ReadExactly(bytes);
        return bytes;
    }

    /// <summary>
    /// Skips the data of the current element according to header.
    /// </summary>
    public void Skip(EbmlElementHeader header)
    {
        if (header.IsUnknownSize)
            return;

        long targetPosition = header.DataOffset + (long)header.DataSize;
        if (_stream.CanSeek)
        {
            _stream.Seek(targetPosition, SeekOrigin.Begin);
        }
        else
        {
            long remaining = targetPosition - _stream.Position;
            if (remaining > 0)
            {
                byte[] buffer = new byte[Math.Min(remaining, 81920)];
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(remaining, buffer.Length);
                    int read = _stream.Read(buffer, 0, toRead);
                    if (read == 0) break;
                    remaining -= read;
                }
            }
        }
    }

    /// <summary>
    /// Seeks to a specific position in the stream.
    /// </summary>
    public void Seek(long position)
    {
        _stream.Seek(position, SeekOrigin.Begin);
    }
}
