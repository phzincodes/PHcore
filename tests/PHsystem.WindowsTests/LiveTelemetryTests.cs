using System.Net;
using PHsystem.Windows;
using Xunit;

namespace PHsystem.WindowsTests;

public sealed class LiveTelemetryTests
{
    [Fact]
    public void SharedReadingIncludesActualMemoryAndNullableSensors()
    {
        using var telemetry = new LiveTelemetry();
        var reading = telemetry.Read();
        Assert.Same(reading, telemetry.Read());
        Assert.NotNull(reading.TotalMemoryBytes);
        Assert.True(reading.TotalMemoryBytes > 0);
        Assert.NotNull(reading.MemoryUsage);
        Assert.InRange(reading.MemoryUsage.Value, 0, 100);
        if (reading.GpuUsage is { } usage) Assert.InRange(usage, 0, 100);
        if (reading.CpuTemperature is { } temperature) Assert.InRange(temperature, 1, 150);
    }

    [Fact]
    public async Task WindowsPingCapturesLoopbackOutputAndHonorsCancellation()
    {
        var result = await WindowsPingCommand.RunAsync(IPAddress.Loopback, CancellationToken.None);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("127.0.0.1", result.Output);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            WindowsPingCommand.RunAsync(IPAddress.Loopback, cancelled.Token));
    }
}
