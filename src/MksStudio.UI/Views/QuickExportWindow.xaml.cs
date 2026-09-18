using System.IO;
using System.Windows;
using MksStudio.Core.MkvToolNix.Models;
using MksStudio.UI.ViewModels;
using Wpf.Ui.Controls;

namespace MksStudio.UI.Views;

public partial class QuickExportWindow : FluentWindow
{
    private readonly QuickExportViewModel _viewModel;

    public QuickExportWindow(string mkvPath, IEnumerable<MkvTrack> tracks)
    {
        InitializeComponent();
        _viewModel = new QuickExportViewModel(mkvPath, tracks)
        {
            RequestClose = Close
        };
        Closing += (s, e) =>
        {
            if (_viewModel.IsBusy)
            {
                e.Cancel = true;
            }
        };
        DataContext = _viewModel;
    }
}
