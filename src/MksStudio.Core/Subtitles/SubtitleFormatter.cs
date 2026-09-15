using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MksStudio.Core.Subtitles;

/// <summary>
/// Professional subtitle text formatting engine supporting:
/// - Smart selection-aware styling (Bold, Italic, Underline, Strikeout)
/// - Accurate ASS override tags ({\b1}..{\b0}, {\i1}..{\i0}, {\c&HBBGGRR&}) vs SRT/HTML (<b>, <i>, <font>)
/// - Intelligent toggle on/off for existing formatting
/// - Anti-stacking color replacement and cleanup
/// - ASS subtitle alignment positioning ({\an1} .. {\an9})
/// - Text case transformations (UPPERCASE, lowercase, Title Case, Sentence case) preserving tags
/// - One-click clean strip formatting
/// </summary>
public static class SubtitleFormatter
{
    public static (string newText, int newSelStart, int newSelLen) ApplyTag(
        string fullText,
        int selStart,
        int selLen,
        string tagType,
        bool isAss)
    {
        fullText ??= string.Empty;

        // Hard Linebreak
        if (tagType == "n")
        {
            string br = isAss ? "\\N" : "\n";
            if (selStart >= 0 && selLen >= 0 && selStart + selLen <= fullText.Length)
            {
                string res = fullText.Substring(0, selStart) + br + fullText.Substring(selStart + selLen);
                return (res, selStart + br.Length, 0);
            }
            return (fullText + br, (fullText + br).Length, 0);
        }

        // Hard Space
        if (tagType == "h")
        {
            string sp = isAss ? "\\h" : " ";
            if (selStart >= 0 && selLen >= 0 && selStart + selLen <= fullText.Length)
            {
                string res = fullText.Substring(0, selStart) + sp + fullText.Substring(selStart + selLen);
                return (res, selStart + sp.Length, 0);
            }
            return (fullText + sp, (fullText + sp).Length, 0);
        }

        string openTag = isAss ? $"\\{tagType}1" : $"<{tagType}>";
        string closeTag = isAss ? $"\\{tagType}0" : $"</{tagType}>";
        string assOpenBlock = $"{{{openTag}}}";
        string assCloseBlock = $"{{{closeTag}}}";

        string actualOpen = isAss ? assOpenBlock : openTag;
        string actualClose = isAss ? assCloseBlock : closeTag;

        // CASE 1: Selection is highlighted
        if (selLen > 0 && selStart >= 0 && selStart + selLen <= fullText.Length)
        {
            string selected = fullText.Substring(selStart, selLen);

            // Check if selected itself starts with open and ends with close (Toggle OFF inside selection)
            if (selected.StartsWith(actualOpen, StringComparison.OrdinalIgnoreCase) &&
                selected.EndsWith(actualClose, StringComparison.OrdinalIgnoreCase))
            {
                string unwrap = selected.Substring(actualOpen.Length, selected.Length - actualOpen.Length - actualClose.Length);
                string res = fullText.Substring(0, selStart) + unwrap + fullText.Substring(selStart + selLen);
                return (res, selStart, unwrap.Length);
            }

            // Check if selection is immediately wrapped by open and close tags outside selection (Toggle OFF outside)
            if (selStart >= actualOpen.Length &&
                selStart + selLen + actualClose.Length <= fullText.Length &&
                fullText.Substring(selStart - actualOpen.Length, actualOpen.Length).Equals(actualOpen, StringComparison.OrdinalIgnoreCase) &&
                fullText.Substring(selStart + selLen, actualClose.Length).Equals(actualClose, StringComparison.OrdinalIgnoreCase))
            {
                string res = fullText.Substring(0, selStart - actualOpen.Length) +
                             selected +
                             fullText.Substring(selStart + selLen + actualClose.Length);
                return (res, selStart - actualOpen.Length, selected.Length);
            }

            // Otherwise: Wrap selection (Toggle ON)
            string wrapped = actualOpen + selected + actualClose;
            string newFull = fullText.Substring(0, selStart) + wrapped + fullText.Substring(selStart + selLen);
            return (newFull, selStart, wrapped.Length);
        }

        // CASE 2: No selection -> Apply or toggle on entire text
        if (string.IsNullOrEmpty(fullText))
        {
            string wrapped = actualOpen + actualClose;
            return (wrapped, actualOpen.Length, 0);
        }

        // Check if fullText is already wrapped in tags (Toggle OFF)
        if (fullText.StartsWith(actualOpen, StringComparison.OrdinalIgnoreCase) &&
            fullText.EndsWith(actualClose, StringComparison.OrdinalIgnoreCase))
        {
            string unwrap = fullText.Substring(actualOpen.Length, fullText.Length - actualOpen.Length - actualClose.Length);
            return (unwrap, 0, unwrap.Length);
        }

        // Wrap fullText (Toggle ON)
        string fullWrapped = actualOpen + fullText + actualClose;
        return (fullWrapped, 0, fullWrapped.Length);
    }

    public static (string newText, int newSelStart, int newSelLen) ApplyColor(
        string fullText,
        int selStart,
        int selLen,
        string hexRgb, // e.g. "FFFF00"
        bool isAss)
    {
        fullText ??= string.Empty;
        string cleanHex = hexRgb.Trim('#').Trim();
        if (cleanHex.Length < 6) cleanHex = cleanHex.PadRight(6, '0');

        // Extract R, G, B
        string r = cleanHex.Substring(0, 2);
        string g = cleanHex.Substring(2, 2);
        string b = cleanHex.Substring(4, 2);

        // In ASS color tag: \c&HBBGGRR&
        string assBgr = $"{b}{g}{r}".ToUpperInvariant();
        string openTag = isAss ? $"{{\\c&H{assBgr}&}}" : $"<font color=\"#{cleanHex.ToUpperInvariant()}\">";
        string closeTag = isAss ? "{\\c}" : "</font>";

        string assColorOpenPattern = @"^\{\\(c|1c)&H[0-9A-Fa-f]+&\}";
        string assColorClosePattern = @"\{\\(c|1c)\}$";
        string htmlColorOpenPattern = @"^<font\s+color=""[^""]*"">";
        string htmlColorClosePattern = @"</font>$";

        string openPattern = isAss ? assColorOpenPattern : htmlColorOpenPattern;
        string closePattern = isAss ? assColorClosePattern : htmlColorClosePattern;

        // Helper to remove any duplicate/stacked color tags
        string CleanStacked(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            if (isAss)
            {
                // Collapse consecutive opening color tags: keep only the last one
                string s = Regex.Replace(input, @"(?:\{\\(?:c|1c)&H[0-9A-Fa-f]+&\}){2,}", m =>
                {
                    var last = Regex.Match(m.Value, @"\{\\(?:c|1c)&H[0-9A-Fa-f]+&\}$");
                    return last.Success ? last.Value : m.Value;
                });
                // Collapse consecutive closing color tags: keep only one {\c}
                s = Regex.Replace(s, @"(?:\{\\(?:c|1c)\}){2,}", "{\\c}");
                return s;
            }
            else
            {
                string s = Regex.Replace(input, @"(?:<font\s+color=""[^""]*"">){2,}", m =>
                {
                    var last = Regex.Match(m.Value, @"<font\s+color=""[^""]*"">$");
                    return last.Success ? last.Value : m.Value;
                });
                s = Regex.Replace(s, @"(?:</font>){2,}", "</font>");
                return s;
            }
        }

        // CASE 1: Selection is highlighted
        if (selLen > 0 && selStart >= 0 && selStart + selLen <= fullText.Length)
        {
            string selected = fullText.Substring(selStart, selLen);

            // Check if selected itself starts with a color open and ends with a color close
            var openMatch = Regex.Match(selected, openPattern, RegexOptions.IgnoreCase);
            var closeMatch = Regex.Match(selected, closePattern, RegexOptions.IgnoreCase);

            if (openMatch.Success && closeMatch.Success)
            {
                string unwrap = selected.Substring(openMatch.Length, selected.Length - openMatch.Length - closeMatch.Length);
                // If same color -> Toggle OFF!
                if (openMatch.Value.Equals(openTag, StringComparison.OrdinalIgnoreCase))
                {
                    string res = fullText.Substring(0, selStart) + unwrap + fullText.Substring(selStart + selLen);
                    return (CleanStacked(res), selStart, unwrap.Length);
                }
                else
                {
                    // Different color -> REPLACE! (Never stack)
                    string replaced = openTag + unwrap + closeTag;
                    string res = fullText.Substring(0, selStart) + replaced + fullText.Substring(selStart + selLen);
                    return (CleanStacked(res), selStart, replaced.Length);
                }
            }

            // Check if selection is immediately flanked by color tags outside the selection
            if (isAss)
            {
                var preOpen = Regex.Match(fullText.Substring(0, selStart), @"\{\\(?:c|1c)&H[0-9A-Fa-f]+&\}$", RegexOptions.IgnoreCase);
                var postClose = Regex.Match(fullText.Substring(selStart + selLen), @"^\{\\(?:c|1c)\}", RegexOptions.IgnoreCase);

                if (preOpen.Success && postClose.Success)
                {
                    if (preOpen.Value.Equals(openTag, StringComparison.OrdinalIgnoreCase))
                    {
                        // Toggle OFF
                        string res = fullText.Substring(0, selStart - preOpen.Length) + selected + fullText.Substring(selStart + selLen + postClose.Length);
                        return (CleanStacked(res), selStart - preOpen.Length, selected.Length);
                    }
                    else
                    {
                        // REPLACE outside tags with new color
                        string res = fullText.Substring(0, selStart - preOpen.Length) + openTag + selected + closeTag + fullText.Substring(selStart + selLen + postClose.Length);
                        return (CleanStacked(res), selStart - preOpen.Length, openTag.Length + selected.Length + closeTag.Length);
                    }
                }
            }

            // Strip any internal color tags from selection so they don't corrupt the new color
            string cleanSel = selected;
            if (isAss)
            {
                cleanSel = Regex.Replace(cleanSel, @"\{\\(?:c|1c)&H[0-9A-Fa-f]+&\}", "");
                cleanSel = Regex.Replace(cleanSel, @"\{\\(?:c|1c)\}", "");
            }
            else
            {
                cleanSel = Regex.Replace(cleanSel, @"<font\s+color=""[^""]*"">", "");
                cleanSel = Regex.Replace(cleanSel, @"</font>", "");
            }

            string wrapped = openTag + cleanSel + closeTag;
            string newFull = fullText.Substring(0, selStart) + wrapped + fullText.Substring(selStart + selLen);
            return (CleanStacked(newFull), selStart, wrapped.Length);
        }

        // CASE 2: Whole line (No selection)
        if (string.IsNullOrEmpty(fullText))
        {
            return (openTag + closeTag, openTag.Length, 0);
        }

        // Check if fullText already starts with color open and ends with color close (or multiple stacked ones)
        if (isAss)
        {
            var leadingColor = Regex.Match(fullText, @"^(?:\{\\(?:c|1c)&H[0-9A-Fa-f]+&\})+", RegexOptions.IgnoreCase);
            var trailingClose = Regex.Match(fullText, @"(?:\{\\(?:c|1c)\})+$", RegexOptions.IgnoreCase);

            if (leadingColor.Success && trailingClose.Success)
            {
                var lastTag = Regex.Match(leadingColor.Value, @"\{\\(?:c|1c)&H[0-9A-Fa-f]+&\}$", RegexOptions.IgnoreCase);
                string inner = fullText.Substring(leadingColor.Length, fullText.Length - leadingColor.Length - trailingClose.Length);

                if (lastTag.Success && lastTag.Value.Equals(openTag, StringComparison.OrdinalIgnoreCase))
                {
                    // Toggle OFF
                    return (inner, 0, inner.Length);
                }
                else
                {
                    // REPLACE color
                    string replaced = openTag + inner + closeTag;
                    return (CleanStacked(replaced), 0, replaced.Length);
                }
            }
        }
        else
        {
            var openM = Regex.Match(fullText, htmlColorOpenPattern, RegexOptions.IgnoreCase);
            var closeM = Regex.Match(fullText, htmlColorClosePattern, RegexOptions.IgnoreCase);
            if (openM.Success && closeM.Success)
            {
                string inner = fullText.Substring(openM.Length, fullText.Length - openM.Length - closeM.Length);
                if (openM.Value.Equals(openTag, StringComparison.OrdinalIgnoreCase))
                {
                    return (inner, 0, inner.Length);
                }
                return (openTag + inner + closeTag, 0, (openTag + inner + closeTag).Length);
            }
        }

        // Regular wrap
        string fullWrapped = openTag + fullText + closeTag;
        return (CleanStacked(fullWrapped), 0, fullWrapped.Length);
    }

    public static string ApplyAlignment(string fullText, int alignNumber, bool isAss)
    {
        fullText ??= string.Empty;
        if (!isAss) return fullText; // \an is ASS specific

        string alignTag = $"\\an{alignNumber}";

        // If text already has {\anX} at the beginning, replace or remove it
        var match = Regex.Match(fullText, @"^\{\\an([1-9])\}");
        if (match.Success)
        {
            if (int.Parse(match.Groups[1].Value) == alignNumber)
            {
                // Toggle off (remove alignment override)
                return fullText.Substring(match.Length);
            }
            // Replace with new alignment
            return $"{{{alignTag}}}" + fullText.Substring(match.Length);
        }

        return $"{{{alignTag}}}" + fullText;
    }

    public static (string newText, int newSelStart, int newSelLen) ChangeCase(
        string fullText,
        int selStart,
        int selLen,
        string mode)
    {
        fullText ??= string.Empty;

        string TransformWord(string s) => mode.ToLowerInvariant() switch
        {
            "upper" => s.ToUpperInvariant(),
            "lower" => s.ToLowerInvariant(),
            "title" => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower()),
            "sentence" => ToSentenceCase(s),
            _ => s
        };

        // Transform only dialogue text, protecting ASS tags {...}, HTML tags <...>, and \N, \h, \n
        string TransformProtected(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var pattern = @"(\{.*?\}|<.*?>|\\[Nnh])";
            var tokens = Regex.Split(input, pattern);
            var sb = new StringBuilder(input.Length);

            foreach (var token in tokens)
            {
                if (token.Length == 0) continue;
                if ((token.StartsWith("{") && token.EndsWith("}")) ||
                    (token.StartsWith("<") && token.EndsWith(">")) ||
                    token.Equals("\\N", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("\\h", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("\\n", StringComparison.OrdinalIgnoreCase))
                {
                    // Preserve tag as-is with normalized ASS escape casing
                    if (token.Equals("\\N", StringComparison.OrdinalIgnoreCase)) sb.Append("\\N");
                    else if (token.Equals("\\h", StringComparison.OrdinalIgnoreCase)) sb.Append("\\h");
                    else if (token.Equals("\\n", StringComparison.OrdinalIgnoreCase)) sb.Append("\\n");
                    else sb.Append(token);
                }
                else
                {
                    sb.Append(TransformWord(token));
                }
            }

            return sb.ToString();
        }

        if (selLen > 0 && selStart >= 0 && selStart + selLen <= fullText.Length)
        {
            string selected = fullText.Substring(selStart, selLen);
            string transformed = TransformProtected(selected);
            string res = fullText.Substring(0, selStart) + transformed + fullText.Substring(selStart + selLen);
            return (res, selStart, transformed.Length);
        }

        string fullTransformed = TransformProtected(fullText);
        return (fullTransformed, 0, fullTransformed.Length);
    }

    public static (string newText, int newSelStart, int newSelLen) StripFormatting(
        string fullText,
        int selStart,
        int selLen)
    {
        fullText ??= string.Empty;

        string Clean(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            // Remove ASS override tags {...}
            string s = Regex.Replace(input, @"\{[^}]*\}", string.Empty);
            // Remove HTML tags <...>
            s = Regex.Replace(s, @"<[^>]*>", string.Empty);
            return s;
        }

        if (selLen > 0 && selStart >= 0 && selStart + selLen <= fullText.Length)
        {
            string selected = fullText.Substring(selStart, selLen);
            string cleaned = Clean(selected);
            string res = fullText.Substring(0, selStart) + cleaned + fullText.Substring(selStart + selLen);
            return (res, selStart, cleaned.Length);
        }

        string fullCleaned = Clean(fullText);
        return (fullCleaned, 0, fullCleaned.Length);
    }

    private static string ToSentenceCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        var sb = new StringBuilder(input.Length);
        bool capitalizeNext = true;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsLetter(c))
            {
                if (capitalizeNext)
                {
                    sb.Append(char.ToUpper(c, CultureInfo.CurrentCulture));
                    capitalizeNext = false;
                }
                else
                {
                    sb.Append(char.ToLower(c, CultureInfo.CurrentCulture));
                }
            }
            else
            {
                sb.Append(c);
                if (c == '.' || c == '!' || c == '?' || c == '\n')
                {
                    capitalizeNext = true;
                }
            }
        }
        return sb.ToString();
    }
}
