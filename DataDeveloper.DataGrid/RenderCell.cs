using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace DataDeveloper.DataGrid;

public class RenderCell : Control
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<RenderCell, string>(nameof(Header), string.Empty);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<RenderCell, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ColumnAlignment> ColumnAlignmentProperty =
        AvaloniaProperty.Register<RenderCell, ColumnAlignment>(nameof(ColumnAlignment), ColumnAlignment.Near);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<RenderCell, FontFamily>(nameof(FontFamily), default(FontFamily));

    private FormattedText? _formattedHeader;
    private FormattedText? _formattedText;

    private static Typeface? _typeface;
    private readonly int _fontSize = 12;
    private readonly double _extraHeaderButtonWidth = 30d;
    private readonly IBrush _backgroundBrush = Brushes.Transparent;
    private readonly Pen _borderPen = new Pen(Brushes.Transparent, 1);
    private readonly IBrush _foregroundBrush = Brushes.White;
    private readonly IBrush _nullForegroundBrush = Brushes.Gray;
    

    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ColumnAlignment ColumnAlignment
    {
        get => GetValue(ColumnAlignmentProperty);
        set => SetValue(ColumnAlignmentProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty || change.Property == HeaderProperty || change.Property == FontFamilyProperty)
        {
            _formattedHeader = null;
            _formattedText = null;
            if (change.Property == FontFamilyProperty)
            {
                _typeface ??= new Typeface(this.FontFamily);
            }
            InvalidateMeasure();
            InvalidateVisual();
        }

        if (change.Property == ColumnAlignmentProperty)
        {
            InvalidateVisual();
        }
    }

    private void EnsureFormattedTexts()
    {
        _formattedHeader ??= new FormattedText(
            Header ?? "",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            _typeface.Value,
            _fontSize,
            _foregroundBrush);

        var textBrush = string.IsNullOrEmpty(Text) ? _nullForegroundBrush : _foregroundBrush;
        _formattedText ??= new FormattedText(
            Text ?? "(null)",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            _typeface.Value,
            _fontSize,
            textBrush);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureFormattedTexts();

        double width = Math.Max(_formattedHeader!.Width, _formattedText!.Width) + _extraHeaderButtonWidth;
        double height = Math.Max(_formattedHeader.Height, _formattedText.Height);

        return new Size(width > 500 ? 500 : width, height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        EnsureFormattedTexts();

        double width = Math.Max(_formattedHeader!.Width, _formattedText!.Width) + _extraHeaderButtonWidth;
        double height = Math.Max(_formattedHeader.Height, _formattedText.Height);

        var rect = new Rect(0, 0, width, height);
        context.DrawRectangle(_backgroundBrush, _borderPen, rect);

        double x = ColumnAlignment switch
        {
            ColumnAlignment.Near => 0,
            ColumnAlignment.Center => (width - _formattedText.Width) / 2,
            ColumnAlignment.Far => width - _formattedText.Width,
            _ => 0
        };

        context.DrawText(_formattedText, new Point(x, 0));
    }
}


