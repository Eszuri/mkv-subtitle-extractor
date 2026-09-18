using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MksStudio.Core.MkvToolNix;
using MksStudio.Core.MkvToolNix.Models;
using MksStudio.Core.Subtitles;

namespace MksStudio.UI.ViewModels;

public partial class QuickExportTrackItem : ObservableObject
{
    public int Id { get; }
    public string Language { get; }
    public string Codec { get; }
    public string CodecId { get; }
    public string Name { get; }
    public bool IsDefault { get; }
    public bool IsForced { get; }

    [ObservableProperty]
    private bool _isSelected = true;

    public string TitleDisplay => string.IsNullOrWhiteSpace(Name)
        ? $"Track #{Id} ({Language})"
        : $"{Name} ({Language})";

    public string BadgeText => $"#{Id}";

    public string CodecDisplay
    {
        get
        {
            if (Codec.Contains("SubStation", StringComparison.OrdinalIgnoreCase) || CodecId.Contains("ASS", StringComparison.OrdinalIgnoreCase))
                return "SubStationAlpha (.ass)";
            if (Codec.Contains("SubRip", StringComparison.OrdinalIgnoreCase) || CodecId.Contains("UTF8", StringComparison.OrdinalIgnoreCase))
                return "SubRip (.srt)";
            if (Codec.Contains("VTT", StringComparison.OrdinalIgnoreCase))
                return "WebVTT (.vtt)";
            return Codec;
        }
    }

    public QuickExportTrackItem(MkvTrack track)
    {
        Id = track.Id;
        Language = string.IsNullOrWhiteSpace(track.Language) ? "und" : track.Language;
        Codec = track.Codec ?? "Subtitle";
        CodecId = track.CodecId;
        Name = track.TrackName;
        IsDefault = track.IsDefault;
        IsForced = track.IsForced;
    }
}

public partial class QuickExportViewModel : ObservableObject
{
    private readonly MkvMergeService _mergeService = new();
    private readonly MkvExtractService _extractService = new();

    public string MkvPath { get; }
    public string FileName => Path.GetFileName(MkvPath);
    public string OutputFolder => Path.GetDirectoryName(MkvPath) ?? string.Empty;

    public ObservableCollection<QuickExportTrackItem> Tracks { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private int _progressValue;

    public Action? RequestClose { get; set; }

    public QuickExportViewModel(string mkvPath, IEnumerable<MkvTrack> tracks)
    {
        MkvPath = mkvPath;
        foreach (var t in tracks)
        {
            Tracks.Add(new QuickExportTrackItem(t));
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var t in Tracks) t.IsSelected = true;
    }

    [RelayCommand]
    private void UnselectAll()
    {
        foreach (var t in Tracks) t.IsSelected = false;
    }

    [RelayCommand]
    public async Task ExportMksAsync()
    {
        var selected = Tracks.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Pilih setidaknya satu track.", "Export Subtitle", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        ProgressValue = 0;
        StatusText = "Mengekspor .mks...";

        try
        {
            string baseName = Path.GetFileNameWithoutExtension(MkvPath);
            string outMks = Path.Combine(OutputFolder, $"{baseName}.mks");
            if (File.Exists(outMks))
            {
                outMks = Path.Combine(OutputFolder, $"{baseName}_subtitles.mks");
            }

            var trackIds = selected.Select(t => t.Id).ToList();
            var progress = new Progress<int>(p => ProgressValue = p);
            var log = new Progress<string>(msg => StatusText = msg);

            bool success = await Task.Run(() => _mergeService.ExtractToMksAsync(
                MkvPath,
                outMks,
                trackIds,
                includeAttachments: false,
                logProgress: log));

            if (success && File.Exists(outMks))
            {
                StatusText = "Export .mks selesai!";
                var result = MessageBox.Show(
                    "Export .mks selesai!\n\nBuka folder?",
                    "Export Subtitle",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    OpenFileInExplorer(outMks);
                }

                RequestClose?.Invoke();
            }
            else
            {
                MessageBox.Show("Gagal mengekspor file .mks.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal export .mks:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
        }
    }

    [RelayCommand]
    public async Task ExportAssAsync()
    {
        var selected = Tracks.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Pilih setidaknya satu track.", "Export Subtitle", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        ProgressValue = 0;
        StatusText = "Mengekspor .ass...";

        try
        {
            string baseName = Path.GetFileNameWithoutExtension(MkvPath);
            var exportedFiles = new List<string>();
            var progress = new Progress<int>(p => ProgressValue = p);
            var log = new Progress<string>(msg => StatusText = msg);

            await Task.Run(async () =>
            {
                foreach (var track in selected)
                {
                    string langSuffix = !string.IsNullOrWhiteSpace(track.Language) && track.Language != "und"
                        ? $".{track.Language}"
                        : "";

                    string outFileName = selected.Count == 1
                        ? $"{baseName}.ass"
                        : $"{baseName}{langSuffix}.track{track.Id}.ass";

                    string outAssPath = Path.Combine(OutputFolder, outFileName);

                    bool isNativeAss = track.CodecId.Contains("ASS", StringComparison.OrdinalIgnoreCase) ||
                                       track.CodecId.Contains("SSA", StringComparison.OrdinalIgnoreCase) ||
                                       track.Codec.Contains("SubStation", StringComparison.OrdinalIgnoreCase);

                    if (isNativeAss)
                    {
                        // Direct extraction of native ASS stream
                        var trackMap = new Dictionary<int, string> { [track.Id] = outAssPath };
                        await _extractService.ExtractTracksAsync(MkvPath, trackMap, progress, log);
                    }
                    else
                    {
                        // Extract to temporary file and convert to pure ASS
                        string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.srt");
                        var trackMap = new Dictionary<int, string> { [track.Id] = tempFile };
                        await _extractService.ExtractTracksAsync(MkvPath, trackMap, progress, log);

                        if (File.Exists(tempFile))
                        {
                            string srtContent = await File.ReadAllTextAsync(tempFile, Encoding.UTF8);
                            var doc = SrtCodec.Parse(srtContent);
                            var assDoc = SubtitleConverter.ConvertToAss(doc);
                            string assContent = AssCodec.Serialize(assDoc);
                            await File.WriteAllTextAsync(outAssPath, assContent, Encoding.UTF8);
                            try { File.Delete(tempFile); } catch { }
                        }
                    }

                    if (File.Exists(outAssPath))
                    {
                        exportedFiles.Add(outAssPath);
                    }
                }
            });

            if (exportedFiles.Count > 0)
            {
                StatusText = "Export .ass selesai!";
                var result = MessageBox.Show(
                    "Export .ass selesai!\n\nBuka folder?",
                    "Export Subtitle",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    OpenFileInExplorer(exportedFiles[0]);
                }

                RequestClose?.Invoke();
            }
            else
            {
                MessageBox.Show("Gagal mengekspor file .ass.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal export .ass:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
        }
    }

    private static void OpenFileInExplorer(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
        }
        catch
        {
            // Ignore if explorer launch fails
        }
    }
}
