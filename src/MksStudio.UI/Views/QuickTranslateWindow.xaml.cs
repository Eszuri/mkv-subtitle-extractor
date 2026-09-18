using MksStudio.Core.Subtitles.Models;
using MksStudio.UI.ViewModels;
using Wpf.Ui.Controls;

namespace MksStudio.UI.Views;

public partial class QuickTranslateWindow : FluentWindow
{
    private readonly QuickTranslateViewModel _viewModel;

    public QuickTranslateWindow(string filePath, SubtitleDocument document)
    {
        InitializeComponent();
        _viewModel = new QuickTranslateViewModel(filePath, document)
        {
            RequestClose = Close
        };
        _viewModel.RequestScrollToItem += item =>
        {
            PreviewListBox.ScrollIntoView(item);
        };
        DataContext = _viewModel;
    }
}
