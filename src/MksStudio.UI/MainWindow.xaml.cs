using System.IO;
using System.Windows;
using MksStudio.UI.ViewModels;
using MksStudio.UI.Views;

namespace MksStudio.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel Vm => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            string path = args[1];
            if (File.Exists(path) || Directory.Exists(path))
            {
                OpenFileOrFolder(path);
            }
        }
    }

    private void OpenFileOrFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Vm.ExtractorVm.IsBatchMode = true;
            Vm.ExtractorVm.InputPath = path;
            Vm.ExtractorVm.OutputFolder = path;
            Vm.CurrentViewIndex = 2; // Switch to Extractor
            _ = Vm.ExtractorVm.ScanInput();
        }
        else if (path.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
        {
            Vm.ExtractorVm.IsBatchMode = false;
            Vm.ExtractorVm.InputPath = path;
            Vm.ExtractorVm.OutputFolder = Path.GetDirectoryName(path) ?? string.Empty;
            Vm.CurrentViewIndex = 2; // Switch to Extractor
            _ = Vm.ExtractorVm.ScanInput();
        }
        else if (path.EndsWith(".mks", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var demuxer = new Core.Matroska.MatroskaDemuxer();
                var mks = demuxer.Demux(path);
                Vm.ExtractorVm.RequestOpenInEditor?.Invoke(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading MKS file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else if (path.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith(".ass", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase))
        {
            Vm.ExtractorVm.RequestOpenInEditor?.Invoke(path);
        }
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                string first = files[0];
                if (Directory.Exists(first))
                {
                    // Folder dropped: open in batch MKV extractor
                    Vm.ExtractorVm.IsBatchMode = true;
                    Vm.ExtractorVm.InputPath = first;
                    Vm.ExtractorVm.OutputFolder = first;
                    Vm.CurrentViewIndex = 2; // Switch to Extractor
                    _ = Vm.ExtractorVm.ScanInput();
                }
                else if (first.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
                {
                    // MKV video dropped: open in MKV extractor
                    Vm.ExtractorVm.IsBatchMode = false;
                    Vm.ExtractorVm.InputPath = first;
                    Vm.ExtractorVm.OutputFolder = Path.GetDirectoryName(first) ?? string.Empty;
                    Vm.CurrentViewIndex = 2; // Switch to Extractor
                    _ = Vm.ExtractorVm.ScanInput();
                }
                else if (first.EndsWith(".mks", StringComparison.OrdinalIgnoreCase))
                {
                    // MKS container dropped: open in MKS Studio Editor
                    try
                    {
                        var demuxer = new Core.Matroska.MatroskaDemuxer();
                        var mks = demuxer.Demux(first);
                        Vm.OpenFileCommand.Execute(null); // Or direct load
                        Vm.CurrentViewIndex = 1; // Switch to Editor
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading dropped MKS file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (first.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) ||
                         first.EndsWith(".ass", StringComparison.OrdinalIgnoreCase) ||
                         first.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase))
                {
                    // Subtitle file dropped: load in editor
                    Vm.ExtractorVm.RequestOpenInEditor?.Invoke(first);
                }
            }
        }
    }

    private void OnModeStandaloneChecked(object sender, RoutedEventArgs e)
    {
        if (Vm?.ExtractorVm != null) Vm.ExtractorVm.OutputMode = 0;
    }

    private void OnModeMksChecked(object sender, RoutedEventArgs e)
    {
        if (Vm?.ExtractorVm != null) Vm.ExtractorVm.OutputMode = 1;
    }

    private void OnModeFontsChecked(object sender, RoutedEventArgs e)
    {
        if (Vm?.ExtractorVm != null) Vm.ExtractorVm.OutputMode = 2;
    }

    private void OnOpenMkvExtractorClicked(object sender, RoutedEventArgs e)
    {
        Vm.CurrentViewIndex = 2;
    }

    private void OnOpenTimeShiftClicked(object sender, RoutedEventArgs e)
    {
        var win = new TimeShiftWindow(Vm)
        {
            Owner = this
        };
        win.ShowDialog();
    }

    private void OnOpenSearchReplaceClicked(object sender, RoutedEventArgs e)
    {
        var win = new SearchReplaceWindow(Vm)
        {
            Owner = this
        };
        win.Show();
    }

    private void OnOpenAttachmentsClicked(object sender, RoutedEventArgs e)
    {
        var win = new AttachmentWindow(Vm)
        {
            Owner = this
        };
        win.ShowDialog();
    }

    private void OnAboutClicked(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "MKS Subtitle Studio v1.0\n\n" +
            "A modern, high-performance editor and viewer for Matroska Subtitle (.mks) containers.\n" +
            "Includes full MKV Subtitle & Font Extractor (MKVToolNix Wrapper).\n" +
            "Supports SubRip (SRT), Advanced SubStation Alpha (ASS), WebVTT, and embedded fonts.\n\n" +
            "Built with C# and .NET 10 WPF.",
            "About MKS Subtitle Studio",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}