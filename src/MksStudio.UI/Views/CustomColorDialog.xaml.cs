using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MksStudio.UI.Views;

public partial class CustomColorDialog : Window
{
    private static readonly string[] CuratedColors =
    [
        // Vivid Cinema
        "#FFFF00", "#00FFFF", "#00FF00", "#FF3333", "#FFAA00", "#FF00FF", "#FFFFFF", "#E2E8F0",
        // Pastels / Anime
        "#FFFFA0", "#A0FFFF", "#A0FFA0", "#FFA0A0", "#FFD0A0", "#FFA0FF", "#E0E0FF", "#FFFFE0",
        // Neon / Cyber
        "#FFD700", "#00E5FF", "#39FF14", "#FF073A", "#FF6EC7", "#7928CA", "#38BDF8", "#A855F7",
        // Shaded / Classic
        "#FFCC00", "#00B4D8", "#22C55E", "#EF4444", "#F97316", "#EC4899", "#94A3B8", "#475569"
    ];

    private bool _isUpdatingInternally;
    private bool _isTypingHex;

    public string SelectedHex { get; private set; } = "FFFF00";

    public CustomColorDialog(string? initialHex = null, string? subtitleText = null)
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(subtitleText))
        {
            PreviewTextBlock.Text = subtitleText;
        }
        else
        {
            PreviewTextBlock.Text = "Aa Cinema Subtitle Text 123";
        }

        BuildPaletteGrid();

        string init = (initialHex ?? "FFFF00").Trim('#').Trim();
        if (init.Length < 6) init = init.PadRight(6, '0');
        SetColorFromHex(init);
    }

    private void BuildPaletteGrid()
    {
        PaletteWrapPanel.Children.Clear();
        foreach (var hex in CuratedColors)
        {
            var btn = new Button
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ToolTip = hex,
                Tag = hex
            };

            var color = (Color)ColorConverter.ConvertFromString(hex);
            var rect = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };
            btn.Content = rect;
            btn.Click += (s, e) =>
            {
                if (s is Button b && b.Tag is string h)
                {
                    SetColorFromHex(h.Trim('#'));
                }
            };

            PaletteWrapPanel.Children.Add(btn);
        }
    }

    private void SetColorFromHex(string cleanHex)
    {
        if (cleanHex.Length >= 6 &&
            byte.TryParse(cleanHex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
            byte.TryParse(cleanHex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
            byte.TryParse(cleanHex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            _isUpdatingInternally = true;
            try
            {
                SliderR.Value = r;
                SliderG.Value = g;
                SliderB.Value = b;
                UpdateUiPreview(r, g, b);
            }
            finally
            {
                _isUpdatingInternally = false;
            }
        }
    }

    private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingInternally) return;
        byte r = (byte)Math.Clamp((int)SliderR.Value, 0, 255);
        byte g = (byte)Math.Clamp((int)SliderG.Value, 0, 255);
        byte b = (byte)Math.Clamp((int)SliderB.Value, 0, 255);
        UpdateUiPreview(r, g, b);
    }

    private void OnHexInputTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingInternally) return;
        string text = HexInputBox.Text.Trim().Trim('#');
        if (text.Length == 6)
        {
            _isTypingHex = true;
            try
            {
                SetColorFromHex(text);
            }
            finally
            {
                _isTypingHex = false;
            }
        }
    }

    private void UpdateUiPreview(byte r, byte g, byte b)
    {
        SelectedHex = $"{r:X2}{g:X2}{b:X2}";
        string assBgr = $"{b:X2}{g:X2}{r:X2}";

        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        PreviewTextBlock.Foreground = brush;
        HexCodeText.Text = $"HEX: #{SelectedHex}";
        AssTagCodeText.Text = $"ASS TAG: {{\\c&H{assBgr}&}}";

        if (!_isTypingHex)
        {
            _isUpdatingInternally = true;
            try
            {
                HexInputBox.Text = $"#{SelectedHex}";
            }
            finally
            {
                _isUpdatingInternally = false;
            }
        }
    }

    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
