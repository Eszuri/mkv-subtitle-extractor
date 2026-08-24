using System.Text.RegularExpressions;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.Core.Operations;

/// <summary>
/// Service for finding and replacing text within subtitle cues.
/// </summary>
public static class SearchReplaceService
{
    public record SearchMatch(SubtitleCue Cue, int MatchIndex, int MatchLength);

    public static List<SearchMatch> Find(IEnumerable<SubtitleCue> cues, string query, bool matchCase = false, bool useRegex = false)
    {
        var matches = new List<SearchMatch>();
        if (string.IsNullOrEmpty(query)) return matches;

        if (useRegex)
        {
            var options = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            try
            {
                var regex = new Regex(query, options);
                foreach (var cue in cues)
                {
                    var m = regex.Match(cue.RawText);
                    while (m.Success)
                    {
                        matches.Add(new SearchMatch(cue, m.Index, m.Length));
                        m = m.NextMatch();
                    }
                }
            }
            catch (ArgumentException)
            {
                // Invalid regex
            }
        }
        else
        {
            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            foreach (var cue in cues)
            {
                int index = 0;
                while ((index = cue.RawText.IndexOf(query, index, comparison)) >= 0)
                {
                    matches.Add(new SearchMatch(cue, index, query.Length));
                    index += query.Length;
                }
            }
        }

        return matches;
    }

    public static int ReplaceAll(IEnumerable<SubtitleCue> cues, string search, string replacement, bool matchCase = false, bool useRegex = false)
    {
        if (string.IsNullOrEmpty(search)) return 0;
        replacement ??= string.Empty;
        int count = 0;

        if (useRegex)
        {
            var options = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            try
            {
                var regex = new Regex(search, options);
                foreach (var cue in cues)
                {
                    if (regex.IsMatch(cue.RawText))
                    {
                        var matches = regex.Matches(cue.RawText);
                        count += matches.Count;
                        cue.RawText = regex.Replace(cue.RawText, replacement);
                    }
                }
            }
            catch (ArgumentException)
            {
                return 0;
            }
        }
        else
        {
            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            foreach (var cue in cues)
            {
                if (cue.RawText.Contains(search, comparison))
                {
                    int index = 0;
                    while ((index = cue.RawText.IndexOf(search, index, comparison)) >= 0)
                    {
                        count++;
                        cue.RawText = cue.RawText.Remove(index, search.Length).Insert(index, replacement);
                        index += replacement.Length;
                    }
                }
            }
        }

        return count;
    }
}
