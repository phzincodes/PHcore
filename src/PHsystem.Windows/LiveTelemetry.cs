using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace PHsystem.Windows;

public sealed record LiveHardwareSample(DateTimeOffset At, double? CpuUsage, double? MemoryUsage,
    double? GpuUsage, double? DiskUsage, double? CpuTemperature, double? GpuTemperature, string? GpuModel,
    ulong? TotalMemoryBytes, long? DiskFreeBytes);

/// <summary>One local source for dashboard, hardware and on-demand diagnostics.</summary>
public sealed class LiveTelemetry : IDisposable
{
    private readonly object gate = new();
    private readonly SensorReader sensors = new();
    private LiveHardwareSample? latest;
    private long lastSample;
    private long lastSensors;
    private long lastDisk;
    private CpuTimes? previousCpu;
    private bool disposed;
    private (double? CpuTemperature, double? GpuTemperature, double? GpuUsage, string? GpuModel) sensorValues;
    private (double? Usage, long? Free) diskValues;

    public LiveHardwareSample Read()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var elapsed = Stopwatch.GetElapsedTime(lastSample);
            if (latest is not null && elapsed < TimeSpan.FromMilliseconds(900)) return latest;
            var cpu = ReadCpu(elapsed);
            var memory = WindowsCollectors.TryReadMemory();
            if (latest is null || Stopwatch.GetElapsedTime(lastSensors) >= TimeSpan.FromSeconds(3))
            {
                sensorValues = sensors.Read();
                lastSensors = Stopwatch.GetTimestamp();
            }
            if (latest is null || Stopwatch.GetElapsedTime(lastDisk) >= TimeSpan.FromSeconds(5))
            {
                diskValues = ReadSystemVolume();
                lastDisk = Stopwatch.GetTimestamp();
            }
            lastSample = Stopwatch.GetTimestamp();
            return latest = new(DateTimeOffset.UtcNow, cpu, memory?.MemoryLoad, sensorValues.GpuUsage, diskValues.Usage,
                sensorValues.CpuTemperature, sensorValues.GpuTemperature, sensorValues.GpuModel,
                memory?.TotalPhysical, diskValues.Free);
        }
    }

    private double? ReadCpu(TimeSpan elapsed)
    {
        if (GetActiveProcessorCount(ushort.MaxValue) > 64 || !GetSystemTimes(out var idle, out var kernel, out var user))
        {
            previousCpu = null;
            return null;
        }
        var now = new CpuTimes(idle.Ticks, kernel.Ticks, user.Ticks);
        var previous = previousCpu;
        previousCpu = now;
        if (previous is null || elapsed > TimeSpan.FromSeconds(5)) return null;
        if (now.Idle < previous.Idle || now.Kernel < previous.Kernel || now.User < previous.User) return null;
        var total = (now.Kernel - previous.Kernel) + (double)(now.User - previous.User);
        var idleDelta = now.Idle - previous.Idle;
        return total <= 0 || idleDelta > total ? null : (total - idleDelta) * 100 / total;
    }

    private static (double?, long?) ReadSystemVolume()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (root is null) return (null, null);
            var volume = new DriveInfo(root);
            var size = volume.TotalSize;
            var free = volume.TotalFreeSpace;
            return size <= 0 || free < 0 || free > size ? (null, null) : ((size - free) * 100d / size, free);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (null, null);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            sensors.Dispose();
        }
    }

    private sealed record CpuTimes(ulong Idle, ulong Kernel, ulong User);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low, High;
        public readonly ulong Ticks => ((ulong)High << 32) | Low;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [DllImport("kernel32.dll")]
    private static extern uint GetActiveProcessorCount(ushort group);

    private sealed class SensorReader : IDisposable
    {
        private Computer? computer;
        private bool initializationFailed;

        public (double? CpuTemperature, double? GpuTemperature, double? GpuUsage, string? GpuModel) Read()
        {
            if (initializationFailed) return default;
            if (computer is null)
            {
                computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
                try { computer.Open(); }
                catch (Exception error) when (IsHardwareUnavailable(error))
                {
                    initializationFailed = true;
                    Dispose();
                    return default;
                }
            }

            double? cpuTemperature = null;
            double? gpuTemperature = null;
            double? gpuUsage = null;
            string? gpuModel = null;
            foreach (var device in computer.Hardware)
            {
                try
                {
                    device.Update();
                    var current = device.Sensors;
                    if (device.HardwareType == HardwareType.Cpu)
                        cpuTemperature = Max(cpuTemperature, Temperature(current));
                    else if (device.HardwareType is HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia)
                    {
                        var load = current.Where(sensor => sensor.SensorType == SensorType.Load
                                && sensor.Name is "GPU Core" or "D3D 3D")
                            .Select(sensor => Bounded(sensor.Value, 0, 100)).Max();
                        if (gpuModel is null || load > (gpuUsage ?? -1))
                        {
                            gpuModel = device.Name;
                            gpuUsage = load;
                            gpuTemperature = Temperature(current.Where(sensor =>
                                sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                                sensor.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase)));
                        }
                    }
                }
                catch (Exception error) when (IsHardwareUnavailable(error))
                {
                    // Um sensor inacessível não invalida as leituras dos demais dispositivos.
                }
            }
            return (cpuTemperature, gpuTemperature, gpuUsage, gpuModel);
        }

        private static double? Temperature(IEnumerable<ISensor> sensors) => sensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature)
            .Select(sensor => Bounded(sensor.Value, 1, 150)).Max();

        private static double? Bounded(float? value, double min, double max) =>
            value is { } number && float.IsFinite(number) && number >= min && number <= max ? number : null;

        private static double? Max(double? first, double? second) =>
            first is null ? second : second is null ? first : Math.Max(first.Value, second.Value);

        private static bool IsHardwareUnavailable(Exception error) => error is
            IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException or NotSupportedException
            or TypeInitializationException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException;

        public void Dispose()
        {
            try { computer?.Close(); }
            catch (Exception error) when (IsHardwareUnavailable(error)) { }
            finally { computer = null; }
        }
    }
}
