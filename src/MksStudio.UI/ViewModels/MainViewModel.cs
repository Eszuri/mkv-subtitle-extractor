using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MksStudio.Core.Ebml;
using MksStudio.Core.Matroska;
using MksStudio.Core.Matroska.Models;
using MksStudio.Core.Operations;
using MksStudio.Core.Subtitles;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MatroskaDemuxer _demuxer = new();
    private readonly MatroskaMuxer _muxer = new();

    private MksFile _currentMks = new();

    [ObservableProperty]
    private string _windowTitle = "MKS Subtitle Studio - [Untitled.mks]";

    [ObservableProperty]
    private string _statusMessage = "Ready. Open or create a .mks file to begin.";

    [ObservableProperty]
    private TrackItemViewModel? _selectedTrack;

    [ObservableProperty]
    private CueItemViewModel? _selectedCue;

    [ObservableProperty]
    private string _containerTitle = "Matroska Subtitles";

    [ObservableProperty]
    private string _currentFilePath = string.Empty;

    [ObservableProperty]
    private bool _isModified = false;

    public ObservableCollection<TrackItemViewModel> Tracks { get; } = [];
    public ObservableCollection<AttachmentItemViewModel> Attachments { get; } = [];

    public string TotalDurationText => _currentMks.TotalDuration.ToString(@"hh\:mm\:ss\.fff");
    public int TotalTracksCount => Tracks.Count;
    public int TotalAttachmentsCount => Attachments.Count;

    [ObservableProperty]
    private int _currentViewIndex = 0; // 0 = Home, 1 = MKS Editor, 2 = MKV Extractor

    [ObservableProperty]
    private MkvExtractorViewModel _extractorVm;

    [RelayCommand]
    public void NavigateToHome() => CurrentViewIndex = 0;

    [RelayCommand]
    public void NavigateToEditor() => CurrentViewIndex = 1;

    [RelayCommand]
    public void NavigateToExtractor() => CurrentViewIndex = 2;

    public MainViewModel()
    {
        _extractorVm = new MkvExtractorViewModel
        {
            RequestNavigateToExtractor = () => CurrentViewIndex = 2,
            RequestOpenInEditor = path =>
            {
                if (File.Exists(path))
                {
                    if (path.EndsWith(".mks", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
                    {
                        var mks = _demuxer.Demux(path);
                        LoadMksModel(mks, path);
                    }
                    else
                    {
                        // Single subtitle file: import as track into editor
                        string text = File.ReadAllText(path);
                        string ext = Path.GetExtension(path).ToLowerInvariant();
                        var (codecId, codecName) = SubtitleFormatRouter.GetMatroskaCodecInfo(ext);

                        var track = new MksTrack
                        {
                            Name = Path.GetFileNameWithoutExtension(path),
                            Language = "und",
                            IsDefault = true,
                            CodecId = codecId,
                            CodecName = codecName,
                            Subtitles = SubtitleFormatRouter.Parse(text, path)
                        };

                        _currentMks = new MksFile { Title = Path.GetFileNameWithoutExtension(path) };
                        _currentMks.Tracks.Add(track);
                        LoadMksModel(_currentMks, path);
                    }

                    CurrentViewIndex = 1; // Switch to MKS Editor
                    StatusMessage = $"Loaded '{Path.GetFileName(path)}' into MKS Subtitle Studio.";
                }
            }
        };

        CreateNewFile();
        CurrentViewIndex = 0; // Start at Home Screen
    }

    [ObservableProperty]
    private string _filterQuery = string.Empty;

    public string SearchFilter
    {
        get => FilterQuery;
        set => FilterQuery = value;
    }

    public ObservableCollection<CueItemViewModel> VisibleCues
    {
        get
        {
            if (SelectedTrack == null) return [];
            if (string.IsNullOrWhiteSpace(FilterQuery)) return SelectedTrack.Cues;

            var filtered = SelectedTrack.Cues
                .Where(c => c.RawText.Contains(FilterQuery, StringComparison.OrdinalIgnoreCase) ||
                            c.Actor.Contains(FilterQuery, StringComparison.OrdinalIgnoreCase) ||
                            c.Style.Contains(FilterQuery, StringComparison.OrdinalIgnoreCase));

            return new ObservableCollection<CueItemViewModel>(filtered);
        }
    }

    public ObservableCollection<CueItemViewModel> FilteredCues => VisibleCues;

    partial void OnFilterQueryChanged(string value)
    {
        OnPropertyChanged(nameof(VisibleCues));
        OnPropertyChanged(nameof(FilteredCues));
        OnPropertyChanged(nameof(SearchFilter));
    }

    partial void OnSelectedTrackChanged(TrackItemViewModel? value)
    {
        SelectedCue = value?.Cues.FirstOrDefault();
        OnPropertyChanged(nameof(TotalDurationText));
        OnPropertyChanged(nameof(VisibleCues));
        OnPropertyChanged(nameof(FilteredCues));
        UpdatePreviewProperties();
    }

    public void RefreshAllViews()
    {
        OnPropertyChanged(nameof(Tracks));
        OnPropertyChanged(nameof(TotalTracksCount));
        OnPropertyChanged(nameof(TotalDurationText));
        OnPropertyChanged(nameof(VisibleCues));
        OnPropertyChanged(nameof(FilteredCues));
        UpdatePreviewProperties();
    }

    partial void OnSelectedCueChanged(CueItemViewModel? oldValue, CueItemViewModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnSelectedCuePropertyChanged;
        }
        if (newValue != null)
        {
            newValue.PropertyChanged += OnSelectedCuePropertyChanged;
        }

        UpdatePreviewProperties();
    }

    private void OnSelectedCuePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CueItemViewModel.RawText)
            or nameof(CueItemViewModel.PlainText)
            or nameof(CueItemViewModel.Style)
            or nameof(CueItemViewModel.StartTimeText)
            or nameof(CueItemViewModel.EndTimeText)
            or nameof(CueItemViewModel.Actor))
        {
            UpdatePreviewProperties();
        }
    }

    private void UpdatePreviewProperties()
    {
        OnPropertyChanged(nameof(HasSelectedCue));
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(PreviewRawText));
        OnPropertyChanged(nameof(PreviewTimecodeText));
        OnPropertyChanged(nameof(CurrentAssStyle));
        OnPropertyChanged(nameof(ActiveStyleDisplayName));
        OnPropertyChanged(nameof(ActiveStyleDetails));
        OnPropertyChanged(nameof(PreviewPrimaryBrush));
        OnPropertyChanged(nameof(PreviewPrimaryColor));
        OnPropertyChanged(nameof(PreviewOutlineBrush));
        OnPropertyChanged(nameof(PreviewOutlineThickness));
        OnPropertyChanged(nameof(PreviewShadowColor));
        OnPropertyChanged(nameof(PreviewShadowDepth));
        OnPropertyChanged(nameof(PreviewFontFamily));
        OnPropertyChanged(nameof(PreviewFontSize));
        OnPropertyChanged(nameof(PreviewFontWeight));
        OnPropertyChanged(nameof(PreviewFontStyle));
        OnPropertyChanged(nameof(PreviewTextAlignment));
        OnPropertyChanged(nameof(PreviewVerticalAlignment));
        OnPropertyChanged(nameof(PreviewHorizontalAlignment));
        OnPropertyChanged(nameof(PreviewMargin));
        OnPropertyChanged(nameof(HasSelectedCueActor));
        OnPropertyChanged(nameof(SelectedCueActorText));
        OnPropertyChanged(nameof(SelectedCueCharCount));
        OnPropertyChanged(nameof(SelectedCueWordCount));
        OnPropertyChanged(nameof(SelectedCueLineCount));
    }

    public bool HasSelectedCue => SelectedCue != null;
    public bool HasSelectedCueActor => !string.IsNullOrWhiteSpace(SelectedCue?.Actor);
    public string SelectedCueActorText => HasSelectedCueActor ? $"Actor: {SelectedCue!.Actor}" : string.Empty;
    public string PreviewText => SelectedCue?.PlainText ?? string.Empty;
    public string PreviewRawText => SelectedCue?.RawText ?? string.Empty;
    public string PreviewTimecodeText => SelectedCue != null
        ? $"▶ {SelectedCue.StartTimeText} → {SelectedCue.EndTimeText} ({SelectedCue.Duration.TotalSeconds:F1}s)"
        : "--:--:--.---";

    public int SelectedCueCharCount => SelectedCue?.RawText.Length ?? 0;
    public int SelectedCueWordCount => string.IsNullOrWhiteSpace(SelectedCue?.PlainText)
        ? 0
        : SelectedCue.PlainText.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    public int SelectedCueLineCount => string.IsNullOrWhiteSpace(SelectedCue?.RawText)
        ? 0
        : SelectedCue.RawText.Replace("\r\n", "\n").Replace("\\N", "\n").Replace("\\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

    public AssStyle? CurrentAssStyle
    {
        get
        {
            if (SelectedTrack?.Model?.Subtitles?.Styles == null) return null;
            var styles = SelectedTrack.Model.Subtitles.Styles;
            if (styles.Count == 0) return null;

            if (SelectedCue != null && !string.IsNullOrWhiteSpace(SelectedCue.Style))
            {
                var match = styles.FirstOrDefault(s => string.Equals(s.Name, SelectedCue.Style, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            var def = styles.FirstOrDefault(s => string.Equals(s.Name, "Default", StringComparison.OrdinalIgnoreCase));
            return def ?? styles.FirstOrDefault();
        }
    }

    public string ActiveStyleDisplayName => !string.IsNullOrWhiteSpace(SelectedCue?.Style)
        ? SelectedCue.Style
        : (CurrentAssStyle?.Name ?? "Default");

    public string ActiveStyleDetails
    {
        get
        {
            if (CurrentAssStyle == null) return "Standard Style";
            return $"{CurrentAssStyle.Fontname} • {CurrentAssStyle.Fontsize:0}pt • Outl {CurrentAssStyle.Outline:0.#} • Shad {CurrentAssStyle.Shadow:0.#}";
        }
    }

    public static Color ParseAssColor(string? colorStr, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(colorStr)) return fallback;
        string s = colorStr.Trim();

        if (s.StartsWith("&H", StringComparison.OrdinalIgnoreCase) || s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            string hex = s.Substring(2).TrimEnd('&').Trim();
            if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint val))
            {
                if (hex.Length <= 6)
                {
                    byte b = (byte)((val >> 16) & 0xFF);
                    byte g = (byte)((val >> 8) & 0xFF);
                    byte r = (byte)(val & 0xFF);
                    return Color.FromArgb(255, r, g, b);
                }
                else
                {
                    byte assA = (byte)((val >> 24) & 0xFF);
                    byte b = (byte)((val >> 16) & 0xFF);
                    byte g = (byte)((val >> 8) & 0xFF);
                    byte r = (byte)(val & 0xFF);
                    return Color.FromArgb((byte)(255 - assA), r, g, b);
                }
            }
        }
        else if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long num))
        {
            uint val = (uint)num;
            byte assA = (byte)((val >> 24) & 0xFF);
            byte b = (byte)((val >> 16) & 0xFF);
            byte g = (byte)((val >> 8) & 0xFF);
            byte r = (byte)(val & 0xFF);
            return Color.FromArgb((byte)(255 - assA), r, g, b);
        }
        return fallback;
    }

    public Color PreviewPrimaryColor => ParseAssColor(CurrentAssStyle?.PrimaryColour, Colors.White);

    public Brush PreviewPrimaryBrush
    {
        get
        {
            var brush = new SolidColorBrush(PreviewPrimaryColor);
            brush.Freeze();
            return brush;
        }
    }

    public Brush PreviewOutlineBrush
    {
        get
        {
            var color = ParseAssColor(CurrentAssStyle?.OutlineColour, Colors.Black);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    public Color PreviewShadowColor => ParseAssColor(CurrentAssStyle?.BackColour, Color.FromArgb(190, 0, 0, 0));

    public double PreviewOutlineThickness => CurrentAssStyle != null && CurrentAssStyle.Outline > 0
        ? Math.Clamp(CurrentAssStyle.Outline * 0.9, 1.2, 4.5)
        : 2.5;

    public double PreviewShadowDepth => CurrentAssStyle != null && CurrentAssStyle.Shadow > 0
        ? Math.Clamp(CurrentAssStyle.Shadow * 0.8, 1.0, 3.5)
        : 1.5;

    public FontFamily PreviewFontFamily
    {
        get
        {
            string fontName = CurrentAssStyle?.Fontname?.Trim() ?? string.Empty;
            return !string.IsNullOrEmpty(fontName)
                ? new FontFamily($"{fontName}, Trebuchet MS, Arial, Segoe UI")
                : new FontFamily("Trebuchet MS, Arial, Segoe UI");
        }
    }

    public double PreviewFontSize
    {
        get
        {
            double fs = CurrentAssStyle?.Fontsize ?? 22.0;
            if (fs <= 0) fs = 22.0;
            if (fs >= 65) return Math.Clamp(fs * 0.42, 16, 32);
            if (fs >= 40) return Math.Clamp(fs * 0.52, 16, 28);
            if (fs >= 26) return Math.Clamp(fs * 0.72, 16, 26);
            return Math.Clamp(fs, 14, 24);
        }
    }

    public FontWeight PreviewFontWeight => FontWeights.Normal;
    public FontStyle PreviewFontStyle => (CurrentAssStyle?.Italic != 0) ? FontStyles.Italic : FontStyles.Normal;

    public VerticalAlignment PreviewVerticalAlignment
    {
        get
        {
            int align = CurrentAssStyle?.Alignment ?? 2;
            return align switch
            {
                7 or 8 or 9 => VerticalAlignment.Top,
                4 or 5 or 6 => VerticalAlignment.Center,
                _ => VerticalAlignment.Bottom
            };
        }
    }

    public HorizontalAlignment PreviewHorizontalAlignment
    {
        get
        {
            int align = CurrentAssStyle?.Alignment ?? 2;
            return align switch
            {
                1 or 4 or 7 => HorizontalAlignment.Left,
                3 or 6 or 9 => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center
            };
        }
    }

    public TextAlignment PreviewTextAlignment
    {
        get
        {
            int align = CurrentAssStyle?.Alignment ?? 2;
            return align switch
            {
                1 or 4 or 7 => TextAlignment.Left,
                3 or 6 or 9 => TextAlignment.Right,
                _ => TextAlignment.Center
            };
        }
    }

    public Thickness PreviewMargin
    {
        get
        {
            int align = CurrentAssStyle?.Alignment ?? 2;
            double hMargin = Math.Clamp((CurrentAssStyle?.MarginL ?? 10) * 0.6, 24, 60);
            double vMargin = Math.Clamp((CurrentAssStyle?.MarginV ?? 10) * 0.5, 16, 40);

            return align switch
            {
                7 or 8 or 9 => new Thickness(hMargin, vMargin, hMargin, 0),
                4 or 5 or 6 => new Thickness(hMargin, 0, hMargin, 0),
                _ => new Thickness(hMargin, 0, hMargin, vMargin)
            };
        }
    }

    [RelayCommand]
    public void InsertFormatting(string tag)
    {
        if (SelectedCue == null) return;
        bool isAss = SelectedTrack?.IsAss != false;

        string newText = SelectedCue.RawText;
        switch (tag)
        {
            case "b":
            case "i":
            case "u":
            case "s":
            case "n":
            case "h":
                var tagRes = SubtitleFormatter.ApplyTag(SelectedCue.RawText, 0, 0, tag, isAss);
                newText = tagRes.newText;
                break;
            case "color_yellow":
                var cy = SubtitleFormatter.ApplyColor(SelectedCue.RawText, 0, 0, "FFFF00", isAss);
                newText = cy.newText;
                break;
            case "color_cyan":
                var cc = SubtitleFormatter.ApplyColor(SelectedCue.RawText, 0, 0, "00FFFF", isAss);
                newText = cc.newText;
                break;
            case "color_white":
                var cw = SubtitleFormatter.ApplyColor(SelectedCue.RawText, 0, 0, "FFFFFF", isAss);
                newText = cw.newText;
                break;
            case "strip":
                var st = SubtitleFormatter.StripFormatting(SelectedCue.RawText, 0, 0);
                newText = st.newText;
                break;
        }

        SelectedCue.RawText = newText;
        IsModified = true;
    }

    [RelayCommand]
    public void MoveCueUp()
    {
        if (SelectedTrack == null || SelectedCue == null) return;
        int idx = SelectedTrack.Cues.IndexOf(SelectedCue);
        if (idx > 0)
        {
            var cue = SelectedCue;
            SelectedTrack.Cues.Move(idx, idx - 1);
            SelectedTrack.ReindexCues();
            SelectedCue = cue;
            IsModified = true;
            OnPropertyChanged(nameof(VisibleCues));
            OnPropertyChanged(nameof(FilteredCues));
        }
    }

    [RelayCommand]
    public void MoveCueDown()
    {
        if (SelectedTrack == null || SelectedCue == null) return;
        int idx = SelectedTrack.Cues.IndexOf(SelectedCue);
        if (idx >= 0 && idx < SelectedTrack.Cues.Count - 1)
        {
            var cue = SelectedCue;
            SelectedTrack.Cues.Move(idx, idx + 1);
            SelectedTrack.ReindexCues();
            SelectedCue = cue;
            IsModified = true;
            OnPropertyChanged(nameof(VisibleCues));
            OnPropertyChanged(nameof(FilteredCues));
        }
    }

    // --- File Operations ---

    [RelayCommand]
    public void CreateNewFile()
    {
        _currentMks = new MksFile { Title = "Untitled Subtitle Container" };
        _currentMks.Tracks.Add(new MksTrack
        {
            TrackNumber = 1,
            Name = "Default Subtitle",
            Language = "eng",
            CodecId = EbmlConstants.CodecSrt
        });

        LoadMksModel(_currentMks, null);
        CurrentViewIndex = 1; // Switch to editor
        StatusMessage = "Created new .mks container with 1 default track.";
    }

    [RelayCommand]
    public void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Semua Format Subtitle Didukung (*.mks;*.srt;*.ass;*.ssa;*.vtt;*.ttml;*.dfxp;*.xml;*.smi;*.sami;*.sub;*.sbv;*.lrc;*.mkv)|*.mks;*.srt;*.ass;*.ssa;*.vtt;*.ttml;*.dfxp;*.xml;*.smi;*.sami;*.sub;*.sbv;*.lrc;*.mkv|" +
                     "Matroska Subtitles (*.mks)|*.mks|" +
                     "SubRip Subtitles (*.srt)|*.srt|" +
                     "Advanced SubStation (*.ass;*.ssa)|*.ass;*.ssa|" +
                     "WebVTT Subtitles (*.vtt)|*.vtt|" +
                     "Timed Text XML (*.ttml;*.dfxp;*.xml)|*.ttml;*.dfxp;*.xml|" +
                     "SAMI Subtitles (*.smi;*.sami)|*.smi;*.sami|" +
                     "MicroDVD Subtitles (*.sub)|*.sub|" +
                     "YouTube SubViewer (*.sbv)|*.sbv|" +
                     "Timed Lyrics (*.lrc)|*.lrc|" +
                     "Matroska Video (*.mkv)|*.mkv|" +
                     "Semua File (*.*)|*.*",
            DefaultExt = ".mks",
            FilterIndex = 1,
            Title = "Open Subtitle File"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                if (ext is ".mks" or ".mkv")
                {
                    var mks = _demuxer.Demux(dialog.FileName);
                    LoadMksModel(mks, dialog.FileName);
                }
                else
                {
                    // Single subtitle file: parse with unified SubtitleFormatRouter
                    string text = File.ReadAllText(dialog.FileName);
                    var (codecId, codecName) = SubtitleFormatRouter.GetMatroskaCodecInfo(ext);

                    var track = new MksTrack
                    {
                        TrackNumber = 1,
                        Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                        Language = "und",
                        IsDefault = true,
                        CodecId = codecId,
                        CodecName = codecName,
                        Subtitles = SubtitleFormatRouter.Parse(text, dialog.FileName)
                    };

                    _currentMks = new MksFile { Title = Path.GetFileNameWithoutExtension(dialog.FileName) };
                    _currentMks.Tracks.Add(track);
                    LoadMksModel(_currentMks, dialog.FileName);
                }
                CurrentViewIndex = 1; // Switch to editor
                StatusMessage = $"Opened '{Path.GetFileName(dialog.FileName)}' ({Tracks.Count} tracks, {Attachments.Count} attachments).";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Failed to open file.";
            }
        }
    }

    [RelayCommand]
    public void SaveFile()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
        {
            SaveAsFile();
        }
        else
        {
            SaveToFile(CurrentFilePath);
        }
    }

    [RelayCommand]
    public void SaveAsFile()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Matroska Subtitles (*.mks)|*.mks|All Files (*.*)|*.*",
            DefaultExt = ".mks",
            FileName = string.IsNullOrEmpty(CurrentFilePath) ? "Subtitles.mks" : Path.GetFileName(CurrentFilePath),
            Title = "Save MKS Container"
        };

        if (dialog.ShowDialog() == true)
        {
            SaveToFile(dialog.FileName);
        }
    }

    private void SaveToFile(string path)
    {
        try
        {
            SyncToModel();
            _muxer.Mux(_currentMks, path);
            CurrentFilePath = path;
            IsModified = false;
            UpdateTitle();
            StatusMessage = $"Successfully saved to '{Path.GetFileName(path)}'.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving file:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Failed to save file.";
        }
    }

    private void LoadMksModel(MksFile mks, string? path)
    {
        _currentMks = mks;
        CurrentFilePath = path ?? string.Empty;
        ContainerTitle = mks.Title;
        IsModified = false;

        Tracks.Clear();
        foreach (var track in mks.Tracks)
        {
            Tracks.Add(new TrackItemViewModel(track));
        }

        Attachments.Clear();
        foreach (var att in mks.Attachments)
        {
            Attachments.Add(new AttachmentItemViewModel(att));
        }

        SelectedTrack = Tracks.FirstOrDefault();
        UpdateTitle();
        OnPropertyChanged(nameof(TotalDurationText));
        OnPropertyChanged(nameof(TotalTracksCount));
        OnPropertyChanged(nameof(TotalAttachmentsCount));
    }

    private void SyncToModel()
    {
        _currentMks.Title = ContainerTitle;
        _currentMks.Tracks.Clear();
        ulong num = 1;
        foreach (var trackVm in Tracks)
        {
            trackVm.TrackNumber = num++;
            trackVm.SyncModelCues();
            _currentMks.Tracks.Add(trackVm.Model);
        }

        _currentMks.Attachments.Clear();
        foreach (var attVm in Attachments)
        {
            _currentMks.Attachments.Add(attVm.Model);
        }
    }

    private void UpdateTitle()
    {
        string name = string.IsNullOrEmpty(CurrentFilePath) ? "Untitled.mks" : Path.GetFileName(CurrentFilePath);
        WindowTitle = $"MKS Subtitle Studio - [{name}]";
    }

    // --- Track Management ---

    [RelayCommand]
    public void AddTrackFromFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Semua Format Subtitle Didukung (*.srt;*.ass;*.ssa;*.vtt;*.ttml;*.dfxp;*.xml;*.smi;*.sami;*.sub;*.sbv;*.lrc)|*.srt;*.ass;*.ssa;*.vtt;*.ttml;*.dfxp;*.xml;*.smi;*.sami;*.sub;*.sbv;*.lrc|" +
                     "SubRip Subtitles (*.srt)|*.srt|" +
                     "Advanced SubStation (*.ass;*.ssa)|*.ass;*.ssa|" +
                     "WebVTT Subtitles (*.vtt)|*.vtt|" +
                     "Timed Text XML (*.ttml;*.dfxp;*.xml)|*.ttml;*.dfxp;*.xml|" +
                     "SAMI Subtitles (*.smi;*.sami)|*.smi;*.sami|" +
                     "MicroDVD Subtitles (*.sub)|*.sub|" +
                     "YouTube SubViewer (*.sbv)|*.sbv|" +
                     "Timed Lyrics (*.lrc)|*.lrc|" +
                     "Semua File (*.*)|*.*",
            DefaultExt = ".srt",
            FilterIndex = 1,
            Title = "Import Subtitle Track"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string text = File.ReadAllText(dialog.FileName);
                string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                var (codecId, codecName) = SubtitleFormatRouter.GetMatroskaCodecInfo(ext);

                var track = new MksTrack
                {
                    TrackNumber = (ulong)(Tracks.Count + 1),
                    Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                    Language = "und",
                    IsDefault = Tracks.Count == 0,
                    CodecId = codecId,
                    CodecName = codecName,
                    Subtitles = SubtitleFormatRouter.Parse(text, dialog.FileName)
                };

                var trackVm = new TrackItemViewModel(track);
                Tracks.Add(trackVm);
                SelectedTrack = trackVm;
                IsModified = true;
                StatusMessage = $"Imported track '{track.Name}' with {track.CueCount} cues.";
                OnPropertyChanged(nameof(TotalTracksCount));
                OnPropertyChanged(nameof(TotalDurationText));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import subtitle file:\n{ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void RemoveTrack()
    {
        if (SelectedTrack == null) return;

        if (MessageBox.Show($"Are you sure you want to remove track '{SelectedTrack.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            int idx = Tracks.IndexOf(SelectedTrack);
            Tracks.Remove(SelectedTrack);
            SelectedTrack = Tracks.ElementAtOrDefault(idx) ?? Tracks.LastOrDefault();
            IsModified = true;
            OnPropertyChanged(nameof(TotalTracksCount));
            OnPropertyChanged(nameof(TotalDurationText));
            StatusMessage = "Track removed.";
        }
    }

    [RelayCommand]
    public void DuplicateTrack()
    {
        if (SelectedTrack == null) return;

        var source = SelectedTrack.Model;
        var dup = new MksTrack
        {
            TrackNumber = (ulong)(Tracks.Count + 1),
            Name = $"{source.Name} (Copy)",
            Language = source.Language,
            LanguageIetf = source.LanguageIetf,
            CodecId = source.CodecId,
            CodecName = source.CodecName,
            IsDefault = false,
            IsForced = false
        };

        foreach (var cue in source.Subtitles.Cues)
        {
            dup.Subtitles.Cues.Add(cue.Clone());
        }

        foreach (var st in source.Subtitles.Styles)
        {
            dup.Subtitles.Styles.Add(new AssStyle { Name = st.Name, Fontname = st.Fontname, Fontsize = st.Fontsize, PrimaryColour = st.PrimaryColour });
        }

        var dupVm = new TrackItemViewModel(dup);
        Tracks.Add(dupVm);
        SelectedTrack = dupVm;
        IsModified = true;
        OnPropertyChanged(nameof(TotalTracksCount));
        StatusMessage = $"Duplicated track to '{dup.Name}'.";
    }

    [RelayCommand]
    public void ExportTrack()
    {
        if (SelectedTrack == null) return;

        string currentExt = SelectedTrack.IsAss ? ".ass" : (SelectedTrack.IsVtt ? ".vtt" : ".srt");
        string baseName = !string.IsNullOrWhiteSpace(CurrentFilePath)
            ? Path.GetFileNameWithoutExtension(CurrentFilePath)
            : (!string.IsNullOrWhiteSpace(ContainerTitle) ? ContainerTitle : "Subtitles");

        var dialog = new SaveFileDialog
        {
            Filter = "SubRip Subtitles (*.srt)|*.srt|" +
                     "Advanced SubStation Alpha (*.ass)|*.ass|" +
                     "WebVTT Subtitles (*.vtt)|*.vtt|" +
                     "Timed Text XML (*.ttml)|*.ttml|" +
                     "SAMI Subtitles (*.smi)|*.smi|" +
                     "MicroDVD Subtitles (*.sub)|*.sub|" +
                     "YouTube SubViewer (*.sbv)|*.sbv|" +
                     "Timed Lyrics (*.lrc)|*.lrc|" +
                     "Matroska Subtitles (*.mks)|*.mks|" +
                     "All Files (*.*)|*.*",
            DefaultExt = currentExt,
            FilterIndex = SelectedTrack.IsAss ? 2 : (SelectedTrack.IsVtt ? 3 : 1),
            FileName = $"{baseName}{currentExt}",
            Title = "Export Subtitle Track"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                SelectedTrack.SyncModelCues();
                string saveExt = Path.GetExtension(dialog.FileName).ToLowerInvariant();

                if (saveExt == ".mks")
                {
                    // Export single track as an independent .mks container
                    var singleMks = new MksFile
                    {
                        Title = SelectedTrack.Name,
                        MuxingApp = "MksStudio v1.0",
                        WritingApp = "MksStudio Single Track Export"
                    };
                    singleMks.Tracks.Add(SelectedTrack.Model);
                    // Also include font attachments if ASS
                    if (SelectedTrack.IsAss)
                    {
                        foreach (var att in Attachments)
                            singleMks.Attachments.Add(att.Model);
                    }
                    _muxer.Mux(singleMks, dialog.FileName);
                }
                else
                {
                    string content = SubtitleFormatRouter.Serialize(SelectedTrack.Model.Subtitles, saveExt);
                    File.WriteAllText(dialog.FileName, content, Encoding.UTF8);
                }

                StatusMessage = $"Exported track to '{Path.GetFileName(dialog.FileName)}'.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void ExportAllTracks()
    {
        if (Tracks.Count == 0) return;

        var dialog = new OpenFolderDialog
        {
            Title = "Select Output Folder for Exporting All Tracks"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string baseName = !string.IsNullOrWhiteSpace(CurrentFilePath)
                    ? Path.GetFileNameWithoutExtension(CurrentFilePath)
                    : (!string.IsNullOrWhiteSpace(ContainerTitle) ? ContainerTitle : "Subtitles");

                int count = 0;
                foreach (var track in Tracks)
                {
                    track.SyncModelCues();
                    string ext = track.IsAss ? ".ass" : (track.IsVtt ? ".vtt" : ".srt");
                    string lang = string.IsNullOrWhiteSpace(track.Language) || track.Language == "und" ? "" : $".{track.Language}";
                    string fileName = Tracks.Count == 1 ? $"{baseName}{ext}" : $"{baseName}{lang}{ext}";
                    string fullPath = Path.Combine(dialog.FolderName, fileName);

                    string content = SubtitleFormatRouter.Serialize(track.Model.Subtitles, ext);
                    File.WriteAllText(fullPath, content, Encoding.UTF8);
                    count++;
                }

                StatusMessage = $"Exported all {count} tracks to '{dialog.FolderName}'.";
                MessageBox.Show($"Successfully exported {count} track(s) to folder:\n{dialog.FolderName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void ConvertTrackToSrt() => ConvertTrackToFormat(".srt");

    [RelayCommand]
    public void ConvertTrackToAss() => ConvertTrackToFormat(".ass");

    [RelayCommand]
    public void ConvertTrackToVtt() => ConvertTrackToFormat(".vtt");

    [RelayCommand]
    public void ConvertTrackToFormat(string targetExt)
    {
        if (SelectedTrack == null) return;
        SelectedTrack.SyncModelCues();
        var converted = SubtitleConverter.ConvertToFormat(SelectedTrack.Model.Subtitles, targetExt);
        var (codecId, codecName) = SubtitleFormatRouter.GetMatroskaCodecInfo(targetExt);

        SelectedTrack.Model.Subtitles.Cues.Clear();
        SelectedTrack.Model.Subtitles.Cues.AddRange(converted.Cues);
        SelectedTrack.CodecId = codecId;
        SelectedTrack.CodecName = codecName;
        SelectedTrack.RefreshCues();
        IsModified = true;
        RefreshAllViews();
        StatusMessage = $"Converted track to {codecName}.";
    }

    // --- Cue Editing Operations ---

    [RelayCommand]
    public void AddCue()
    {
        if (SelectedTrack == null) return;

        TimeSpan start = TimeSpan.FromSeconds(1);
        TimeSpan end = TimeSpan.FromSeconds(4);

        if (SelectedCue != null)
        {
            start = SelectedCue.EndTime + TimeSpan.FromMilliseconds(200);
            end = start + TimeSpan.FromSeconds(3);
        }
        else if (SelectedTrack.Cues.Count > 0)
        {
            start = SelectedTrack.Cues.Last().EndTime + TimeSpan.FromMilliseconds(200);
            end = start + TimeSpan.FromSeconds(3);
        }

        var newCue = SelectedTrack.AddCue(start, end, "New subtitle line");
        SelectedCue = newCue;
        IsModified = true;
        OnPropertyChanged(nameof(VisibleCues));
        OnPropertyChanged(nameof(FilteredCues));
        StatusMessage = $"Added cue #{newCue.Index}.";
    }

    [RelayCommand]
    public void DeleteCue()
    {
        if (SelectedTrack == null || SelectedCue == null) return;

        int idx = SelectedTrack.Cues.IndexOf(SelectedCue);
        SelectedTrack.RemoveCue(SelectedCue);
        SelectedCue = SelectedTrack.Cues.ElementAtOrDefault(idx) ?? SelectedTrack.Cues.LastOrDefault();
        IsModified = true;
        OnPropertyChanged(nameof(VisibleCues));
        OnPropertyChanged(nameof(FilteredCues));
        StatusMessage = "Cue deleted.";
    }

    [RelayCommand]
    public void SplitCue()
    {
        if (SelectedTrack == null || SelectedCue == null) return;

        var cue = SelectedCue;
        var midpoint = cue.StartTime + TimeSpan.FromMilliseconds(cue.Duration.TotalMilliseconds / 2.0);

        string firstText = cue.RawText;
        string secondText = "Second half";

        var lines = cue.RawText.Split(['\n', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length >= 2)
        {
            firstText = lines[0].Trim();
            secondText = string.Join("\n", lines.Skip(1)).Trim();
        }

        var oldEnd = cue.EndTime;
        cue.EndTime = midpoint;
        cue.RawText = firstText;
        cue.NotifyTimeChanged();

        var newCue = SelectedTrack.AddCue(midpoint, oldEnd, secondText);
        SelectedTrack.Cues.OrderBy(c => c.StartTime);
        SelectedTrack.ReindexCues();
        SelectedCue = newCue;
        IsModified = true;
        OnPropertyChanged(nameof(VisibleCues));
        OnPropertyChanged(nameof(FilteredCues));
        StatusMessage = "Cue split into two segments.";
    }

    // --- Attachment Management ---

    [RelayCommand]
    public void AddAttachment()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "TrueType Font (*.ttf)|*.ttf|OpenType Font (*.otf)|*.otf|Web Font (*.woff;*.woff2)|*.woff;*.woff2|PNG Image (*.png)|*.png|JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg|All Files (*.*)|*.*",
            DefaultExt = ".ttf",
            FilterIndex = 1,
            Multiselect = true,
            Title = "Add Font Attachment"
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                byte[] data = File.ReadAllBytes(file);
                var att = new MksAttachment
                {
                    FileName = Path.GetFileName(file),
                    FileMimeType = MksAttachment.DetectMimeType(file),
                    Data = data,
                    FileUid = (ulong)Random.Shared.Next(1000, 999999)
                };

                Attachments.Add(new AttachmentItemViewModel(att));
            }

            IsModified = true;
            OnPropertyChanged(nameof(TotalAttachmentsCount));
            StatusMessage = $"Added {dialog.FileNames.Length} attachment(s).";
        }
    }

    [RelayCommand]
    public void RemoveAttachment(AttachmentItemViewModel? attVm)
    {
        if (attVm != null && Attachments.Remove(attVm))
        {
            IsModified = true;
            OnPropertyChanged(nameof(TotalAttachmentsCount));
            StatusMessage = $"Removed attachment '{attVm.FileName}'.";
        }
    }
}
