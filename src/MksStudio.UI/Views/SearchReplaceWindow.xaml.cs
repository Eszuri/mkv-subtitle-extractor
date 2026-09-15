using System.Windows;
using MksStudio.UI.ViewModels;

namespace MksStudio.UI.Views;

public partial class SearchReplaceWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel _mainVm;
    private readonly SearchReplaceViewModel _vm;

    public SearchReplaceWindow(MainViewModel mainVm, MainWindow? mainWindow = null)
    {
        InitializeComponent();
        _mainVm = mainVm;
        if (mainWindow != null)
        {
            Owner = mainWindow;
        }

        _vm = new SearchReplaceViewModel
        {
            GetTargetCues = () => _mainVm.SelectedTrack?.Model.Subtitles.Cues ?? [],
            GetCurrentCue = () => _mainVm.SelectedCue?.Model,
            OnMatchFound = match =>
            {
                if (_mainVm.SelectedTrack != null)
                {
                    var cueVm = _mainVm.SelectedTrack.Cues.FirstOrDefault(c => c.Model == match.Cue);
                    if (cueVm != null)
                    {
                        var targetWindow = Owner as MainWindow ?? Application.Current.MainWindow as MainWindow;
                        if (targetWindow != null)
                        {
                            targetWindow.ScrollToCue(cueVm, match.MatchIndex, match.MatchLength);
                        }
                        else
                        {
                            _mainVm.SelectedCue = cueVm;
                        }
                    }
                }
            },
            OnDataModified = () =>
            {
                _mainVm.SelectedTrack?.RefreshCues();
                _mainVm.IsModified = true;
                _mainVm.RefreshAllViews();
            }
        };

        DataContext = _vm;
    }

    private void OnFindNextClicked(object sender, RoutedEventArgs e)
    {
        _vm.FindNextCommand.Execute(null);
    }

    private void OnReplaceNextClicked(object sender, RoutedEventArgs e)
    {
        _vm.ReplaceNextCommand.Execute(null);
    }

    private void OnReplaceAllClicked(object sender, RoutedEventArgs e)
    {
        _vm.ReplaceAllCommand.Execute(null);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
