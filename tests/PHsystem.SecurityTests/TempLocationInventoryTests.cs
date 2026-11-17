using PHsystem.Cleanup;
using Xunit;

namespace PHsystem.SecurityTests;

public sealed class TempLocationInventoryTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("PHsystem-temporarios-").FullName;

    [Fact]
    public async Task InventoryCountsFilesWithoutAddingThemToTheDeletionPlan()
    {
        var user = Directory.CreateDirectory(Path.Combine(root, "user")).FullName;
        var windows = Directory.CreateDirectory(Path.Combine(root, "windows")).FullName;
        var prefetch = Directory.CreateDirectory(Path.Combine(root, "prefetch")).FullName;
        var nested = Directory.CreateDirectory(Path.Combine(windows, "nested")).FullName;
        var systemFile = Path.Combine(windows, "keep.tmp");
        File.WriteAllBytes(systemFile, new byte[23]);
        File.SetLastWriteTimeUtc(systemFile, DateTime.UtcNow.AddDays(-14));
        File.WriteAllBytes(Path.Combine(nested, "keep.tmp"), new byte[100]);
        File.WriteAllBytes(Path.Combine(prefetch, "keep.pf"), new byte[10]);
        var service = new TempCleanupService(user, additionalLocations:
        [
            new("%TEMP%", user + Path.DirectorySeparatorChar),
            new("Temporários do Windows", windows),
            new("Prefetch", prefetch)
        ]);

        var results = await service.InspectAdditionalLocationsAsync();
        var plan = await service.PreviewAsync();

        Assert.Equal(TempLocationState.Duplicate, results[0].State);
        Assert.Equal(TempLocationState.Observed, results[1].State);
        Assert.Equal(1, results[1].Files);
        Assert.Equal(23, results[1].Bytes);
        Assert.Equal(1, results[1].ExcludedEntries);
        Assert.Equal(10, results[2].Bytes);
        Assert.Empty(plan.Files);
        Assert.True(File.Exists(systemFile));
    }

    [Fact]
    public async Task MissingAndNonLocalLocationsAreDistinctFromAnEmptyDirectory()
    {
        var empty = Directory.CreateDirectory(Path.Combine(root, "empty")).FullName;
        var service = new TempCleanupService(Path.Combine(root, "user"), additionalLocations:
        [
            new("Ausente", Path.Combine(root, "missing")),
            new("Raiz", Path.GetPathRoot(root)),
            new("Relativo", "relative-path"),
            new("Vazio", empty)
        ]);

        var results = await service.InspectAdditionalLocationsAsync();

        Assert.Equal(TempLocationState.Missing, results[0].State);
        Assert.Equal(TempLocationState.UnsupportedPath, results[1].State);
        Assert.Equal(TempLocationState.UnsupportedPath, results[2].State);
        Assert.Equal(TempLocationState.Observed, results[3].State);
        Assert.Equal(0, results[3].Files);
    }

    [Fact]
    public async Task CancellationDoesNotReturnAnEmptySuccessfulInventory()
    {
        var service = new TempCleanupService(Path.Combine(root, "user"), additionalLocations:
            [new("Prefetch", Path.Combine(root, "prefetch"))]);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.InspectAdditionalLocationsAsync(source.Token));
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
