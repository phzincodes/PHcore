using System.Collections.Immutable;

namespace PHsystem.Cleanup;

public sealed record TempLocation(string Label, string? Path);

public enum TempLocationState { Observed, Partial, Duplicate, Missing, PermissionDenied, UnsupportedPath, Unavailable }

public sealed record TempLocationSummary(string Label, string? Path, TempLocationState State,
    int Files = 0, long Bytes = 0, int ExcludedEntries = 0, string? SameLocationAs = null);

internal static class TempLocationInventory
{
    private const int MaxEntriesPerDirectory = 2_000;

    public static ImmutableArray<TempLocationSummary> Inspect(
        IReadOnlyList<TempLocation> locations, string cleanupRoot, CancellationToken token)
    {
        var paths = new Dictionary<string, string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal)
        {
            [Path.TrimEndingDirectorySeparator(cleanupRoot)] = "Temporários do usuário"
        };
        var results = ImmutableArray.CreateBuilder<TempLocationSummary>(locations.Count);
        foreach (var location in locations)
        {
            token.ThrowIfCancellationRequested();
            var path = NormalizeLocalDirectory(location.Path);
            if (path is null)
                results.Add(new(location.Label, null, string.IsNullOrWhiteSpace(location.Path)
                    ? TempLocationState.Missing
                    : TempLocationState.UnsupportedPath));
            else if (paths.TryGetValue(path, out var existing))
                results.Add(new(location.Label, path, TempLocationState.Duplicate, SameLocationAs: existing));
            else
            {
                paths.Add(path, location.Label);
                results.Add(Observe(location.Label, path, token));
            }
        }
        return results.MoveToImmutable();
    }

    private static string? NormalizeLocalDirectory(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || !Path.IsPathFullyQualified(input)) return null;
        try
        {
            var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(input));
            var volume = Path.GetPathRoot(path)!;
            if (path == volume) return null;
            if (OperatingSystem.IsWindows() && (volume.Length != 3 || new DriveInfo(volume).DriveType == DriveType.Network))
                return null;
            return path;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static TempLocationSummary Observe(string label, string path, CancellationToken token)
    {
        var examined = 0;
        var excluded = 0;
        var files = 0;
        long bytes = 0;
        TempLocationSummary Summary(TempLocationState state) => new(label, path, state, files, bytes, excluded);
        try
        {
            TempCleanupService.EnsureNoLinks(path);
            using var pin = OperatingSystem.IsWindows() ? WindowsDeletionScope.Open(path) : null;
            var directory = new DirectoryInfo(path);
            foreach (var entry in directory.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
            {
                token.ThrowIfCancellationRequested();
                if (examined++ == MaxEntriesPerDirectory) return Summary(TempLocationState.Partial);
                if (entry is not FileInfo file || (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    excluded++;
                    continue;
                }
                bytes += file.Length;
                files++;
            }
            return Summary(TempLocationState.Observed);
        }
        catch (UnauthorizedAccessException) { return Summary(TempLocationState.PermissionDenied); }
        catch (FileNotFoundException) { return Summary(examined == 0 ? TempLocationState.Missing : TempLocationState.Partial); }
        catch (DirectoryNotFoundException) { return Summary(examined == 0 ? TempLocationState.Missing : TempLocationState.Partial); }
        catch (IOException) { return Summary(examined == 0 ? TempLocationState.Unavailable : TempLocationState.Partial); }
    }
}
