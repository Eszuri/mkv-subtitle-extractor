using System.Windows;
using MksStudio.UI.ViewModels;

namespace MksStudio.UI.Views;

public partial class MkvExtractorWindow : Window
{
    private readonly MainViewModel _mainVm;
    private readonly MkvExtractorViewModel _vm;

    public MkvExtractorWindow(MainViewModel mainVm)
    {
        InitializeComponent();
        _mainVm = mainVm;
        _vm = new MkvExtractorViewModel
        {
            RequestOpenInEditor = path =>
            {
                if (path.EndsWith(".mks", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var demuxer = new Core.Matroska.MatroskaDemuxer();
                        var mks = demuxer.Demux(path);
                        _mainVm.CreateNewFileCommand.Execute(null); // Reset
                        _mainVm.OpenFile(); // Or direct load
                        Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to open extracted MKS file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show($"Extracted subtitle is saved at:\n{path}\n\nYou can now import or edit it directly in MKS Studio.", "File Ready", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        };

        DataContext = _vm;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                string first = files[0];
                if (System.IO.Directory.Exists(first))
                {
                    _vm.IsBatchMode = true;
                    _vm.InputPath = first;
                    _vm.OutputFolder = first;
                    _vm.ScanInputCommand.Execute(null);
                }
                else if (first.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
                {
                    _vm.IsBatchMode = false;
                    _vm.InputPath = first;
                    _vm.OutputFolder = System.IO.Path.GetDirectoryName(first) ?? string.Empty;
                    _vm.ScanInputCommand.Execute(null);
                }
            }
        }
    }

    private void OnModeStandaloneChecked(object sender, RoutedEventArgs e)
    {
        if (_vm != null) _vm.OutputMode = 0;
    }

    private void OnModeMksChecked(object sender, RoutedEventArgs e)
    {
        if (_vm != null) _vm.OutputMode = 1;
    }

    private void OnModeFontsChecked(object sender, RoutedEventArgs e)
    {
        if (_vm != null) _vm.OutputMode = 2;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
