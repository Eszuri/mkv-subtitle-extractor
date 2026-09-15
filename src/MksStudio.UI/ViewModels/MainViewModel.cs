using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
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
    }

    public void RefreshAllViews()
    {
        OnPropertyChanged(nameof(Tracks));
        OnPropertyChanged(nameof(TotalTracksCount));
        OnPropertyChanged(nameof(TotalDurationText));
        OnPropertyChanged(nameof(VisibleCues));
        OnPropertyChanged(nameof(FilteredCues));
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(SelectedCueCharCount));
        OnPropertyChanged(nameof(SelectedCueWordCount));
        OnPropertyChanged(nameof(SelectedCueLineCount));
    }

    partial void OnSelectedCueChanged(CueItemViewModel? value)
    {
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(SelectedCueCharCount));
        OnPropertyChanged(nameof(SelectedCueWordCount));
        OnPropertyChanged(nameof(SelectedCueLineCount));
    }

    public string PreviewText => SelectedCue?.PlainText ?? "No subtitle selected";
    public int SelectedCueCharCount => SelectedCue?.RawText.Length ?? 0;
    public int SelectedCueWordCount => string.IsNullOrWhiteSpace(SelectedCue?.PlainText)
        ? 0
        : SelectedCue.PlainText.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    public int SelectedCueLineCount => string.IsNullOrWhiteSpace(SelectedCue?.RawText)
        ? 0
        : SelectedCue.RawText.Split(['\n', '\\'], StringSplitOptions.RemoveEmptyEntries).Length;

    [RelayCommand]
    public void InsertFormatting(string tag)
    {
        if (SelectedCue == null) return;
        SelectedCue.RawText = tag switch
        {
            "b" => SelectedTrack?.IsAss == true ? $"{{{SelectedCue.RawText}}}{{\\b1}}" : $"<b>{SelectedCue.RawText}</b>",
            "i" => SelectedTrack?.IsAss == true ? $"{{{SelectedCue.RawText}}}{{\\i1}}" : $"<i>{SelectedCue.RawText}</i>",
            "u" => SelectedTrack?.IsAss == true ? $"{{{SelectedCue.RawText}}}{{\\u1}}" : $"<u>{SelectedCue.RawText}</u>",
            "n" => $"{SelectedCue.RawText}\\N",
            "color_yellow" => SelectedTrack?.IsAss == true ? $"{{\\c&H00FFFF&}}{SelectedCue.RawText}" : $"<font color=\"yellow\">{SelectedCue.RawText}</font>",
            "color_cyan" => SelectedTrack?.IsAss == true ? $"{{\\c&HFFFF00&}}{SelectedCue.RawText}" : $"<font color=\"cyan\">{SelectedCue.RawText}</font>",
            _ => SelectedCue.RawText
        };
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
