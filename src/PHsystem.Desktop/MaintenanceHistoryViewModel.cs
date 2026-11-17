using System.Collections.ObjectModel;
using PHsystem.LocalData;

namespace PHsystem.Desktop;

public sealed record MaintenanceLine(string When, string Tool, string Result);

public sealed partial class MainViewModel
{
    public ObservableCollection<MaintenanceLine> MaintenanceHistory { get; } = [];

    private async Task RefreshMaintenanceHistoryAsync(CancellationToken token)
    {
        if (repository is not IMaintenanceHistory history) return;
        var entries = await history.LoadMaintenanceAsync(cancellationToken: token);
        MaintenanceHistory.Clear();
        foreach (var entry in entries)
            MaintenanceHistory.Add(new(entry.At.ToLocalTime().ToString("g"), entry.Action switch
            {
                MaintenanceAction.WindowsSecurity => "Segurança do Windows",
                MaintenanceAction.WindowsUpdate => "Windows Update",
                MaintenanceAction.StartupApps => "Aplicativos de inicialização",
                _ => "Ferramenta do Windows",
            }, entry.Outcome == MaintenanceOutcome.ToolOpened ? "Ferramenta aberta" : "Falha ao abrir"));
    }
}
