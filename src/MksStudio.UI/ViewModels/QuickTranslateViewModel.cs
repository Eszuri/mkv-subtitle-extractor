using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MksStudio.Core.Operations;
using MksStudio.Core.Operations.Models;
using MksStudio.Core.Subtitles;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.UI.ViewModels;

public partial class QuickTranslatePreviewItem : ObservableObject
{
    public string OriginalText { get; init; } = string.Empty;

    [ObservableProperty]
    private string _translatedText = string.Empty;
}

public partial class QuickTranslateViewModel : ObservableObject
{
    private readonly GoogleTranslateService _translateService = new();
    private CancellationTokenSource? _cts;

    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public string OutputFolder => Path.GetDirectoryName(FilePath) ?? string.Empty;
    public SubtitleDocument Document { get; }
    public int CueCount => Document.Cues.Count;

    public IReadOnlyList<LanguageOption> TargetLanguages { get; } = LanguageOption.TargetLanguages;

    [ObservableProperty]
    private LanguageOption _selectedTargetLanguage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isPreviewVisible;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public ObservableCollection<QuickTranslatePreviewItem> PreviewItems { get; } = new();

    public event Action<QuickTranslatePreviewItem>? RequestScrollToItem;

    public Action? RequestClose { get; set; }

    public QuickTranslateViewModel(string filePath, SubtitleDocument document)
    {
        FilePath = filePath;
        Document = document;
        _selectedTargetLanguage = TargetLanguages.FirstOrDefault(l => l.Code == "en") ?? TargetLanguages[0];
    }

    [RelayCommand]
    public async Task StartTranslationAsync()
    {
        if (Document.Cues.Count == 0)
        {
            MessageBox.Show("Tidak ada baris subtitle untuk diterjemahkan.", "Translate Subtitle", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        IsCompleted = false;
        IsPreviewVisible = true;
        ProgressPercent = 0;
        StatusText = "Menerjemahkan...";
        _cts = new CancellationTokenSource();

        PreviewItems.Clear();
        for (int i = 0; i < Document.Cues.Count; i++)
        {
            string orig = SubtitleCue.CleanTags(Document.Cues[i].RawText);
            if (string.IsNullOrWhiteSpace(orig))
            {
                orig = Document.Cues[i].RawText;
            }

            PreviewItems.Add(new QuickTranslatePreviewItem
            {
                OriginalText = orig,
                TranslatedText = string.Empty
            });
        }

        try
        {
            var progress = new Progress<TranslationProgress>(p =>
            {
                if (p.TotalCount > 0)
                {
                    ProgressPercent = (double)p.ProcessedCount / p.TotalCount * 100.0;
                    StatusText = $"Menerjemahkan: {p.ProcessedCount} / {p.TotalCount} baris ({ProgressPercent:0}%)";
                }

                if (p.CueIndex >= 0 && p.CueIndex < PreviewItems.Count)
                {
                    var item = PreviewItems[p.CueIndex];
                    string trans = SubtitleCue.CleanTags(p.TranslatedPreview);
                    if (string.IsNullOrWhiteSpace(trans))
                    {
                        trans = p.TranslatedPreview;
                    }
                    item.TranslatedText = trans;

                    if (p.CueIndex % 3 == 0 || p.ProcessedCount >= p.TotalCount)
                    {
                        RequestScrollToItem?.Invoke(item);
                    }
                }
            });

            var translatedCues = await _translateService.TranslateCuesAsync(
                Document.Cues,
                sourceLang: "auto",
                targetLang: SelectedTargetLanguage.Code,
                batchSize: 15,
                progress: progress,
                ct: _cts.Token);

            if (translatedCues.Count > 0)
            {
                // Prepare translated document preserving original styles and metadata
                Document.Cues.Clear();
                Document.Cues.AddRange(translatedCues);

                string baseName = Path.GetFileNameWithoutExtension(FilePath);
                string ext = Path.GetExtension(FilePath);
                string outPath = Path.Combine(OutputFolder, $"{baseName}_translated{ext}");

                string serialized = SubtitleFormatRouter.Serialize(Document, ext);
                await File.WriteAllTextAsync(outPath, serialized, Encoding.UTF8);

                IsCompleted = true;
                StatusText = $"Selesai! {translatedCues.Count} baris berhasil diterjemahkan.";
                var result = MessageBox.Show(
                    "Terjemahan selesai!\n\nBuka folder?",
                    "Translate Subtitle",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    OpenFileInExplorer(outPath);
                    RequestClose?.Invoke();
                }
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Penerjemahan dibatalkan.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal menerjemahkan subtitle:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = "Gagal menerjemahkan.";
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    public void CancelOrClose()
    {
        if (IsBusy)
        {
            _cts?.Cancel();
        }
        else
        {
            RequestClose?.Invoke();
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
