namespace PHsystem.LocalData;

public enum MaintenanceAction { WindowsSecurity = 1, WindowsUpdate = 2, StartupApps = 3 }
public enum MaintenanceOutcome { ToolOpened = 1, OpenFailed = 2 }

public sealed record MaintenanceEntry(DateTimeOffset At, MaintenanceAction Action, MaintenanceOutcome Outcome);

public interface IMaintenanceHistory
{
    Task RecordAsync(MaintenanceEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaintenanceEntry>> LoadMaintenanceAsync(int limit = 20, CancellationToken cancellationToken = default);
}
