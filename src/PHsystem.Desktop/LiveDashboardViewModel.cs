using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using PHsystem.Windows;
using SkiaSharp;

namespace PHsystem.Desktop;

public sealed class ResourceReading(string title, string shortName, string defaultDetail)
    : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private double? value;
    private string detail = defaultDetail;
    public string Title { get; } = title;
    public string ShortName { get; } = shortName;
    public string ValueLabel => Value is { } number ? $"{number:0}%" : "Indisponível";
    public bool HasValue => Value.HasValue;
    public double? Value
    {
        get => value;
        internal set
        {
            if (!SetProperty(ref this.value, value)) return;
            OnPropertyChanged(nameof(ValueLabel));
            OnPropertyChanged(nameof(HasValue));
        }
    }
    public string Detail { get => detail; internal set => SetProperty(ref detail, value); }
    internal string DefaultDetail { get; } = defaultDetail;
}

public sealed class LiveDashboardViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private readonly LiveTelemetry? source;
    private readonly DispatcherTimer? timer;
    private readonly Queue<LiveHardwareSample> readings = new();
    private readonly ObservableCollection<ObservablePoint> points = [];
    private readonly LineSeries<ObservablePoint> line;
    private readonly Axis yAxis;
    private readonly Axis xAxis;
    private ResourceReading selectedMetric;
    private DateTimeOffset? latest;
    private bool active;
    private bool paused;
    private bool pending;
    private bool readFailed;

    public LiveDashboardViewModel(LiveTelemetry? source = null)
    {
        this.source = source;
        Metrics = [new("Processador", "CPU", "Uso total do processador"),
            new("Placa de vídeo", "GPU", "Adaptador e temperatura"),
            new("Memória", "RAM", "Memória física em uso"),
            new("Disco do sistema", "Disco", "Espaço ocupado no volume do Windows")];
        selectedMetric = Metrics[0];
        line = new LineSeries<ObservablePoint> { Values = points, GeometrySize = 0, LineSmoothness = 0.14 };
        yAxis = new Axis { MinLimit = 0, MaxLimit = 100, MinStep = 25, ForceStepToMin = true, Labeler = value => $"{value:0}%" };
        xAxis = new Axis { IsVisible = false };
        Series = [line];
        YAxes = [yAxis];
        XAxes = [xAxis];
        ToggleCommand = new RelayCommand(Toggle, () => this.source is not null);
        if (source is not null)
        {
            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => _ = ReadAsync();
        }
    }

    public IReadOnlyList<ResourceReading> Metrics { get; }
    public IReadOnlyList<ISeries> Series { get; }
    public IReadOnlyList<Axis> XAxes { get; }
    public IReadOnlyList<Axis> YAxes { get; }
    public event EventHandler? ChartChanged;
    public bool HasSamples => latest.HasValue;
    public IRelayCommand ToggleCommand { get; }
    public string ToggleLabel => paused ? "Retomar" : "Pausar";
    public string LiveStatus => paused ? "Pausado" : readFailed ? "Indisponível" : latest.HasValue ? "Ao vivo" : "Aguardando medição";
    public string SampleStatus => latest is { } timestamp ? $"Última leitura às {timestamp.ToLocalTime():HH:mm:ss}" : "As leituras aparecem quando o Windows fornece dados.";
    public ResourceReading SelectedMetric
    {
        get => selectedMetric;
        set
        {
            if (value is null || !Metrics.Contains(value) || !SetProperty(ref selectedMetric, value)) return;
            points.Clear();
            foreach (var reading in readings) AddPoint(reading);
            ChartChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetActive(bool visible)
    {
        if (active == visible) return;
        active = visible;
        UpdateTimer();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        var accent = SystemParameters.HighContrast ? SKColors.Yellow : SKColor.Parse(theme.Accent);
        var muted = SystemParameters.HighContrast ? SKColors.White : SKColor.Parse(theme.Secondary);
        var grid = SystemParameters.HighContrast ? SKColors.Gray : SKColor.Parse(theme.Border);
        line.Stroke = new SolidColorPaint(accent, 2.5f);
        line.Fill = new SolidColorPaint(accent.WithAlpha(30));
        yAxis.LabelsPaint = new SolidColorPaint(muted);
        yAxis.SeparatorsPaint = new SolidColorPaint(grid, 1);
        ChartChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Toggle()
    {
        paused = !paused;
        OnPropertyChanged(nameof(ToggleLabel));
        OnPropertyChanged(nameof(LiveStatus));
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        if (timer is null) return;
        if (active && !paused)
        {
            timer.Start();
            _ = ReadAsync();
        }
        else timer.Stop();
    }

    private async Task ReadAsync()
    {
        if (source is null || !active || paused || pending) return;
        pending = true;
        try
        {
            var sample = await Task.Run(source.Read);
            if (!active || paused || sample.At == latest) return;
            readFailed = false;
            latest = sample.At;
            Metrics[0].Value = sample.CpuUsage;
            Metrics[0].Detail = sample.CpuTemperature is { } cpu ? $"Temperatura reportada {cpu:0.#} °C" : "Temperatura indisponível";
            Metrics[1].Value = sample.GpuUsage;
            Metrics[1].Detail = $"{sample.GpuModel ?? "Adaptador indisponível"} · " +
                (sample.GpuTemperature is { } gpu ? $"{gpu:0.#} °C" : "temperatura indisponível");
            Metrics[2].Value = sample.MemoryUsage;
            Metrics[2].Detail = sample.TotalMemoryBytes is { } memory && memory <= long.MaxValue
                ? $"{DisplayFormat.Bytes((long)memory)} de memória física" : Metrics[2].DefaultDetail;
            Metrics[3].Value = sample.DiskUsage;
            Metrics[3].Detail = sample.DiskFreeBytes is { } disk
                ? $"{DisplayFormat.Bytes(disk)} livres no volume do Windows" : Metrics[3].DefaultDetail;

            readings.Enqueue(sample);
            AddPoint(sample);
            var cutoff = sample.At.AddSeconds(-60);
            while (readings.Count > 60 || readings.Peek().At < cutoff)
            {
                readings.Dequeue();
                points.RemoveAt(0);
            }
            var now = sample.At.ToUnixTimeMilliseconds() * 0.001;
            xAxis.MinLimit = now - 60;
            xAxis.MaxLimit = now + 1;
            OnPropertyChanged(nameof(LiveStatus));
            OnPropertyChanged(nameof(SampleStatus));
            ChartChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (ObjectDisposedException) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            readFailed = true;
            foreach (var metric in Metrics) metric.Value = null;
            OnPropertyChanged(nameof(LiveStatus));
        }
        finally { pending = false; }
    }

    private void AddPoint(LiveHardwareSample sample)
    {
        double? value = SelectedMetric.ShortName switch
        {
            "CPU" => sample.CpuUsage,
            "GPU" => sample.GpuUsage,
            "RAM" => sample.MemoryUsage,
            _ => sample.DiskUsage,
        };
        points.Add(new ObservablePoint(sample.At.ToUnixTimeMilliseconds() * 0.001, value));
    }
}
