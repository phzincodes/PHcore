using System.Windows;
using System.Windows.Media;

namespace PHsystem.Desktop.Components;

public sealed class CoverageRing : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(CoverageRing), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Brush), typeof(CoverageRing), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(CoverageRing), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Brush Accent { get => (Brush)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public Brush Track { get => (Brush)GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    protected override void OnRender(DrawingContext context)
    {
        const double stroke = 7;
        var radius = (Math.Min(ActualWidth, ActualHeight) - stroke) / 2;
        if (radius <= 0) return;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        context.DrawEllipse(null, new Pen(Track, stroke), center, radius, radius);
        var amount = Math.Clamp(Value, 0, 100);
        if (amount == 0 || !double.IsFinite(amount)) return;
        var pen = new Pen(Accent, stroke) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (amount == 100) { context.DrawEllipse(null, pen, center, radius, radius); return; }
        var angle = amount * Math.Tau / 100;
        var arc = new StreamGeometry();
        using (var geometry = arc.Open())
        {
            geometry.BeginFigure(new Point(center.X, center.Y - radius), false, false);
            geometry.ArcTo(new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle)),
                new Size(radius, radius), 0, amount > 50, SweepDirection.Clockwise, true, false);
        }
        context.DrawGeometry(null, pen, arc);
    }
}
