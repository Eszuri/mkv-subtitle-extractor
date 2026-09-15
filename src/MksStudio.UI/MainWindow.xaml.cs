using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using MksStudio.Core.Subtitles;
using MksStudio.UI.ViewModels;
using MksStudio.UI.Views;

namespace MksStudio.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private MainViewModel Vm => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
        AppTitleBar.WndProcInvoked += OnTitleBarWndProc;
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

    private void OnTitleBarWndProc(object? sender, Wpf.Ui.Controls.HwndProcEventArgs e)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTCLIENT = 1;

        if (e.Message == WM_NCHITTEST)
        {
            try
            {
                int x = (short)(e.LParam.ToInt32() & 0xFFFF);
                int y = (short)(e.LParam.ToInt32() >> 16);
                var screenPoint = new Point(x, y);

                Point clientPoint = PointFromScreen(screenPoint);

                // 1. Direct bounding box check on NavPillBorder
                if (NavPillBorder != null && NavPillBorder.IsVisible)
                {
                    GeneralTransform transform = NavPillBorder.TransformToAncestor(this);
                    Rect bounds = transform.TransformBounds(new Rect(0, 0, NavPillBorder.ActualWidth, NavPillBorder.ActualHeight));
                    if (bounds.Contains(clientPoint))
                    {
                        e.Handled = true;
                        e.ReturnValue = (nint)HTCLIENT;
                        return;
                    }
                }

                // 2. Direct bounding box check on DurationBorder
                if (DurationBorder != null && DurationBorder.IsVisible)
                {
                    GeneralTransform transform = DurationBorder.TransformToAncestor(this);
                    Rect bounds = transform.TransformBounds(new Rect(0, 0, DurationBorder.ActualWidth, DurationBorder.ActualHeight));
                    if (bounds.Contains(clientPoint))
                    {
                        e.Handled = true;
                        e.ReturnValue = (nint)HTCLIENT;
                        return;
                    }
                }

                // 3. Fallback visual tree hit test for buttons or interactive controls
                var hitResult = VisualTreeHelper.HitTest(this, clientPoint);
                if (hitResult?.VisualHit != null)
                {
                    DependencyObject? current = hitResult.VisualHit;
                    while (current != null && current != this)
                    {
                        if (current is Button || current is TextBox || current is CheckBox ||
                            current is ComboBox || current is RadioButton)
                        {
                            e.Handled = true;
                            e.ReturnValue = (nint)HTCLIENT;
                            return;
                        }
                        current = VisualTreeHelper.GetParent(current);
                    }
                }
            }
            catch
            {
                // Fallback to default TitleBar handling
            }
        }
    }

    private void OnNavHomeClicked(object sender, RoutedEventArgs e) => Vm.CurrentViewIndex = 0;
    private void OnNavEditorClicked(object sender, RoutedEventArgs e) => Vm.CurrentViewIndex = 1;
    private void OnNavExtractorClicked(object sender, RoutedEventArgs e) => Vm.CurrentViewIndex = 2;

    private void OnNavHomePreviewMouseDown(object sender, MouseButtonEventArgs e) => Vm.CurrentViewIndex = 0;
    private void OnNavEditorPreviewMouseDown(object sender, MouseButtonEventArgs e) => Vm.CurrentViewIndex = 1;
    private void OnNavExtractorPreviewMouseDown(object sender, MouseButtonEventArgs e) => Vm.CurrentViewIndex = 2;

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
        else if (SubtitleFormatRouter.IsSupportedExtension(Path.GetExtension(path)))
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
                else if (SubtitleFormatRouter.IsSupportedExtension(Path.GetExtension(first)))
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

    private void OnTrackRowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && FindVisualParent<CheckBox>(dep) != null)
            return;

        if (sender is DataGridRow row && row.DataContext is MkvTrackItemViewModel track)
        {
            track.IsSelected = !track.IsSelected;
        }
    }

    private void OnFontRowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && FindVisualParent<CheckBox>(dep) != null)
            return;

        if (sender is DataGridRow row && row.DataContext is MkvAttachmentItemViewModel font)
        {
            font.IsSelected = !font.IsSelected;
        }
    }

    private void OnDataGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            if (sender is DataGrid dg && dg.SelectedItems.Count > 0)
            {
                foreach (var item in dg.SelectedItems)
                {
                    if (item is MkvTrackItemViewModel track)
                        track.IsSelected = !track.IsSelected;
                    else if (item is MkvAttachmentItemViewModel font)
                        font.IsSelected = !font.IsSelected;
                }
                e.Handled = true;
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
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
        var win = new SearchReplaceWindow(Vm, this)
        {
            Owner = this
        };
        win.Show();
    }

    public void ScrollToCue(CueItemViewModel cue, int matchIndex = -1, int matchLength = 0)
    {
        if (cue == null) return;

        // Ensure Editor view is displayed
        Vm.CurrentViewIndex = 1;

        // Clear filter query if this cue is hidden by active filter
        if (!string.IsNullOrEmpty(Vm.FilterQuery) && !Vm.VisibleCues.Contains(cue))
        {
            Vm.FilterQuery = string.Empty;
        }

        // Set SelectedCue in VM and DataGrid
        Vm.SelectedCue = cue;
        CuesDataGrid.SelectedItem = cue;

        // Scroll immediately to make it visible in the viewport
        CuesDataGrid.UpdateLayout();
        CuesDataGrid.ScrollIntoView(cue);

        // Highlight matching text if provided
        if (matchIndex >= 0 && matchLength > 0 && CueRawTextBox != null)
        {
            var text = CueRawTextBox.Text ?? string.Empty;
            if (matchIndex + matchLength <= text.Length)
            {
                CueRawTextBox.Select(matchIndex, matchLength);
            }
        }

        // Deferred update to ensure virtualized row layout is updated
        Dispatcher.BeginInvoke(new Action(() =>
        {
            CuesDataGrid.ScrollIntoView(cue);
            if (matchIndex >= 0 && matchLength > 0 && CueRawTextBox != null)
            {
                var text = CueRawTextBox.Text ?? string.Empty;
                if (matchIndex + matchLength <= text.Length)
                {
                    CueRawTextBox.Select(matchIndex, matchLength);
                }
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnOpenAttachmentsClicked(object sender, RoutedEventArgs e)
    {
        var win = new AttachmentWindow(Vm)
        {
            Owner = this
        };
        win.ShowDialog();
    }

    private void OnOpenTranslationClicked(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedTrack == null)
        {
            MessageBox.Show("Silakan pilih track subtitle yang ingin diterjemahkan terlebih dahulu.", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var translationVm = new TranslationViewModel();
        translationVm.OnTranslationCompleted = (translatedCues, srcLang, tgtLang, destMode) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (destMode == 0)
                {
                    // Create New Track
                    var source = Vm.SelectedTrack?.Model;
                    if (source == null) return;

                    var newTrack = new MksStudio.Core.Matroska.Models.MksTrack
                    {
                        TrackNumber = (ulong)(Vm.Tracks.Count + 1),
                        Name = $"{source.Name} [{tgtLang.DisplayName}]",
                        Language = tgtLang.Code,
                        LanguageIetf = tgtLang.Code,
                        CodecId = source.CodecId,
                        CodecName = source.CodecName,
                        IsDefault = false,
                        IsForced = false
                    };

                    foreach (var cue in translatedCues)
                    {
                        newTrack.Subtitles.Cues.Add(cue);
                    }

                    foreach (var st in source.Subtitles.Styles)
                    {
                        newTrack.Subtitles.Styles.Add(new MksStudio.Core.Subtitles.Models.AssStyle
                        {
                            Name = st.Name,
                            Fontname = st.Fontname,
                            Fontsize = st.Fontsize,
                            PrimaryColour = st.PrimaryColour
                        });
                    }

                    var newTrackVm = new TrackItemViewModel(newTrack);
                    Vm.Tracks.Add(newTrackVm);
                    Vm.SelectedTrack = newTrackVm;
                    Vm.SelectedCue = newTrackVm.Cues.FirstOrDefault();
                    Vm.IsModified = true;
                    Vm.CurrentViewIndex = 1; // Ensure Editor tab is active
                    Vm.RefreshAllViews();
                    Vm.StatusMessage = $"Berhasil membuat track terjemahan baru '{newTrack.Name}' ({translatedCues.Count} baris).";
                }
                else
                {
                    // Overwrite Selected Track / Cues
                    if (Vm.SelectedTrack == null) return;

                    Vm.SelectedTrack.SyncModelCues();
                    if (translationVm.SelectedScopeIndex == 1)
                    {
                        var dict = translatedCues.ToDictionary(c => c.Index, c => c.RawText);
                        foreach (var cueVm in Vm.SelectedTrack.Cues)
                        {
                            if (dict.TryGetValue(cueVm.Index, out var newText))
                            {
                                cueVm.RawText = newText;
                            }
                        }
                    }
                    else
                    {
                        Vm.SelectedTrack.Model.Subtitles.Cues.Clear();
                        Vm.SelectedTrack.Model.Subtitles.Cues.AddRange(translatedCues);
                        Vm.SelectedTrack.RefreshCues();
                    }

                    Vm.SelectedCue = Vm.SelectedTrack.Cues.FirstOrDefault();
                    Vm.IsModified = true;
                    Vm.CurrentViewIndex = 1; // Ensure Editor tab is active
                    Vm.RefreshAllViews();
                    Vm.StatusMessage = $"Berhasil menerjemahkan {translatedCues.Count} baris pada track '{Vm.SelectedTrack.Name}'.";
                }
            });
        };

        var selectedCues = Vm.SelectedCue != null ? new List<CueItemViewModel> { Vm.SelectedCue } : new List<CueItemViewModel>();
        var win = new TranslationWindow(translationVm, Vm.SelectedTrack, selectedCues)
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