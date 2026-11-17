using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiveChartsCore.SkiaSharpView.SKCharts;
using SkiaSharp;

namespace PHsystem.Desktop.Components;

public sealed class TelemetryChart : FrameworkElement
{
    private LiveDashboardViewModel? model;
    private SKCartesianChart? chart;
    private WriteableBitmap? bitmap;

    public TelemetryChart()
    {
        Loaded += (_, _) => Connect();
        Unloaded += (_, _) => Disconnect();
        DataContextChanged += (_, _) => { if (IsLoaded) Connect(); };
        SizeChanged += (_, _) => DrawChart();
        IsVisibleChanged += (_, _) => DrawChart();
    }

    private void Connect()
    {
        Disconnect();
        model = DataContext as LiveDashboardViewModel;
        if (model is not null) model.ChartChanged += ModelChanged;
        DrawChart();
    }

    private void Disconnect()
    {
        if (model is not null) model.ChartChanged -= ModelChanged;
        model = null;
        chart = null;
        bitmap = null;
        InvalidateVisual();
    }

    private void ModelChanged(object? sender, EventArgs change) => DrawChart();

    private void DrawChart()
    {
        if (model is not { HasSamples: true } || !IsVisible || ActualWidth < 24 || ActualHeight < 24) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = (int)Math.Clamp(Math.Ceiling(ActualWidth * dpi.DpiScaleX), 24, 1920);
        var height = (int)Math.Clamp(Math.Ceiling(ActualHeight * dpi.DpiScaleY), 24, 480);
        chart ??= new SKCartesianChart
        {
            Background = SKColors.Transparent,
            Series = model.Series,
            XAxes = model.XAxes,
            YAxes = model.YAxes,
        };
        chart.Width = width;
        chart.Height = height;
        if (bitmap is null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
            bitmap = new WriteableBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32, null);

        bitmap.Lock();
        try
        {
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, bitmap.BackBuffer, bitmap.BackBufferStride);
            if (surface is null) return;
            surface.Canvas.Clear(SKColors.Transparent);
            chart.DrawOnCanvas(surface.Canvas);
            surface.Canvas.Flush();
            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally { bitmap.Unlock(); }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        if (bitmap is not null) context.DrawImage(bitmap, new Rect(RenderSize));
    }
}
