using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MksStudio.Core.Operations;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.UI.ViewModels;

public partial class TimeShiftViewModel : ObservableObject
{
    [ObservableProperty]
    private long _offsetMilliseconds = 1000;

    [ObservableProperty]
    private double _sourceFps = 23.976;

    [ObservableProperty]
    private double _targetFps = 25.000;

    [ObservableProperty]
    private int _selectedScopeIndex = 0; // 0 = Current Track, 1 = Selected Cues Only, 2 = All Tracks

    public Action? OnApplied { get; set; }
    public Action? OnCloseRequested { get; set; }

    [RelayCommand]
    private void ApplyShift(IEnumerable<SubtitleCue> targetCues)
    {
        TimeShiftService.Shift(targetCues, OffsetMilliseconds);
        OnApplied?.Invoke();
    }

    [RelayCommand]
    private void ApplyFpsConvert(IEnumerable<SubtitleCue> targetCues)
    {
        TimeShiftService.ConvertFrameRate(targetCues, SourceFps, TargetFps);
        OnApplied?.Invoke();
    }

    [RelayCommand]
    private void Close()
    {
        OnCloseRequested?.Invoke();
    }
}
