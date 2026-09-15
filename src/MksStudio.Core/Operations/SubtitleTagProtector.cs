using System.Text.RegularExpressions;

namespace MksStudio.Core.Operations;

/// <summary>
/// Utility to protect subtitle formatting tags (ASS override blocks, ASS linebreaks, HTML tags)
/// from being corrupted or translated during machine translation, and restoring them seamlessly.
/// </summary>
public class SubtitleTagProtector
{
    // Matches:
    // 1. ASS Override tags: {\...}
    // 2. ASS Escaped Linebreaks: \N, \n, \h
    // 3. HTML/SRT tags: <i>, </b>, <font ...>, etc.
    private static readonly Regex TagRegex = new(
        @"(\{[^}]+\})|(\\[Nnh])|(<[^>]+>)",
        RegexOptions.Compiled);

    // Matches placeholder tokens: ⟦T0⟧, ⟦T1⟧, [[T0]], [T0], {T0}, ⦅T0⦆ with optional whitespace and case insensitivity
    private static readonly Regex TokenRestoreRegex = new(
        @"(?:⟦\s*T(\d+)\s*⟧|\[\[\s*T(\d+)\s*\]\]|\[\s*T(\d+)\s*\]|\{\s*T(\d+)\s*\}|⦅\s*T(\d+)\s*⦆)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Protects all tags in raw subtitle text by replacing them with indexed tokens.
    /// </summary>
    public static (string ProtectedText, List<string> ExtractedTags) Protect(string rawText)
    {
        if (string.IsNullOrEmpty(rawText))
            return (string.Empty, new List<string>());

        var tags = new List<string>();
        string protectedText = TagRegex.Replace(rawText, match =>
        {
            int index = tags.Count;
            tags.Add(match.Value);
            return $"⟦T{index}⟧";
        });

        return (protectedText, tags);
    }

    /// <summary>
    /// Restores previously extracted tags back into the translated text.
    /// </summary>
    public static string Restore(string translatedText, IReadOnlyList<string> extractedTags)
    {
        if (string.IsNullOrEmpty(translatedText) || extractedTags == null || extractedTags.Count == 0)
            return translatedText ?? string.Empty;

        return TokenRestoreRegex.Replace(translatedText, match =>
        {
            for (int i = 1; i < match.Groups.Count; i++)
            {
                if (!string.IsNullOrEmpty(match.Groups[i].Value) && int.TryParse(match.Groups[i].Value, out int index))
                {
                    if (index >= 0 && index < extractedTags.Count)
                    {
                        return extractedTags[index];
                    }
                }
            }

            return match.Value;
        });
    }
}
