using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MksStudio.Core.Operations;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.UI.ViewModels;

public partial class SearchReplaceViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _replaceText = string.Empty;

    [ObservableProperty]
    private bool _matchCase = false;

    [ObservableProperty]
    private bool _useRegex = false;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public Func<IEnumerable<SubtitleCue>>? GetTargetCues { get; set; }
    public Func<SubtitleCue?>? GetCurrentCue { get; set; }
    public Action<SearchReplaceService.SearchMatch>? OnMatchFound { get; set; }
    public Action? OnDataModified { get; set; }

    private int _lastFoundIndex = -1;

    partial void OnSearchTextChanged(string value) => _lastFoundIndex = -1;
    partial void OnMatchCaseChanged(bool value) => _lastFoundIndex = -1;
    partial void OnUseRegexChanged(bool value) => _lastFoundIndex = -1;

    [RelayCommand]
    public void FindNext()
    {
        if (string.IsNullOrEmpty(SearchText) || GetTargetCues == null)
        {
            StatusText = "Please enter search text.";
            return;
        }

        var cues = GetTargetCues().ToList();
        if (cues.Count == 0)
        {
            StatusText = "No subtitle cues to search.";
            return;
        }

        var matches = SearchReplaceService.Find(cues, SearchText, MatchCase, UseRegex);

        if (matches.Count == 0)
        {
            StatusText = "Text not found.";
            _lastFoundIndex = -1;
            return;
        }

        if (_lastFoundIndex == -1 && GetCurrentCue != null)
        {
            var current = GetCurrentCue();
            if (current != null)
            {
                int currentCueIndex = cues.IndexOf(current);
                if (currentCueIndex >= 0)
                {
                    // Find first match at or after current cue
                    int nextIdx = matches.FindIndex(m => cues.IndexOf(m.Cue) >= currentCueIndex);
                    if (nextIdx >= 0)
                    {
                        _lastFoundIndex = nextIdx;
                    }
                    else
                    {
                        _lastFoundIndex = 0;
                    }
                }
                else
                {
                    _lastFoundIndex = 0;
                }
            }
            else
            {
                _lastFoundIndex = 0;
            }
        }
        else
        {
            _lastFoundIndex = (_lastFoundIndex + 1) % matches.Count;
        }

        var currentMatch = matches[_lastFoundIndex];
        StatusText = $"Match {_lastFoundIndex + 1} of {matches.Count}";
        OnMatchFound?.Invoke(currentMatch);
    }

    [RelayCommand]
    public void ReplaceNext()
    {
        if (string.IsNullOrEmpty(SearchText) || GetTargetCues == null) return;
        var cues = GetTargetCues().ToList();
        var matches = SearchReplaceService.Find(cues, SearchText, MatchCase, UseRegex);
        if (matches.Count == 0)
        {
            StatusText = "Text not found.";
            _lastFoundIndex = -1;
            return;
        }

        if (_lastFoundIndex < 0 || _lastFoundIndex >= matches.Count)
        {
            FindNext();
            return;
        }

        var match = matches[_lastFoundIndex];
        var replacement = ReplaceText ?? string.Empty;

        if (match.MatchIndex >= 0 && match.MatchIndex + match.MatchLength <= match.Cue.RawText.Length)
        {
            match.Cue.RawText = match.Cue.RawText.Remove(match.MatchIndex, match.MatchLength).Insert(match.MatchIndex, replacement);
            StatusText = "1 occurrence replaced.";
            OnDataModified?.Invoke();

            _lastFoundIndex--;
            FindNext();
        }
    }

    [RelayCommand]
    public void ReplaceAll()
    {
        if (string.IsNullOrEmpty(SearchText) || GetTargetCues == null) return;

        var cues = GetTargetCues().ToList();
        int count = SearchReplaceService.ReplaceAll(cues, SearchText, ReplaceText, MatchCase, UseRegex);
        StatusText = count > 0 ? $"Successfully replaced {count} occurrence(s)." : "Text not found.";
        _lastFoundIndex = -1;
        OnDataModified?.Invoke();
    }
}
