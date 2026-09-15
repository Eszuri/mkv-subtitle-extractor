using System.Windows;
using MksStudio.Core.Subtitles.Models;
using MksStudio.UI.ViewModels;

namespace MksStudio.UI.Views;

public partial class TranslationWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly TranslationViewModel _viewModel;
    private readonly TrackItemViewModel _activeTrack;
    private readonly IList<CueItemViewModel> _selectedCues;

    public TranslationWindow(
        TranslationViewModel viewModel,
        TrackItemViewModel activeTrack,
        IList<CueItemViewModel>? selectedCues = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _activeTrack = activeTrack;
        _selectedCues = selectedCues ?? new List<CueItemViewModel>();

        _viewModel.TotalCuesCount = _activeTrack.Cues.Count;
        _viewModel.SelectedCuesCount = _selectedCues.Count;

        _viewModel.OnCloseRequested = () =>
        {
            Dispatcher.Invoke(() =>
            {
                DialogResult = true;
                Close();
            });
        };

        DataContext = _viewModel;
    }

    private void OnStartTranslationClicked(object sender, RoutedEventArgs e)
    {
        // Prepare cues to translate based on scope
        var cuesToTranslate = new List<SubtitleCue>();

        if (_viewModel.SelectedScopeIndex == 1 && _selectedCues.Count > 0)
        {
            // Selected cues only
            foreach (var cueVm in _selectedCues)
            {
                cuesToTranslate.Add(cueVm.Model);
            }
        }
        else
        {
            // Entire track
            foreach (var cueVm in _activeTrack.Cues)
            {
                cuesToTranslate.Add(cueVm.Model);
            }
        }

        if (cuesToTranslate.Count == 0)
        {
            MessageBox.Show("Tidak ada baris subtitle yang dapat diterjemahkan.", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _ = _viewModel.StartTranslationAsync(cuesToTranslate);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        _viewModel.Close();
    }
}
