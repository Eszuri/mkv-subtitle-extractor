using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MksStudio.Core.Ebml;
using MksStudio.Core.Matroska.Models;
using MksStudio.Core.Subtitles;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.UI.ViewModels;

/// <summary>
/// Observable ViewModel representing a subtitle track inside the MKS container.
/// </summary>
public partial class TrackItemViewModel : ObservableObject
{
    private readonly MksTrack _model;
    public MksTrack Model => _model;

    public ObservableCollection<CueItemViewModel> Cues { get; } = [];

    public TrackItemViewModel(MksTrack model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        RefreshCues();
    }

    public ulong TrackNumber
    {
        get => _model.TrackNumber;
        set
        {
            if (_model.TrackNumber != value)
            {
                _model.TrackNumber = value;
                OnPropertyChanged();
            }
        }
    }

    public ulong TrackUid
    {
        get => _model.TrackUid;
        set
        {
            if (_model.TrackUid != value)
            {
                _model.TrackUid = value;
                OnPropertyChanged();
            }
        }
    }

    public string Name
    {
        get => _model.Name;
        set
        {
            if (_model.Name != value)
            {
                _model.Name = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string Language
    {
        get => _model.Language;
        set
        {
            if (_model.Language != value)
            {
                _model.Language = value ?? "und";
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string? LanguageIetf
    {
        get => _model.LanguageIetf;
        set
        {
            if (_model.LanguageIetf != value)
            {
                _model.LanguageIetf = value;
                OnPropertyChanged();
            }
        }
    }

    public string CodecId
    {
        get => _model.CodecId;
        set
        {
            if (_model.CodecId != value)
            {
                _model.CodecId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CodecDisplay));
                OnPropertyChanged(nameof(IsAss));
                OnPropertyChanged(nameof(IsSrt));
                OnPropertyChanged(nameof(IsVtt));
            }
        }
    }

    public string CodecDisplay => CodecId switch
    {
        EbmlConstants.CodecSrt => "SubRip (SRT)",
        EbmlConstants.CodecAss => "Advanced SSA (ASS)",
        EbmlConstants.CodecSsa => "SubStation Alpha (SSA)",
        EbmlConstants.CodecVtt => "WebVTT",
        EbmlConstants.CodecPgs => "HDMV PGS (Bitmap)",
        EbmlConstants.CodecVobSub => "VobSub (Bitmap)",
        _ => CodecId
    };

    public bool IsDefault
    {
        get => _model.IsDefault;
        set
        {
            if (_model.IsDefault != value)
            {
                _model.IsDefault = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsForced
    {
        get => _model.IsForced;
        set
        {
            if (_model.IsForced != value)
            {
                _model.IsForced = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsAss => _model.IsAss;
    public bool IsSrt => _model.IsSrt;
    public bool IsVtt => _model.IsVtt;

    public int CueCount => Cues.Count;

    public string DisplayName => $"#{TrackNumber} - {Name} [{Language.ToUpperInvariant()}] ({CodecDisplay})";

    public void RefreshCues()
    {
        Cues.Clear();
        foreach (var cue in _model.Subtitles.Cues)
        {
            Cues.Add(new CueItemViewModel(cue));
        }
        OnPropertyChanged(nameof(CueCount));
    }

    public void SyncModelCues()
    {
        _model.Subtitles.Cues.Clear();
        int idx = 1;
        foreach (var vm in Cues)
        {
            vm.Index = idx++;
            _model.Subtitles.Cues.Add(vm.Model);
        }
        OnPropertyChanged(nameof(CueCount));
    }

    public CueItemViewModel AddCue(TimeSpan start, TimeSpan end, string text = "")
    {
        var cue = new SubtitleCue
        {
            Index = Cues.Count + 1,
            StartTime = start,
            EndTime = end,
            RawText = text
        };
        var vm = new CueItemViewModel(cue);
        Cues.Add(vm);
        _model.Subtitles.Cues.Add(cue);
        OnPropertyChanged(nameof(CueCount));
        return vm;
    }

    public void RemoveCue(CueItemViewModel cueVm)
    {
        if (Cues.Remove(cueVm))
        {
            _model.Subtitles.Cues.Remove(cueVm.Model);
            ReindexCues();
            OnPropertyChanged(nameof(CueCount));
        }
    }

    public void ReindexCues()
    {
        for (int i = 0; i < Cues.Count; i++)
        {
            Cues[i].Index = i + 1;
        }
    }
}
