using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MksStudio.Core.Operations;
using MksStudio.Core.Operations.Models;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.UI.ViewModels;

public partial class TranslationViewModel : ObservableObject
{
    private readonly GoogleTranslateService _translateService;
    private CancellationTokenSource? _cts;

    public IReadOnlyList<LanguageOption> SourceLanguages { get; } = LanguageOption.SourceLanguages;
    public IReadOnlyList<LanguageOption> TargetLanguages { get; } = LanguageOption.TargetLanguages;

    [ObservableProperty]
    private LanguageOption _selectedSourceLanguage;

    [ObservableProperty]
    private LanguageOption _selectedTargetLanguage;

    [ObservableProperty]
    private int _selectedScopeIndex = 0; // 0 = Entire Track, 1 = Selected Cues Only

    [ObservableProperty]
    private int _selectedDestinationIndex = 0; // 0 = Create New Track, 1 = Overwrite Current Track

    [ObservableProperty]
    private bool _isTranslating = false;

    [ObservableProperty]
    private double _progressPercent = 0.0;

    [ObservableProperty]
    private string _progressStatusText = "Siap untuk menerjemahkan subtitle.";

    [ObservableProperty]
    private string _currentOriginalPreview = string.Empty;

    [ObservableProperty]
    private string _currentTranslatedPreview = string.Empty;

    [ObservableProperty]
    private int _totalCuesCount = 0;

    [ObservableProperty]
    private int _selectedCuesCount = 0;

    public Action<List<SubtitleCue>, LanguageOption, LanguageOption, int>? OnTranslationCompleted { get; set; }
    public Action? OnCloseRequested { get; set; }

    public TranslationViewModel()
    {
        _translateService = new GoogleTranslateService();
        _selectedSourceLanguage = SourceLanguages.FirstOrDefault(l => l.Code == "auto") ?? SourceLanguages[0];
        _selectedTargetLanguage = TargetLanguages.FirstOrDefault(l => l.Code == "id") ?? TargetLanguages[0];
    }

    [RelayCommand]
    public async Task StartTranslationAsync(IList<SubtitleCue> cuesToTranslate)
    {
        if (cuesToTranslate == null || cuesToTranslate.Count == 0)
        {
            ProgressStatusText = "Tidak ada baris subtitle yang dipilih untuk diterjemahkan.";
            return;
        }

        IsTranslating = true;
        ProgressPercent = 0.0;
        ProgressStatusText = $"Memulai penerjemahan {cuesToTranslate.Count} baris ({SelectedSourceLanguage.DisplayName} ➔ {SelectedTargetLanguage.DisplayName})...";
        _cts = new CancellationTokenSource();

        var progress = new Progress<TranslationProgress>(p =>
        {
            ProgressPercent = p.Percentage;
            ProgressStatusText = p.StatusMessage;
            CurrentOriginalPreview = p.CurrentText;
            CurrentTranslatedPreview = p.TranslatedPreview;
        });

        try
        {
            var translatedCues = await _translateService.TranslateCuesAsync(
                cuesToTranslate,
                SelectedSourceLanguage.Code,
                SelectedTargetLanguage.Code,
                batchSize: 15,
                progress: progress,
                ct: _cts.Token
            );

            ProgressPercent = 100.0;
            ProgressStatusText = $"Selesai! Berhasil menerjemahkan {translatedCues.Count} baris subtitle.";

            OnTranslationCompleted?.Invoke(
                translatedCues,
                SelectedSourceLanguage,
                SelectedTargetLanguage,
                SelectedDestinationIndex
            );

            await Task.Delay(400);
            OnCloseRequested?.Invoke();
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText = "Penerjemahan dibatalkan oleh pengguna.";
        }
        catch (Exception ex)
        {
            ProgressStatusText = $"Terjadi kesalahan saat menerjemahkan: {ex.Message}";
        }
        finally
        {
            IsTranslating = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    public void CancelTranslation()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            ProgressStatusText = "Membatalkan proses...";
        }
    }

    [RelayCommand]
    public void Close()
    {
        if (IsTranslating)
        {
            CancelTranslation();
        }
        OnCloseRequested?.Invoke();
    }
}
