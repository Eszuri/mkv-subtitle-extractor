using CommunityToolkit.Mvvm.ComponentModel;
using MksStudio.Core.Matroska.Models;

namespace MksStudio.UI.ViewModels;

/// <summary>
/// Observable ViewModel representing an embedded attachment (font or image) in the .mks container.
/// </summary>
public partial class AttachmentItemViewModel : ObservableObject
{
    private readonly MksAttachment _model;
    public MksAttachment Model => _model;

    public AttachmentItemViewModel(MksAttachment model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public string FileName
    {
        get => _model.FileName;
        set
        {
            if (_model.FileName != value)
            {
                _model.FileName = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    public string FileMimeType
    {
        get => _model.FileMimeType;
        set
        {
            if (_model.FileMimeType != value)
            {
                _model.FileMimeType = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    public string? Description
    {
        get => _model.Description;
        set
        {
            if (_model.Description != value)
            {
                _model.Description = value;
                OnPropertyChanged();
            }
        }
    }

    public ulong FileUid => _model.FileUid;
    public long FileSize => _model.FileSize;

    public string FileSizeFormatted
    {
        get
        {
            if (_model.FileSize < 1024)
                return $"{_model.FileSize} B";
            if (_model.FileSize < 1024 * 1024)
                return $"{_model.FileSize / 1024.0:F1} KB";
            return $"{_model.FileSize / (1024.0 * 1024.0):F2} MB";
        }
    }
}
