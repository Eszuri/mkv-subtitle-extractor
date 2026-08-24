using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MksStudio.Core.Subtitles;
using MksStudio.Core.Subtitles.Models;

namespace MksStudio.UI.ViewModels;

/// <summary>
/// Observable ViewModel representing a single subtitle line (cue).
/// </summary>
public partial class CueItemViewModel : ObservableObject
{
    private readonly SubtitleCue _model;

    public SubtitleCue Model => _model;

    public CueItemViewModel(SubtitleCue model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public int Index
    {
        get => _model.Index;
        set
        {
            if (_model.Index != value)
            {
                _model.Index = value;
                OnPropertyChanged();
            }
        }
    }

    public TimeSpan StartTime
    {
        get => _model.StartTime;
        set
        {
            if (_model.StartTime != value)
            {
                _model.StartTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StartTimeText));
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(DurationText));
            }
        }
    }

    public TimeSpan EndTime
    {
        get => _model.EndTime;
        set
        {
            if (_model.EndTime != value)
            {
                _model.EndTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EndTimeText));
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(DurationText));
            }
        }
    }

    public TimeSpan Duration => _model.Duration;

    public string StartTimeText
    {
        get => FormatTimecode(_model.StartTime);
        set
        {
            if (TryParseTimecode(value, out var ts))
            {
                StartTime = ts;
            }
        }
    }

    public string EndTimeText
    {
        get => FormatTimecode(_model.EndTime);
        set
        {
            if (TryParseTimecode(value, out var ts))
            {
                EndTime = ts;
            }
        }
    }

    public string DurationText => FormatTimecode(_model.Duration);

    public int Layer
    {
        get => _model.Layer;
        set
        {
            if (_model.Layer != value)
            {
                _model.Layer = value;
                OnPropertyChanged();
            }
        }
    }

    public string Style
    {
        get => _model.Style;
        set
        {
            if (_model.Style != value)
            {
                _model.Style = value ?? "Default";
                OnPropertyChanged();
            }
        }
    }

    public string Actor
    {
        get => _model.Actor;
        set
        {
            if (_model.Actor != value)
            {
                _model.Actor = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    public string Effect
    {
        get => _model.Effect;
        set
        {
            if (_model.Effect != value)
            {
                _model.Effect = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    public string RawText
    {
        get => _model.RawText;
        set
        {
            if (_model.RawText != value)
            {
                _model.RawText = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlainText));
            }
        }
    }

    public string PlainText => _model.PlainText;

    public void NotifyTimeChanged()
    {
        OnPropertyChanged(nameof(StartTime));
        OnPropertyChanged(nameof(EndTime));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(StartTimeText));
        OnPropertyChanged(nameof(EndTimeText));
        OnPropertyChanged(nameof(DurationText));
    }

    private static string FormatTimecode(TimeSpan time)
    {
        int hours = (int)time.TotalHours;
        int minutes = time.Minutes;
        int seconds = time.Seconds;
        int milliseconds = time.Milliseconds;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
    }

    private static bool TryParseTimecode(string text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string clean = text.Trim().Replace(',', '.');
        string[] parts = clean.Split(':');
        if (parts.Length < 2) return false;

        try
        {
            int h = 0, m = 0, s = 0, ms = 0;
            if (parts.Length == 3)
            {
                h = int.Parse(parts[0], CultureInfo.InvariantCulture);
                m = int.Parse(parts[1], CultureInfo.InvariantCulture);
                string[] secParts = parts[2].Split('.');
                s = int.Parse(secParts[0], CultureInfo.InvariantCulture);
                if (secParts.Length > 1)
                {
                    string msStr = secParts[1].PadRight(3, '0');
                    if (msStr.Length > 3) msStr = msStr.Substring(0, 3);
                    ms = int.Parse(msStr, CultureInfo.InvariantCulture);
                }
            }
            else if (parts.Length == 2)
            {
                m = int.Parse(parts[0], CultureInfo.InvariantCulture);
                string[] secParts = parts[1].Split('.');
                s = int.Parse(secParts[0], CultureInfo.InvariantCulture);
                if (secParts.Length > 1)
                {
                    string msStr = secParts[1].PadRight(3, '0');
                    if (msStr.Length > 3) msStr = msStr.Substring(0, 3);
                    ms = int.Parse(msStr, CultureInfo.InvariantCulture);
                }
            }

            time = new TimeSpan(0, h, m, s, ms);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
