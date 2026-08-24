namespace MksStudio.Core.Ebml;

/// <summary>
/// Represents a Variable-Length Integer (VINT) in EBML.
/// </summary>
public readonly struct Vint : IEquatable<Vint>
{
    public const ulong UnknownSize = ulong.MaxValue;

    public ulong Value { get; }
    public int Length { get; }
    public uint RawId { get; }

    public Vint(ulong value, int length, uint rawId = 0)
    {
        Value = value;
        Length = length;
        RawId = rawId;
    }

    /// <summary>
    /// Reads a VINT for an Element ID from a stream (preserving the marker bit in the ID).
    /// </summary>
    public static Vint? ReadId(Stream stream)
    {
        int firstByte = stream.ReadByte();
        if (firstByte == -1)
            return null;

        int length = GetVintLength((byte)firstByte);
        if (length < 1 || length > 4)
            throw new InvalidDataException($"Invalid EBML ID length: {length}");

        uint id = (uint)firstByte;
        for (int i = 1; i < length; i++)
        {
            int b = stream.ReadByte();
            if (b == -1)
                throw new EndOfStreamException("Unexpected end of stream while reading EBML ID.");
            id = (id << 8) | (uint)b;
        }

        return new Vint(id, length, id);
    }

    /// <summary>
    /// Reads a VINT for Data Size from a stream (masking out the marker bit).
    /// </summary>
    public static Vint? ReadSize(Stream stream)
    {
        int firstByte = stream.ReadByte();
        if (firstByte == -1)
            return null;

        int length = GetVintLength((byte)firstByte);
        if (length < 1 || length > 8)
            throw new InvalidDataException($"Invalid EBML Size length: {length}");

        byte mask = (byte)(0xFF >> length);
        ulong value = (ulong)(firstByte & mask);

        // Check if all data bits in first byte are 1
        bool allOnes = (firstByte & mask) == mask;

        for (int i = 1; i < length; i++)
        {
            int b = stream.ReadByte();
            if (b == -1)
                throw new EndOfStreamException("Unexpected end of stream while reading EBML Size.");
            value = (value << 8) | (byte)b;
            if (b != 0xFF)
                allOnes = false;
        }

        if (allOnes)
        {
            return new Vint(UnknownSize, length);
        }

        return new Vint(value, length);
    }

    /// <summary>
    /// Calculates the number of bytes required by counting leading zeros + 1.
    /// </summary>
    public static int GetVintLength(byte firstByte)
    {
        if (firstByte == 0)
            throw new InvalidDataException("Invalid VINT with 0x00 initial byte.");

        for (int i = 0; i < 8; i++)
        {
            if ((firstByte & (0x80 >> i)) != 0)
                return i + 1;
        }

        return 8;
    }

    /// <summary>
    /// Encodes an Element ID into bytes.
    /// </summary>
    public static byte[] EncodeId(uint id)
    {
        if (id <= 0xFF)
            return [(byte)id];
        if (id <= 0xFFFF)
            return [(byte)(id >> 8), (byte)id];
        if (id <= 0xFFFFFF)
            return [(byte)(id >> 16), (byte)(id >> 8), (byte)id];
        return [(byte)(id >> 24), (byte)(id >> 16), (byte)(id >> 8), (byte)id];
    }

    /// <summary>
    /// Encodes a data size into a VINT (with marker bit inserted).
    /// </summary>
    public static byte[] EncodeSize(ulong size, int minLength = 0)
    {
        if (size == UnknownSize)
        {
            int len = Math.Max(1, minLength);
            byte[] unknown = new byte[len];
            unknown[0] = (byte)(0x80 >> (len - 1));
            for (int i = 0; i < len; i++)
                unknown[i] = 0xFF;
            return unknown;
        }

        int length = 1;
        while (length <= 8)
        {
            ulong maxVal = (1UL << (7 * length)) - 2;
            if (size <= maxVal && length >= minLength)
                break;
            length++;
        }

        if (length > 8)
            throw new ArgumentOutOfRangeException(nameof(size), "Size too large for 8-byte EBML VINT.");

        byte[] result = new byte[length];
        ulong marker = 1UL << (7 * length);
        ulong encoded = size | marker;

        for (int i = length - 1; i >= 0; i--)
        {
            result[i] = (byte)(encoded & 0xFF);
            encoded >>= 8;
        }

        return result;
    }

    public bool Equals(Vint other) => Value == other.Value && Length == other.Length && RawId == other.RawId;
    public override bool Equals(object? obj) => obj is Vint other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Value, Length, RawId);
    public override string ToString() => $"Vint(Value={Value}, Length={Length}, RawId=0x{RawId:X})";
}
