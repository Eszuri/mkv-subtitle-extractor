namespace MksStudio.Core.Subtitles.Models;

/// <summary>
/// Represents an ASS (Advanced SubStation Alpha) V4+ style definition.
/// Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour,
///         Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle,
///         BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
/// </summary>
public class AssStyle
{
    public string Name { get; set; } = "Default";
    public string Fontname { get; set; } = "Arial";
    public double Fontsize { get; set; } = 20;
    public string PrimaryColour { get; set; } = "&H00FFFFFF";
    public string SecondaryColour { get; set; } = "&H000000FF";
    public string OutlineColour { get; set; } = "&H00000000";
    public string BackColour { get; set; } = "&H00000000";
    public int Bold { get; set; } = 0;
    public int Italic { get; set; } = 0;
    public int Underline { get; set; } = 0;
    public int StrikeOut { get; set; } = 0;
    public double ScaleX { get; set; } = 100;
    public double ScaleY { get; set; } = 100;
    public double Spacing { get; set; } = 0;
    public double Angle { get; set; } = 0;
    public int BorderStyle { get; set; } = 1;
    public double Outline { get; set; } = 2;
    public double Shadow { get; set; } = 2;
    public int Alignment { get; set; } = 2; // 2 = Bottom-Center in numpad layout
    public int MarginL { get; set; } = 10;
    public int MarginR { get; set; } = 10;
    public int MarginV { get; set; } = 10;
    public int Encoding { get; set; } = 1;

    public static AssStyle Parse(string styleLine)
    {
        // Line format: "Style: Name,Fontname,..."
        string content = styleLine;
        if (content.StartsWith("Style:", StringComparison.OrdinalIgnoreCase))
            content = content.Substring(6).Trim();

        string[] parts = content.Split(',');
        var style = new AssStyle();

        if (parts.Length > 0) style.Name = parts[0].Trim();
        if (parts.Length > 1) style.Fontname = parts[1].Trim();
        if (parts.Length > 2 && double.TryParse(parts[2].Trim(), out var fs)) style.Fontsize = fs;
        if (parts.Length > 3) style.PrimaryColour = parts[3].Trim();
        if (parts.Length > 4) style.SecondaryColour = parts[4].Trim();
        if (parts.Length > 5) style.OutlineColour = parts[5].Trim();
        if (parts.Length > 6) style.BackColour = parts[6].Trim();
        if (parts.Length > 7 && int.TryParse(parts[7].Trim(), out var b)) style.Bold = b;
        if (parts.Length > 8 && int.TryParse(parts[8].Trim(), out var it)) style.Italic = it;
        if (parts.Length > 9 && int.TryParse(parts[9].Trim(), out var u)) style.Underline = u;
        if (parts.Length > 10 && int.TryParse(parts[10].Trim(), out var so)) style.StrikeOut = so;
        if (parts.Length > 11 && double.TryParse(parts[11].Trim(), out var sx)) style.ScaleX = sx;
        if (parts.Length > 12 && double.TryParse(parts[12].Trim(), out var sy)) style.ScaleY = sy;
        if (parts.Length > 13 && double.TryParse(parts[13].Trim(), out var sp)) style.Spacing = sp;
        if (parts.Length > 14 && double.TryParse(parts[14].Trim(), out var ang)) style.Angle = ang;
        if (parts.Length > 15 && int.TryParse(parts[15].Trim(), out var bs)) style.BorderStyle = bs;
        if (parts.Length > 16 && double.TryParse(parts[16].Trim(), out var ol)) style.Outline = ol;
        if (parts.Length > 17 && double.TryParse(parts[17].Trim(), out var sh)) style.Shadow = sh;
        if (parts.Length > 18 && int.TryParse(parts[18].Trim(), out var al)) style.Alignment = al;
        if (parts.Length > 19 && int.TryParse(parts[19].Trim(), out var ml)) style.MarginL = ml;
        if (parts.Length > 20 && int.TryParse(parts[20].Trim(), out var mr)) style.MarginR = mr;
        if (parts.Length > 21 && int.TryParse(parts[21].Trim(), out var mv)) style.MarginV = mv;
        if (parts.Length > 22 && int.TryParse(parts[22].Trim(), out var enc)) style.Encoding = enc;

        return style;
    }

    public string ToAssString()
    {
        return $"Style: {Name},{Fontname},{Fontsize},{PrimaryColour},{SecondaryColour},{OutlineColour},{BackColour},{Bold},{Italic},{Underline},{StrikeOut},{ScaleX},{ScaleY},{Spacing},{Angle},{BorderStyle},{Outline},{Shadow},{Alignment},{MarginL},{MarginR},{MarginV},{Encoding}";
    }
}
