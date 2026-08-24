using System.Windows;
using MksStudio.UI.ViewModels;

namespace MksStudio.UI.Views;

public partial class SearchReplaceWindow : Window
{
    private readonly MainViewModel _mainVm;
    private readonly SearchReplaceViewModel _vm;

    public SearchReplaceWindow(MainViewModel mainVm)
    {
        InitializeComponent();
        _mainVm = mainVm;
        _vm = new SearchReplaceViewModel
        {
            GetTargetCues = () => _mainVm.SelectedTrack?.Model.Subtitles.Cues ?? [],
            OnMatchFound = cue =>
            {
                if (_mainVm.SelectedTrack != null)
                {
                    var vm = _mainVm.SelectedTrack.Cues.FirstOrDefault(c => c.Model == cue);
                    if (vm != null)
                    {
                        _mainVm.SelectedCue = vm;
                    }
                }
            },
            OnDataModified = () =>
            {
                _mainVm.SelectedTrack?.RefreshCues();
                _mainVm.IsModified = true;
            }
        };

        DataContext = _vm;
    }

    private void OnFindNextClicked(object sender, RoutedEventArgs e)
    {
        _vm.FindNextCommand.Execute(null);
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
