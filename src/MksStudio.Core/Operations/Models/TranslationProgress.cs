namespace MksStudio.Core.Operations.Models;

public class TranslationProgress
{
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public double Percentage => TotalCount > 0 ? (double)ProcessedCount / TotalCount * 100.0 : 0.0;
    public string StatusMessage { get; set; } = string.Empty;
    public string CurrentText { get; set; } = string.Empty;
    public string TranslatedPreview { get; set; } = string.Empty;
}
