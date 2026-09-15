using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace MksStudio.UI.Controls;

public sealed class TextSpanInfo
{
    public int Start { get; set; }
    public int Length { get; set; }
    public FontWeight FontWeight { get; set; } = FontWeights.Normal;
    public FontStyle FontStyle { get; set; } = FontStyles.Normal;
    public Brush? ForegroundBrush { get; set; }
    public bool Underline { get; set; }
    public bool Strikethrough { get; set; }
    public double? FontSize { get; set; }
}

public sealed class AssParseResult
{
    public string CleanText { get; set; } = string.Empty;
    public List<TextSpanInfo> Spans { get; } = [];
    public Brush? OverrideStrokeBrush { get; set; }
    public Brush? OverrideShadowBrush { get; set; }
    public int? Alignment { get; set; } // \an1 to \an9
}

/// <summary>
/// Advanced subtitle markup parser for ASS and HTML override tags.
/// Extracts clean text, per-span styling (color, bold, italic, underline, strike, size),
/// and global overrides (alignment, stroke color, shadow color).
/// </summary>
public static class AssTagParser
{
    public static AssParseResult Parse(
        string? rawText,
        FontWeight defaultWeight,
        FontStyle defaultStyle,
        Brush? defaultFill,
        double defaultFontSize = 22.0)
    {
        var result = new AssParseResult();
        if (string.IsNullOrEmpty(rawText)) return result;

        // Convert ASS linebreaks (\N, \n) to real newlines and \h to hard spaces (case-insensitive)
        string s = System.Text.RegularExpressions.Regex.Replace(rawText, @"\\[hH]", " ");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\\[nN]", "\n");

        var sb = new StringBuilder(s.Length);
        int i = 0;

        bool isBaseBold = defaultWeight == FontWeights.Bold || defaultWeight == FontWeights.SemiBold || defaultWeight == FontWeights.ExtraBold || defaultWeight == FontWeights.Black;
        bool isBaseItalic = defaultStyle == FontStyles.Italic;

        bool curBold = false;
        bool curItalic = isBaseItalic;
        bool curUnderline = false;
        bool curStrike = false;
        Brush? curFill = defaultFill;
        double? curFontSize = null;

        int spanStart = 0;

        void CommitSpan()
        {
            int len = sb.Length - spanStart;
            if (len > 0)
            {
                result.Spans.Add(new TextSpanInfo
                {
                    Start = spanStart,
                    Length = len,
                    FontWeight = curBold ? FontWeights.Black : FontWeights.Normal,
                    FontStyle = curItalic ? FontStyles.Italic : FontStyles.Normal,
                    ForegroundBrush = curFill ?? defaultFill,
                    Underline = curUnderline,
                    Strikethrough = curStrike,
                    FontSize = curFontSize
                });
            }
            spanStart = sb.Length;
        }

        while (i < s.Length)
        {
            // ASS override tag block: {...}
            if (s[i] == '{')
            {
                int close = s.IndexOf('}', i);
                if (close > i)
                {
                    string tagContent = s.Substring(i + 1, close - i - 1);
                    CommitSpan();
                    ProcessAssTag(
                        tagContent,
                        ref curBold,
                        ref curItalic,
                        ref curUnderline,
                        ref curStrike,
                        ref curFill,
                        ref curFontSize,
                        isBaseBold,
                        isBaseItalic,
                        defaultFill,
                        result);
                    i = close + 1;
                    continue;
                }
            }
            // HTML tag: <...>
            else if (s[i] == '<')
            {
                int close = s.IndexOf('>', i);
                if (close > i)
                {
                    string tagContent = s.Substring(i + 1, close - i - 1);
                    CommitSpan();
                    ProcessHtmlTag(
                        tagContent,
                        ref curBold,
                        ref curItalic,
                        ref curUnderline,
                        ref curStrike,
                        ref curFill,
                        isBaseBold,
                        isBaseItalic,
                        defaultFill,
                        result);
                    i = close + 1;
                    continue;
                }
            }

            sb.Append(s[i]);
            i++;
        }

        CommitSpan();
        result.CleanText = sb.ToString();
        return result;
    }

    private static void ProcessAssTag(
        string tag,
        ref bool curBold,
        ref bool curItalic,
        ref bool curUnderline,
        ref bool curStrike,
        ref Brush? curFill,
        ref double? curFontSize,
        bool isBaseBold,
        bool isBaseItalic,
        Brush? defaultFill,
        AssParseResult result)
    {
        string[] parts = tag.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawPart in parts)
        {
            string part = rawPart.Trim();

            // Bold
            if (part.StartsWith("b1", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("b700", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("b900", StringComparison.OrdinalIgnoreCase))
            {
                curBold = true;
            }
            else if (part.StartsWith("b0", StringComparison.OrdinalIgnoreCase) ||
                     part.StartsWith("b400", StringComparison.OrdinalIgnoreCase) ||
                     part.Equals("b", StringComparison.OrdinalIgnoreCase))
            {
                curBold = false;
            }
            // Italic
            else if (part.StartsWith("i1", StringComparison.OrdinalIgnoreCase))
            {
                curItalic = true;
            }
            else if (part.StartsWith("i0", StringComparison.OrdinalIgnoreCase))
            {
                curItalic = false;
            }
            // Underline
            else if (part.StartsWith("u1", StringComparison.OrdinalIgnoreCase))
            {
                curUnderline = true;
            }
            else if (part.StartsWith("u0", StringComparison.OrdinalIgnoreCase))
            {
                curUnderline = false;
            }
            // Strikeout
            else if (part.StartsWith("s1", StringComparison.OrdinalIgnoreCase))
            {
                curStrike = true;
            }
            else if (part.StartsWith("s0", StringComparison.OrdinalIgnoreCase))
            {
                curStrike = false;
            }
            // Primary Color: \c&HBBGGRR& or \1c&HBBGGRR& or reset \c / \1c
            else if (part.StartsWith("c&", StringComparison.OrdinalIgnoreCase) ||
                     part.StartsWith("c#", StringComparison.OrdinalIgnoreCase) ||
                     part.StartsWith("1c&", StringComparison.OrdinalIgnoreCase) ||
                     part.StartsWith("1c#", StringComparison.OrdinalIgnoreCase))
            {
                var color = ParseAssColor(part);
                if (color.HasValue)
                {
                    var b = new SolidColorBrush(color.Value);
                    b.Freeze();
                    curFill = b;
                }
            }
            else if (part.Equals("c", StringComparison.OrdinalIgnoreCase) ||
                     part.Equals("1c", StringComparison.OrdinalIgnoreCase))
            {
                // Reset to default fill color
                curFill = defaultFill;
            }
            // Border / Outline Color: \3c&HBBGGRR& or reset \3c
            else if (part.StartsWith("3c&", StringComparison.OrdinalIgnoreCase) ||
                     part.StartsWith("3c#", StringComparison.OrdinalIgnoreCase))
            {
                var color = ParseAssColor(part);
                if (color.HasValue)
                {
                    var b = new SolidColorBrush(color.Value);
                    b.Freeze();
                    result.OverrideStrokeBrush = b;
                }
            }
            else if (part.Equals("3c", StringComparison.OrdinalIgnoreCase))
            {
                result.OverrideStrokeBrush = null;
            }
            // Shadow Color: \4c&HBBGGRR& or reset \4c
            else if (part.StartsWith("4c&", StringComparison.OrdinalIgnoreCase) ||
                     part.StartsWith("4c#", StringComparison.OrdinalIgnoreCase))
            {
                var color = ParseAssColor(part);
                if (color.HasValue)
                {
                    var b = new SolidColorBrush(color.Value);
                    b.Freeze();
                    result.OverrideShadowBrush = b;
                }
            }
            else if (part.Equals("4c", StringComparison.OrdinalIgnoreCase))
            {
                result.OverrideShadowBrush = null;
            }
            // Font Size: \fs<number> or reset \fs
            else if (part.StartsWith("fs", StringComparison.OrdinalIgnoreCase))
            {
                string sizeStr = part.Substring(2).Trim();
                if (double.TryParse(sizeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double size) && size > 4)
                {
                    curFontSize = size;
                }
                else if (sizeStr.Length == 0)
                {
                    curFontSize = null; // reset to default
                }
            }
            // Alignment: \an1 to \an9
            else if (part.StartsWith("an", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(part.Substring(2).Trim(), out int an) && an >= 1 && an <= 9)
                {
                    result.Alignment = an;
                }
            }
            // Legacy SSA Alignment: \a1 to \a11
            else if (part.StartsWith("a", StringComparison.OrdinalIgnoreCase) && !part.StartsWith("alpha", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(part.Substring(1).Trim(), out int a))
                {
                    // Convert SSA \a to ASS \an
                    result.Alignment = a switch
                    {
                        1 => 1, 2 => 2, 3 => 3,
                        9 => 4, 10 => 5, 11 => 6,
                        5 => 7, 6 => 8, 7 => 9,
                        _ => 2
                    };
                }
            }
            // Reset: \r or \r<style>
            else if (part.StartsWith("r", StringComparison.OrdinalIgnoreCase))
            {
                curBold = isBaseBold;
                curItalic = isBaseItalic;
                curUnderline = false;
                curStrike = false;
                curFill = defaultFill;
                curFontSize = null;
            }
        }
    }

    private static void ProcessHtmlTag(
        string tag,
        ref bool curBold,
        ref bool curItalic,
        ref bool curUnderline,
        ref bool curStrike,
        ref Brush? curFill,
        bool isBaseBold,
        bool isBaseItalic,
        Brush? defaultFill,
        AssParseResult result)
    {
        string t = tag.Trim().ToLowerInvariant();

        if (t == "b") curBold = true;
        else if (t == "/b") curBold = isBaseBold;
        else if (t == "i") curItalic = true;
        else if (t == "/i") curItalic = isBaseItalic;
        else if (t == "u") curUnderline = true;
        else if (t == "/u") curUnderline = false;
        else if (t == "s" || t == "strike") curStrike = true;
        else if (t == "/s" || t == "/strike") curStrike = false;
        else if (t.StartsWith("font") && t.Contains("color="))
        {
            int cIdx = t.IndexOf("color=");
            string val = t.Substring(cIdx + 6).Trim('\'', '"', ' ');
            var color = ParseHtmlColor(val);
            if (color.HasValue)
            {
                var brush = new SolidColorBrush(color.Value);
                brush.Freeze();
                curFill = brush;
            }
        }
        else if (t == "/font")
        {
            curFill = defaultFill;
        }
    }

    public static Color? ParseAssColor(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string clean = raw.Trim();

        // Strip leading backslash if present
        if (clean.StartsWith("\\")) clean = clean.Substring(1);

        // Strip prefixes: 1c, 2c, 3c, 4c, c
        if (clean.StartsWith("1c", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("2c", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("3c", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("4c", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(2);
        }
        else if (clean.StartsWith("c", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(1);
        }

        clean = clean.Trim();
        if (clean.StartsWith("&H", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(2);
        else if (clean.StartsWith("&", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(1);
        else if (clean.StartsWith("#")) clean = clean.Substring(1);
        clean = clean.TrimEnd('&').Trim();

        if (clean.Length == 0) return null;

        if (uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint val))
        {
            if (clean.Length <= 6)
            {
                // ASS format is 0xBBGGRR
                byte b = (byte)((val >> 16) & 0xFF);
                byte g = (byte)((val >> 8) & 0xFF);
                byte r = (byte)(val & 0xFF);
                return Color.FromArgb(255, r, g, b);
            }
            else
            {
                // ASS format with alpha 0xAABBGGRR (where 00 is opaque, FF is transparent)
                byte a = (byte)(255 - ((val >> 24) & 0xFF));
                byte b = (byte)((val >> 16) & 0xFF);
                byte g = (byte)((val >> 8) & 0xFF);
                byte r = (byte)(val & 0xFF);
                return Color.FromArgb(a, r, g, b);
            }
        }
        return null;
    }

    public static Color? ParseHtmlColor(string colorNameOrHex)
    {
        try
        {
            if (colorNameOrHex.StartsWith("#"))
            {
                return (Color)ColorConverter.ConvertFromString(colorNameOrHex);
            }
            return colorNameOrHex.ToLowerInvariant() switch
            {
                "yellow" => Colors.Yellow,
                "cyan" => Colors.Cyan,
                "red" => Colors.Red,
                "lime" or "green" => Colors.Lime,
                "blue" => Colors.DeepSkyBlue,
                "white" => Colors.White,
                "black" => Colors.Black,
                "magenta" or "pink" => Colors.Magenta,
                "orange" => Colors.Orange,
                _ => (Color)ColorConverter.ConvertFromString(colorNameOrHex)
            };
        }
        catch
        {
            return null;
        }
    }
}
