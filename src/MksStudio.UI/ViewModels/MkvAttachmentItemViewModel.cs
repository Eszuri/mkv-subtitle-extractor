using CommunityToolkit.Mvvm.ComponentModel;
using MksStudio.Core.MkvToolNix.Models;

namespace MksStudio.UI.ViewModels;

public partial class MkvAttachmentItemViewModel : ObservableObject
{
    private readonly MkvAttachment _model;
    public MkvAttachment Model => _model;

    [ObservableProperty]
    private bool _isSelected = true;

    public MkvAttachmentItemViewModel(MkvAttachment model)
    {
        _model = model;
    }

    public int Id => _model.Id;
    public string FileName => _model.FileName;
    public string ContentType => _model.ContentType;
    public long Size => _model.Size;
    public string Description => string.IsNullOrWhiteSpace(_model.Description) ? "-" : _model.Description;

    public string FormattedSize
    {
        get
        {
            if (Size < 1024) return $"{Size} B";
            if (Size < 1024 * 1024) return $"{Size / 1024.0:0.#} KB";
            return $"{Size / (1024.0 * 1024.0):0.##} MB";
        }
    }
}
