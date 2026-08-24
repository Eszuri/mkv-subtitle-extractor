using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MksStudio.Core.MkvToolNix;
using MksStudio.Core.MkvToolNix.Models;

namespace MksStudio.UI.ViewModels;

public partial class MkvExtractorViewModel : ObservableObject
{
    private readonly MkvMergeService _mergeService = new();
    private readonly MkvExtractService _extractService = new();

    [ObservableProperty]
    private string _inputPath = string.Empty;

    [ObservableProperty]
    private bool _isBatchMode = false;

    [ObservableProperty]
    private string _outputFolder = string.Empty;

    [ObservableProperty]
    private int _outputMode = 0; // 0 = Standalone (.srt/.ass), 1 = MKS Container (.mks), 2 = Fonts Only

    [ObservableProperty]
    private bool _extractFonts = true;

    [ObservableProperty]
    private int _progressValue = 0;

    [ObservableProperty]
    private string _statusText = "Ready. Select an MKV file or folder.";

    [ObservableProperty]
    private string _logOutput = string.Empty;

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private string? _lastExtractedFile;

    [ObservableProperty]
    private bool _isMkvToolNixAvailable = true;

    public ObservableCollection<MkvTrackItemViewModel> ScannedTracks { get; } = [];
    public ObservableCollection<MkvAttachment> ScannedAttachments { get; } = [];
    public ObservableCollection<string> BatchFiles { get; } = [];

    public Action<string>? RequestOpenInEditor { get; set; }

    public MkvExtractorViewModel()
    {
        IsMkvToolNixAvailable = MkvToolNixLocator.IsAvailable();
        if (!IsMkvToolNixAvailable)
        {
            StatusText = "MKVToolNix not detected! Please ensure it is installed in 'C:\\Program Files\\MKVToolNix'.";
        }
    }

    [RelayCommand]
    private async Task BrowseInputAsync()
    {
        if (IsBatchMode)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select Folder Containing MKV Files"
            };

            if (folderDialog.ShowDialog() == true)
            {
                InputPath = folderDialog.FolderName;
                if (string.IsNullOrEmpty(OutputFolder))
                    OutputFolder = folderDialog.FolderName;
                await ScanInput();
            }
        }
        else
        {
            var fileDialog = new OpenFileDialog
            {
                Filter = "Matroska Video (*.mkv)|*.mkv|All Files (*.*)|*.*",
                Title = "Select MKV Video File"
            };

            if (fileDialog.ShowDialog() == true)
            {
                InputPath = fileDialog.FileName;
                if (string.IsNullOrEmpty(OutputFolder))
                    OutputFolder = Path.GetDirectoryName(fileDialog.FileName) ?? string.Empty;
                await ScanInput();
            }
        }
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Output Folder for Extracted Subtitles"
        };

        if (dialog.ShowDialog() == true)
        {
            OutputFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    public async Task ScanInput()
    {
        if (string.IsNullOrWhiteSpace(InputPath)) return;

        ScannedTracks.Clear();
        ScannedAttachments.Clear();
        BatchFiles.Clear();
        LogOutput = string.Empty;
        ProgressValue = 0;

        if (IsBatchMode)
        {
            if (!Directory.Exists(InputPath)) return;

            var files = Directory.GetFiles(InputPath, "*.mkv", SearchOption.TopDirectoryOnly);
            foreach (var f in files)
                BatchFiles.Add(f);

            StatusText = $"Found {BatchFiles.Count} MKV file(s) in folder.";
            if (BatchFiles.Count > 0)
            {
                // Scan the first file as a preview
                await ScanSingleFileAsync(BatchFiles[0]);
            }
        }
        else
        {
            if (!File.Exists(InputPath)) return;
            await ScanSingleFileAsync(InputPath);
        }
    }

    private async Task ScanSingleFileAsync(string mkvPath)
    {
        try
        {
            IsBusy = true;
            StatusText = $"Inspecting '{Path.GetFileName(mkvPath)}' with MKVToolNix...";

            var info = await _mergeService.IdentifyAsync(mkvPath);

            ScannedTracks.Clear();
            foreach (var st in info.SubtitleTracks)
            {
                ScannedTracks.Add(new MkvTrackItemViewModel(st));
            }

            ScannedAttachments.Clear();
            foreach (var att in info.Attachments)
            {
                ScannedAttachments.Add(att);
            }

            StatusText = $"Detected {ScannedTracks.Count} subtitle track(s) and {ScannedAttachments.Count} font(s).";
            AppendLog($"Scanned '{Path.GetFileName(mkvPath)}': {ScannedTracks.Count} subtitles, {ScannedAttachments.Count} attachments.");
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to scan: {ex.Message}";
            AppendLog($"Scan Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAllTracks()
    {
        foreach (var t in ScannedTracks)
            t.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAllTracks()
    {
        foreach (var t in ScannedTracks)
            t.IsSelected = false;
    }

    [RelayCommand]
    public async Task ExtractAsync()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            MessageBox.Show("Please select an output folder.", "Output Folder Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Directory.CreateDirectory(OutputFolder);
        IsBusy = true;
        ProgressValue = 0;
        LogOutput = string.Empty;

        var progressReporter = new Progress<int>(p => ProgressValue = p);
        var logReporter = new Progress<string>(AppendLog);

        try
        {
            if (IsBatchMode)
            {
                int total = BatchFiles.Count;
                int current = 0;

                foreach (var file in BatchFiles)
                {
                    current++;
                    StatusText = $"Extracting file {current} of {total}: '{Path.GetFileName(file)}'...";
                    await ProcessSingleFileExtractionAsync(file, progressReporter, logReporter);
                }

                StatusText = $"Batch extraction completed ({total} files processed).";
                MessageBox.Show($"Successfully processed {total} file(s) into folder:\n{OutputFolder}", "Batch Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusText = $"Extracting from '{Path.GetFileName(InputPath)}'...";
                string? firstOut = await ProcessSingleFileExtractionAsync(InputPath, progressReporter, logReporter);
                LastExtractedFile = firstOut;

                StatusText = "Extraction completed successfully!";
                MessageBox.Show($"Extraction completed!\nSaved to: {OutputFolder}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Extraction failed: {ex.Message}";
            AppendLog($"Extraction Error: {ex.Message}");
            MessageBox.Show($"Extraction Error:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string?> ProcessSingleFileExtractionAsync(string mkvPath, IProgress<int> progress, IProgress<string> log)
    {
        string baseName = Path.GetFileNameWithoutExtension(mkvPath);
        var selectedIds = ScannedTracks.Where(t => t.IsSelected).Select(t => t.Id).ToList();

        if (OutputMode == 1) // Mode: MKS Container
        {
            string outMks = Path.Combine(OutputFolder, $"{baseName}.mks");
            log.Report($"Demuxing to MKS: '{Path.GetFileName(outMks)}'...");
            bool ok = await _mergeService.ExtractToMksAsync(mkvPath, outMks, selectedIds.Any() ? selectedIds : null, ExtractFonts, log);
            progress.Report(100);
            return ok ? outMks : null;
        }
        else if (OutputMode == 2) // Mode: Fonts Only
        {
            var info = await _mergeService.IdentifyAsync(mkvPath);
            if (info.Attachments.Count > 0)
            {
                string fontsDir = Path.Combine(OutputFolder, "fonts");
                var attMap = info.Attachments.ToDictionary(a => a.Id, a => Path.Combine(fontsDir, a.FileName));
                await _extractService.ExtractAttachmentsAsync(mkvPath, attMap, progress, log);
                return fontsDir;
            }
            return null;
        }
        else // Mode: Standalone Subtitles (.srt / .ass)
        {
            var extractedFiles = await _extractService.ExtractSubtitlesAutoAsync(
                mkvPath,
                OutputFolder,
                selectedIds.Any() ? selectedIds : null,
                ExtractFonts,
                progress,
                log);

            return extractedFiles.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void OpenLastInEditor()
    {
        if (!string.IsNullOrEmpty(LastExtractedFile) && File.Exists(LastExtractedFile))
        {
            RequestOpenInEditor?.Invoke(LastExtractedFile);
        }
        else
        {
            MessageBox.Show("No extracted file is available to open yet.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void AppendLog(string message)
    {
        var sb = new StringBuilder(LogOutput);
        sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        LogOutput = sb.ToString();
    }
}
