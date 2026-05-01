using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed class LocalHostSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string repoRoot;

    public LocalHostSnapshotStore(string repoRoot)
    {
        this.repoRoot = repoRoot;
    }

    public HostRuntimeSnapshot? Load()
    {
        var paths = ControlPlanePaths.FromRepoRoot(repoRoot);
        return TryLoadSnapshot(paths.PlatformHostSnapshotLiveStateFile)
            ?? TryLoadSnapshot(paths.PlatformHostSnapshotFile);
    }

    public void Save(HostRuntimeSnapshot snapshot)
    {
        var path = LocalHostPaths.GetSnapshotPath(repoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteAtomically(path, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    private static HostRuntimeSnapshot? TryLoadSnapshot(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return JsonSerializer.Deserialize<HostRuntimeSnapshot>(SharedFileAccess.ReadAllText(path), JsonOptions);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == 2)
                {
                    return null;
                }

                BlockingHostWait.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)));
            }
        }

        return null;
    }

    private static void WriteAtomically(string path, string content)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException($"Path '{path}' has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporaryPath, content);
        File.Move(temporaryPath, path, overwrite: true);
    }
}
