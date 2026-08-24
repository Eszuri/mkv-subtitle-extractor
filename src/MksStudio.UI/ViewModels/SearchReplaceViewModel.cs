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
    public Action<SubtitleCue>? OnMatchFound { get; set; }
    public Action? OnDataModified { get; set; }

    private int _lastFoundIndex = -1;

    [RelayCommand]
    private void FindNext()
    {
        if (string.IsNullOrEmpty(SearchText) || GetTargetCues == null) return;

        var cues = GetTargetCues().ToList();
        var matches = SearchReplaceService.Find(cues, SearchText, MatchCase, UseRegex);

        if (matches.Count == 0)
        {
            StatusText = "No matches found.";
            _lastFoundIndex = -1;
            return;
        }

        _lastFoundIndex = (_lastFoundIndex + 1) % matches.Count;
        var currentMatch = matches[_lastFoundIndex];
        StatusText = $"Match {_lastFoundIndex + 1} of {matches.Count}";
        OnMatchFound?.Invoke(currentMatch.Cue);
    }

    [RelayCommand]
    private void ReplaceAll()
    {
        if (string.IsNullOrEmpty(SearchText) || GetTargetCues == null) return;

        var cues = GetTargetCues().ToList();
        int count = SearchReplaceService.ReplaceAll(cues, SearchText, ReplaceText, MatchCase, UseRegex);
        StatusText = $"Replaced {count} occurrence(s).";
        OnDataModified?.Invoke();
    }
}
