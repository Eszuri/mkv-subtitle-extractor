using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private int _outputMode = 0; // 0 = Standalone Subtitles, 1 = MKS Container, 2 = Fonts Only

    [ObservableProperty]
    private bool _extractFonts = true;

    [ObservableProperty]
    private int _progressValue = 0;

    [ObservableProperty]
    private string _statusText = "Siap. Silakan pilih file video MKV atau folder batch.";

    [ObservableProperty]
    private string _logOutput = string.Empty;

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private string? _lastExtractedFile;

    [ObservableProperty]
    private bool _isMkvToolNixAvailable = true;

    [ObservableProperty]
    private int _selectedContentTab = 0; // 0 = Subtitle Tracks, 1 = Embedded Fonts

    public ObservableCollection<MkvTrackItemViewModel> ScannedTracks { get; } = [];
    public ObservableCollection<MkvAttachmentItemViewModel> ScannedFontItems { get; } = [];
    public ObservableCollection<MkvAttachment> ScannedAttachments { get; } = [];
    public ObservableCollection<string> BatchFiles { get; } = [];

    public Action<string>? RequestOpenInEditor { get; set; }
    public Action? RequestNavigateToExtractor { get; set; }

    public int SubtitleTracksCount => ScannedTracks.Count;
    public int FontsCount => ScannedFontItems.Count;

    public MkvExtractorViewModel()
    {
        IsMkvToolNixAvailable = MkvToolNixLocator.IsAvailable();
        if (!IsMkvToolNixAvailable)
        {
            StatusText = "MKVToolNix tidak terdeteksi! Pastikan terpasang di 'C:\\Program Files\\MKVToolNix'.";
        }
    }

    [RelayCommand]
    private async Task BrowseInputAsync()
    {
        if (IsBatchMode)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Pilih Folder Berisi File MKV"
            };

            if (folderDialog.ShowDialog() == true)
            {
                InputPath = folderDialog.FolderName;
                if (string.IsNullOrEmpty(OutputFolder))
                    OutputFolder = folderDialog.FolderName;
                RequestNavigateToExtractor?.Invoke();
                await ScanInput();
            }
        }
        else
        {
            var fileDialog = new OpenFileDialog
            {
                Filter = "Matroska Video (*.mkv)|*.mkv|Semua File (*.*)|*.*",
                DefaultExt = ".mkv",
                FilterIndex = 1,
                Title = "Pilih File Video MKV"
            };

            if (fileDialog.ShowDialog() == true)
            {
                InputPath = fileDialog.FileName;
                if (string.IsNullOrEmpty(OutputFolder))
                    OutputFolder = Path.GetDirectoryName(fileDialog.FileName) ?? string.Empty;
                RequestNavigateToExtractor?.Invoke();
                await ScanInput();
            }
        }
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih Folder Output Hasil Ekstraksi"
        };

        if (dialog.ShowDialog() == true)
        {
            OutputFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (!string.IsNullOrWhiteSpace(OutputFolder) && Directory.Exists(OutputFolder))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = OutputFolder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Tidak dapat membuka folder:\n{ex.Message}", "Info", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else
        {
            MessageBox.Show("Folder output belum ditentukan atau belum dibuat.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    public async Task ScanInput()
    {
        if (string.IsNullOrWhiteSpace(InputPath)) return;

        ScannedTracks.Clear();
        ScannedFontItems.Clear();
        ScannedAttachments.Clear();
        BatchFiles.Clear();
        LogOutput = string.Empty;
        ProgressValue = 0;

        OnPropertyChanged(nameof(SubtitleTracksCount));
        OnPropertyChanged(nameof(FontsCount));

        if (IsBatchMode)
        {
            if (!Directory.Exists(InputPath)) return;

            var files = Directory.GetFiles(InputPath, "*.mkv", SearchOption.TopDirectoryOnly);
            foreach (var f in files)
                BatchFiles.Add(f);

            StatusText = $"Ditemukan {BatchFiles.Count} file MKV dalam folder.";
            if (BatchFiles.Count > 0)
            {
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
            StatusText = $"Memeriksa '{Path.GetFileName(mkvPath)}' dengan MKVToolNix...";

            var info = await _mergeService.IdentifyAsync(mkvPath);

            ScannedTracks.Clear();
            foreach (var st in info.SubtitleTracks)
            {
                ScannedTracks.Add(new MkvTrackItemViewModel(st));
            }

            ScannedAttachments.Clear();
            ScannedFontItems.Clear();
            foreach (var att in info.Attachments)
            {
                ScannedAttachments.Add(att);
                ScannedFontItems.Add(new MkvAttachmentItemViewModel(att));
            }

            OnPropertyChanged(nameof(SubtitleTracksCount));
            OnPropertyChanged(nameof(FontsCount));

            StatusText = $"Terdeteksi {ScannedTracks.Count} track subtitle dan {ScannedFontItems.Count} font lampiran.";
            AppendLog($"Scan '{Path.GetFileName(mkvPath)}': {ScannedTracks.Count} subtitle, {ScannedFontItems.Count} font.");
        }
        catch (Exception ex)
        {
            StatusText = $"Gagal memindai: {ex.Message}";
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
    private void SelectAllFonts()
    {
        foreach (var f in ScannedFontItems)
            f.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAllFonts()
    {
        foreach (var f in ScannedFontItems)
            f.IsSelected = false;
    }

    [RelayCommand]
    public async Task ExtractAsync()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            MessageBox.Show("Silakan tentukan folder output terlebih dahulu.", "Folder Output Dibutuhkan", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    StatusText = $"Mengekstrak file {current} dari {total}: '{Path.GetFileName(file)}'...";
                    await ProcessSingleFileExtractionAsync(file, progressReporter, logReporter);
                }

                StatusText = $"Ekstraksi batch selesai ({total} file diproses).";
                MessageBox.Show($"Berhasil memproses {total} file ke dalam folder:\n{OutputFolder}", "Ekstraksi Selesai", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusText = $"Mengekstrak dari '{Path.GetFileName(InputPath)}'...";
                string? firstOut = await ProcessSingleFileExtractionAsync(InputPath, progressReporter, logReporter);
                LastExtractedFile = firstOut;

                StatusText = "Ekstraksi berhasil selesai!";
                MessageBox.Show($"Ekstraksi berhasil diselesaikan!\nDisimpan ke: {OutputFolder}", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Ekstraksi gagal: {ex.Message}";
            AppendLog($"Extraction Error: {ex.Message}");
            MessageBox.Show($"Terjadi kesalahan saat ekstraksi:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            log.Report($"Demuxing ke container MKS: '{Path.GetFileName(outMks)}'...");
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
                var selectedFontIds = ScannedFontItems.Where(f => f.IsSelected).Select(f => f.Id).ToHashSet();
                
                var attachmentsToExtract = selectedFontIds.Count > 0
                    ? info.Attachments.Where(a => selectedFontIds.Contains(a.Id)).ToList()
                    : info.Attachments;

                var attMap = attachmentsToExtract.ToDictionary(a => a.Id, a => Path.Combine(fontsDir, a.FileName));
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
            MessageBox.Show("Belum ada file hasil ekstraksi yang dapat dibuka.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void AppendLog(string message)
    {
        var sb = new StringBuilder(LogOutput);
        sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        LogOutput = sb.ToString();
    }
}
