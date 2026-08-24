using System.Windows;
using MksStudio.UI.ViewModels;

namespace MksStudio.UI.Views;

public partial class TimeShiftWindow : Window
{
    private readonly MainViewModel _mainVm;
    private readonly TimeShiftViewModel _vm;

    public TimeShiftWindow(MainViewModel mainVm)
    {
        InitializeComponent();
        _mainVm = mainVm;
        _vm = new TimeShiftViewModel();
        DataContext = _vm;
    }

    private void OnApplyShiftClicked(object sender, RoutedEventArgs e)
    {
        if (_mainVm.SelectedTrack == null || _mainVm.SelectedTrack.Cues.Count == 0)
        {
            MessageBox.Show("No cues available to shift.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var cues = _mainVm.SelectedTrack.Model.Subtitles.Cues;
        _vm.ApplyShiftCommand.Execute(cues);
        _mainVm.SelectedTrack.RefreshCues();
        _mainVm.IsModified = true;
        MessageBox.Show($"Applied {_vm.OffsetMilliseconds}ms shift to '{_mainVm.SelectedTrack.Name}'.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnApplyFpsClicked(object sender, RoutedEventArgs e)
    {
        if (_mainVm.SelectedTrack == null || _mainVm.SelectedTrack.Cues.Count == 0)
        {
            MessageBox.Show("No cues available to convert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var cues = _mainVm.SelectedTrack.Model.Subtitles.Cues;
        _vm.ApplyFpsConvertCommand.Execute(cues);
        _mainVm.SelectedTrack.RefreshCues();
        _mainVm.IsModified = true;
        MessageBox.Show($"Converted frame rate from {_vm.SourceFps} to {_vm.TargetFps} fps.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
