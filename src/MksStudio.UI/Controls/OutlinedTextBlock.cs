using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace MksStudio.UI.Controls;

/// <summary>
/// A specialized subtitle rendering element that produces cinema-authentic text with:
/// - True vector stroke / outline with rounded line joins (matching Aegisub / mpv / libass)
/// - Directional drop shadow
/// - High-fidelity multi-color and multi-style rich text rendering
/// - Support for ASS override tags ({\c&HBBGGRR&}, {\b1}, {\i1}, {\u1}, {\s1}, {\an1}..{\an9}, \N, \h)
/// - Real-time synchronization with text editor
/// </summary>
public class OutlinedTextBlock : FrameworkElement
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(
            nameof(Fill),
            typeof(Brush),
            typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(
            nameof(Stroke),
            typeof(Brush),
            typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(2.5, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowColorProperty =
        DependencyProperty.Register(
            nameof(ShadowColor),
            typeof(Color),
            typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(Color.FromArgb(190, 0, 0, 0), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowDepthProperty =
        DependencyProperty.Register(
            nameof(ShadowDepth),
            typeof(double),
            typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(
            nameof(FontFamily),
            typeof(FontFamily),
            typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(new FontFamily("Arial, Segoe UI"), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(
            nameof(FontSize),
            typeof(double),
            typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(22.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontWeightProperty =
        DependencyProperty.Register(
            nameof(FontWeight),
            typeof(FontWeight),
            typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontStyleProperty =
        DependencyProperty.Register(
            nameof(FontStyle),
            typeof(FontStyle),
            typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(FontStyles.Normal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextAlignmentProperty =
        DependencyProperty.Register(
            nameof(TextAlignment),
            typeof(TextAlignment),
            typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(TextAlignment.Center, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(
            nameof(TextWrapping),
            typeof(TextWrapping),
            typeof(OutlinedTextBlock),
            new FrameworkPropertyMetadata(TextWrapping.Wrap, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Color ShadowColor
    {
        get => (Color)GetValue(ShadowColorProperty);
        set => SetValue(ShadowColorProperty, value);
    }

    public double ShadowDepth
    {
        get => (double)GetValue(ShadowDepthProperty);
        set => SetValue(ShadowDepthProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontStyle FontStyle
    {
        get => (FontStyle)GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    public TextAlignment TextAlignment
    {
        get => (TextAlignment)GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public TextWrapping TextWrapping
    {
        get => (TextWrapping)GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    private FormattedText? CreateFormattedText(double maxConstraintWidth, out AssParseResult parseResult)
    {
        parseResult = AssTagParser.Parse(Text, FontWeight, FontStyle, Fill, FontSize);
        if (string.IsNullOrEmpty(parseResult.CleanText)) return null;

        var typeface = new Typeface(FontFamily, FontStyle, FontWeights.Normal, FontStretches.Normal);

        double pixelsPerDip = 1.0;
        try
        {
            pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        }
        catch
        {
            pixelsPerDip = 1.0;
        }

        var effectiveAlignment = TextAlignment;
        if (parseResult.Alignment.HasValue)
        {
            int a = parseResult.Alignment.Value;
            if (a == 1 || a == 4 || a == 7) effectiveAlignment = TextAlignment.Left;
            else if (a == 3 || a == 6 || a == 9) effectiveAlignment = TextAlignment.Right;
            else effectiveAlignment = TextAlignment.Center;
        }

        var ft = new FormattedText(
            parseResult.CleanText,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            Math.Max(1.0, FontSize),
            Fill ?? Brushes.White,
            pixelsPerDip)
        {
            TextAlignment = effectiveAlignment
        };

        double pad = (StrokeThickness * 2) + Math.Abs(ShadowDepth) + 16;
        if (TextWrapping == TextWrapping.Wrap && maxConstraintWidth > pad && !double.IsInfinity(maxConstraintWidth))
        {
            ft.MaxTextWidth = Math.Max(10.0, maxConstraintWidth - pad);
        }

        foreach (var span in parseResult.Spans)
        {
            if (span.Start >= 0 && span.Start + span.Length <= parseResult.CleanText.Length && span.Length > 0)
            {
                ft.SetFontWeight(span.FontWeight, span.Start, span.Length);
                ft.SetFontStyle(span.FontStyle, span.Start, span.Length);
                if (span.ForegroundBrush != null)
                {
                    ft.SetForegroundBrush(span.ForegroundBrush, span.Start, span.Length);
                }
                if (span.FontSize.HasValue && span.FontSize.Value > 0)
                {
                    ft.SetFontSize(span.FontSize.Value, span.Start, span.Length);
                }
                if (span.Underline && span.Strikethrough)
                {
                    var col = new TextDecorationCollection();
                    col.Add(TextDecorations.Underline);
                    col.Add(TextDecorations.Strikethrough);
                    ft.SetTextDecorations(col, span.Start, span.Length);
                }
                else if (span.Underline)
                {
                    ft.SetTextDecorations(TextDecorations.Underline, span.Start, span.Length);
                }
                else if (span.Strikethrough)
                {
                    ft.SetTextDecorations(TextDecorations.Strikethrough, span.Start, span.Length);
                }
            }
        }

        return ft;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return new Size(0, 0);

        var ft = CreateFormattedText(availableSize.Width, out _);
        if (ft == null)
            return new Size(0, 0);

        double pad = (StrokeThickness * 2) + Math.Abs(ShadowDepth) + 16;
        double width = ft.WidthIncludingTrailingWhitespace + pad;
        double height = ft.Height + pad;

        if (!double.IsInfinity(availableSize.Width))
        {
            width = Math.Min(width, availableSize.Width);
        }

        return new Size(Math.Max(0, width), Math.Max(0, height));
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (string.IsNullOrWhiteSpace(Text)) return;

        double layoutWidth = ActualWidth > 0 ? ActualWidth : RenderSize.Width;
        double layoutHeight = ActualHeight > 0 ? ActualHeight : RenderSize.Height;
        var ft = CreateFormattedText(layoutWidth, out var parseResult);
        if (ft == null) return;

        double pad = (StrokeThickness * 2) + Math.Abs(ShadowDepth) + 16;
        double originX = StrokeThickness + 8;
        if (ft.MaxTextWidth > 0)
        {
            originX = pad / 2.0;
        }

        // Calculate vertical position based on ASS \an alignment or default
        double originY = StrokeThickness + 8;
        if (layoutHeight > ft.Height + pad)
        {
            int a = parseResult.Alignment ?? -1;
            if (a == 7 || a == 8 || a == 9) // Top of screen
            {
                originY = 16.0;
            }
            else if (a == 4 || a == 5 || a == 6) // Middle of screen
            {
                originY = Math.Max(16.0, (layoutHeight - ft.Height) / 2.0);
            }
            else // Bottom of screen (a == 1, 2, 3 or default)
            {
                originY = Math.Max(16.0, layoutHeight - ft.Height - pad - 8.0);
            }
        }

        var origin = new Point(originX, originY);
        var geom = ft.BuildGeometry(origin);
        if (geom == null) return;

        // 1. Draw Drop Shadow
        if (ShadowDepth > 0 && ShadowColor.A > 0)
        {
            var shadowBrush = parseResult.OverrideShadowBrush ?? new SolidColorBrush(ShadowColor);
            if (shadowBrush.CanFreeze) shadowBrush.Freeze();

            var shadowPen = StrokeThickness > 0
                ? new Pen(shadowBrush, StrokeThickness * 2)
                {
                    LineJoin = PenLineJoin.Round,
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                }
                : null;
            if (shadowPen?.CanFreeze == true) shadowPen.Freeze();

            var shadowTransform = new TranslateTransform(ShadowDepth, ShadowDepth);
            dc.PushTransform(shadowTransform);
            if (shadowPen != null)
            {
                dc.DrawGeometry(shadowBrush, shadowPen, geom);
            }
            else
            {
                dc.DrawGeometry(shadowBrush, null, geom);
            }
            dc.Pop();
        }

        // 2. Draw Solid Cinema Stroke Outline behind text
        var strokeBrush = parseResult.OverrideStrokeBrush ?? Stroke ?? Brushes.Black;
        if (StrokeThickness > 0 && strokeBrush != null)
        {
            var strokePen = new Pen(strokeBrush, StrokeThickness * 2)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            if (strokePen.CanFreeze) strokePen.Freeze();
            dc.DrawGeometry(null, strokePen, geom);
        }

        // 3. Draw Crisp Multi-Color, Multi-Style Rich Text Fill on top
        dc.DrawText(ft, origin);
    }
}
