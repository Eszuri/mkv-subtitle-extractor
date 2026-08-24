using System.Buffers.Binary;
using System.Text;

namespace MksStudio.Core.Ebml;

/// <summary>
/// Forward-writing serializer for EBML binary streams.
/// </summary>
public class EbmlWriter
{
    private readonly Stream _stream;

    public EbmlWriter(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public long Position => _stream.Position;

    /// <summary>
    /// Writes an EBML ID.
    /// </summary>
    public void WriteId(uint id)
    {
        byte[] encoded = Vint.EncodeId(id);
        _stream.Write(encoded, 0, encoded.Length);
    }

    /// <summary>
    /// Writes a data size as a VINT.
    /// </summary>
    public void WriteSize(ulong size, int minLength = 0)
    {
        byte[] encoded = Vint.EncodeSize(size, minLength);
        _stream.Write(encoded, 0, encoded.Length);
    }

    /// <summary>
    /// Writes a master element containing children by buffering children to calculate size.
    /// </summary>
    public void WriteMasterElement(uint id, Action<EbmlWriter> writeChildren)
    {
        using var ms = new MemoryStream();
        var childWriter = new EbmlWriter(ms);
        writeChildren(childWriter);
        byte[] childBytes = ms.ToArray();

        WriteId(id);
        WriteSize((ulong)childBytes.Length);
        _stream.Write(childBytes, 0, childBytes.Length);
    }

    /// <summary>
    /// Writes an unsigned integer element (1 to 8 bytes, minimal bytes by default).
    /// </summary>
    public void WriteUInt(uint id, ulong value, int fixedSize = 0)
    {
        int size = fixedSize > 0 ? fixedSize : GetUIntByteCount(value);
        WriteId(id);
        WriteSize((ulong)size);

        Span<byte> buffer = stackalloc byte[size];
        for (int i = size - 1; i >= 0; i--)
        {
            buffer[i] = (byte)(value & 0xFF);
            value >>= 8;
        }
        _stream.Write(buffer);
    }

    /// <summary>
    /// Writes a signed integer element.
    /// </summary>
    public void WriteInt(uint id, long value, int fixedSize = 0)
    {
        int size = fixedSize > 0 ? fixedSize : GetIntByteCount(value);
        WriteId(id);
        WriteSize((ulong)size);

        Span<byte> buffer = stackalloc byte[size];
        for (int i = size - 1; i >= 0; i--)
        {
            buffer[i] = (byte)(value & 0xFF);
            value >>= 8;
        }
        _stream.Write(buffer);
    }

    /// <summary>
    /// Writes a float element (4 or 8 bytes).
    /// </summary>
    public void WriteFloat(uint id, double value, bool doublePrecision = true)
    {
        WriteId(id);
        if (doublePrecision)
        {
            WriteSize(8);
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteDoubleLittleEndian(buffer, value);
            if (BitConverter.IsLittleEndian)
                buffer.Reverse();
            _stream.Write(buffer);
        }
        else
        {
            WriteSize(4);
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteSingleLittleEndian(buffer, (float)value);
            if (BitConverter.IsLittleEndian)
                buffer.Reverse();
            _stream.Write(buffer);
        }
    }

    /// <summary>
    /// Writes an ASCII string element.
    /// </summary>
    public void WriteAsciiString(uint id, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
        WriteId(id);
        WriteSize((ulong)bytes.Length);
        if (bytes.Length > 0)
            _stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Writes a UTF-8 string element.
    /// </summary>
    public void WriteUtf8String(uint id, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        WriteId(id);
        WriteSize((ulong)bytes.Length);
        if (bytes.Length > 0)
            _stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Writes a binary element.
    /// </summary>
    public void WriteBinary(uint id, ReadOnlySpan<byte> data)
    {
        WriteId(id);
        WriteSize((ulong)data.Length);
        if (data.Length > 0)
            _stream.Write(data);
    }

    private static int GetUIntByteCount(ulong value)
    {
        if (value <= 0xFF) return 1;
        if (value <= 0xFFFF) return 2;
        if (value <= 0xFFFFFF) return 3;
        if (value <= 0xFFFFFFFF) return 4;
        if (value <= 0xFFFFFFFFFF) return 5;
        if (value <= 0xFFFFFFFFFFFF) return 6;
        if (value <= 0xFFFFFFFFFFFFFF) return 7;
        return 8;
    }

    private static int GetIntByteCount(long value)
    {
        if (value is >= sbyte.MinValue and <= sbyte.MaxValue) return 1;
        if (value is >= short.MinValue and <= short.MaxValue) return 2;
        if (value is >= -8388608 and <= 8388607) return 3;
        if (value is >= int.MinValue and <= int.MaxValue) return 4;
        return 8;
    }
}
