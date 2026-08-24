namespace MksStudio.Core.Matroska.Models;

/// <summary>
/// Represents an attached file inside the Matroska container (e.g. font TTF/OTF or image).
/// </summary>
public class MksAttachment
{
    public ulong FileUid { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileMimeType { get; set; } = "application/x-truetype-font";
    public string? Description { get; set; }
    public byte[] Data { get; set; } = [];

    public long FileSize => Data.Length;

    public static string DetectMimeType(string filename)
    {
        string ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".ttf" => "application/x-truetype-font",
            ".otf" => "application/vnd.ms-opentype",
            ".woff" => "application/font-woff",
            ".woff2" => "application/font-woff2",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
    }
}
