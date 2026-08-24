using CommunityToolkit.Mvvm.ComponentModel;
using MksStudio.Core.MkvToolNix.Models;

namespace MksStudio.UI.ViewModels;

public partial class MkvTrackItemViewModel : ObservableObject
{
    private readonly MkvTrack _model;
    public MkvTrack Model => _model;

    [ObservableProperty]
    private bool _isSelected = true;

    public MkvTrackItemViewModel(MkvTrack model)
    {
        _model = model;
    }

    public int Id => _model.Id;
    public string Codec => _model.Codec ?? _model.CodecId;
    public string Language => _model.Language;
    public string? LanguageIetf => _model.LanguageIetf;
    public string TrackName => _model.TrackName;
    public bool IsDefault => _model.IsDefault;
    public bool IsForced => _model.IsForced;
    public string DefaultExtension => _model.DefaultExtension;

    public string DisplaySummary => $"Track #{Id} | {Codec} | Lang: {Language.ToUpperInvariant()} | {(IsDefault ? "[DEFAULT] " : "")}{(IsForced ? "[FORCED] " : "")}\"{TrackName}\"";
}
