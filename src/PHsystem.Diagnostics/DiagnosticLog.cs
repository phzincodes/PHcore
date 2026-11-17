using Serilog;
using Serilog.Core;

namespace PHsystem.Diagnostics;

public sealed class DiagnosticLog : IDisposable
{
    private readonly Logger? logger;

    public DiagnosticLog(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .WriteTo.File(Path.Combine(directory, "diagnostico-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    fileSizeLimitBytes: 2 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    shared: true)
                .CreateLogger();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A falha do log não impede o diagnóstico local.
        }
    }

    internal void CollectorFailed(Type collector, string reason) =>
        logger?.Warning("Falha de coleta em {CollectorType} ({Reason})", collector.Name, reason);

    internal void SnapshotFailed(Type reason) =>
        logger?.Warning("Falha ao gravar diagnóstico ({Reason})", reason.Name);

    public void Dispose() => logger?.Dispose();
}
