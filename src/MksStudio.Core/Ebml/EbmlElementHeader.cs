namespace MksStudio.Core.Ebml;

/// <summary>
/// Represents the header of an EBML element (ID and Data Size, plus offset).
/// </summary>
public record EbmlElementHeader(uint Id, ulong DataSize, long HeaderOffset, long DataOffset)
{
    public long TotalSize => (long)(DataOffset - HeaderOffset + (DataSize == Vint.UnknownSize ? 0 : (long)DataSize));
    public bool IsUnknownSize => DataSize == Vint.UnknownSize;
}
