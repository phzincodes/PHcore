using System.IO;
using System.Windows.Interop;
using System.Windows.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using PHsystem.Cleanup;
using PHsystem.Desktop;
using PHsystem.Diagnostics;
using PHsystem.Drivers;
using PHsystem.LocalData;
using PHsystem.Optimizer;
using Xunit;

namespace PHsystem.UiTests;

public sealed class FlaUiNavigationTests
{
    [Fact]
    public async Task ARealWindowExposesItsDashboardAndNavigatesToDiagnostics()
    {
        var ready = new TaskCompletionSource<(MainWindow Window, IntPtr Handle)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var directory = Directory.CreateTempSubdirectory("phsystem-flaui-");
            MainWindow? window = null;
            Exception? failure = null;
            var closed = false;
            try
            {
                var store = new SnapshotStore();
                var repository = new SnapshotRepository(Path.Combine(directory.FullName, "snapshots.db"));
                using var client = new CommunityClient(new SessionVault(Path.Combine(directory.FullName, "session.bin")));
                using var game = new GameModeSession();
                using var community = new CommunityViewModel(store, client);
                var collector = new FixtureCollector();
                var model = new MainViewModel(store, new ScanCoordinator([collector], store, repository), repository,
                    new TempCleanupService(directory.FullName), new RecommendationService(), new DriverInventory(), game, community: community);
                window = new MainWindow(model, communityClient: client) { ShowActivated = false, ShowInTaskbar = false };
                window.Closed += (_, _) =>
                {
                    closed = true;
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                };
                window.Show();
                ready.SetResult((window, new WindowInteropHelper(window).Handle));
                Dispatcher.Run();
            }
            catch (Exception error)
            {
                failure = error;
                ready.TrySetException(error);
            }
            finally
            {
                if (!closed) window?.Close();
                directory.Delete(recursive: true);
                if (failure is null) finished.TrySetResult();
                else finished.TrySetException(failure);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var ui = await ready.Task.WaitAsync(TimeSpan.FromSeconds(20));
        try
        {
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ui.Handle);
            Assert.NotNull(await FindAsync(() => window.FindFirstDescendant(c => c.ByAutomationId("HomeAnalyze"))));
            var navigation = await FindAsync(() => window.FindFirstDescendant(c => c.ByAutomationId("MainNavigation")));
            var item = await FindAsync(() => navigation.FindFirstDescendant(c =>
                c.ByControlType(ControlType.ListItem).And(c.ByName("Diagnóstico"))));
            item.Patterns.SelectionItem.Pattern.Select();
            await FindAsync(() => window.FindFirstDescendant(c => c.ByAutomationId("PageTitle").And(c.ByName("Diagnóstico"))));
        }
        finally
        {
            await ui.Window.Dispatcher.InvokeAsync(ui.Window.Close);
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static async Task<AutomationElement> FindAsync(Func<AutomationElement?> find)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        do
        {
            if (find() is { } item) return item;
            await Task.Delay(75);
        } while (elapsed.Elapsed < TimeSpan.FromSeconds(6));
        throw new InvalidOperationException("Controle esperado não foi exposto pelo UI Automation.");
    }

    private sealed class FixtureCollector : PHsystem.Core.IDiagnosticCollector
    {
        public string Category => "fixture";
        public Task<PHsystem.Core.CategoryResult> CollectAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A navegação não deve executar o coletor.");
    }
}
