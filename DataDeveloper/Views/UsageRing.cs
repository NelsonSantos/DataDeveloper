using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DataDeveloper.Views;

/// <summary>A small circular progress indicator: a faint track with an arc that grows clockwise from the top.</summary>
public sealed class UsageRing : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<UsageRing, double>(nameof(Fraction));

    public static readonly StyledProperty<bool> IsWarningProperty =
        AvaloniaProperty.Register<UsageRing, bool>(nameof(IsWarning));

    private static readonly IBrush TrackBrush = new SolidColorBrush(Color.Parse("#40808080"));
    private static readonly IBrush NormalBrush = new SolidColorBrush(Color.Parse("#5B8DEF"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#E5A03C"));

    private const double Thickness = 2.5;

    static UsageRing()
    {
        AffectsRender<UsageRing>(FractionProperty, IsWarningProperty);
        AffectsMeasure<UsageRing>(WidthProperty, HeightProperty);
    }

    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public bool IsWarning
    {
        get => GetValue(IsWarningProperty);
        set => SetValue(IsWarningProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= Thickness * 2)
            return;

        var radius = (size - Thickness) / 2;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        context.DrawEllipse(null, new Pen(TrackBrush, Thickness), center, radius, radius);

        var fraction = Math.Clamp(Fraction, 0, 1);
        if (fraction <= 0)
            return;

        // Even a tiny share shows as a visible dot of arc instead of nothing.
        fraction = Math.Max(fraction, 0.03);
        var pen = new Pen(IsWarning ? WarningBrush : NormalBrush, Thickness, lineCap: PenLineCap.Round);

        if (fraction >= 0.999)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        var angle = fraction * 2 * Math.PI;
        var start = new Point(center.X, center.Y - radius);
        var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));

        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(start, isFilled: false);
            geometryContext.ArcTo(end, new Size(radius, radius), 0, fraction > 0.5, SweepDirection.Clockwise);
            geometryContext.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }
}
