using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeManifestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly AuthoritativeTruthStoreService authoritativeTruthStoreService;

    public RuntimeManifestService(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        authoritativeTruthStoreService = new AuthoritativeTruthStoreService(paths, lockService);
    }

    public string ManifestPath => Path.Combine(paths.AiRoot, "runtime.json");

    public string AuthoritativeManifestPath => authoritativeTruthStoreService.RuntimeManifestFile;

    public RepoRuntimeManifest? Load()
    {
        var payload = authoritativeTruthStoreService.ReadAuthoritativeFirst(AuthoritativeManifestPath, ManifestPath);
        return !string.IsNullOrWhiteSpace(payload)
            ? JsonSerializer.Deserialize<RepoRuntimeManifest>(payload, JsonOptions)
            : null;
    }

    public RepoRuntimeManifest Upsert(
        string repoId,
        string repoPath,
        string gitRoot,
        string runtimeRoot,
        string branch,
        string runtimeVersion,
        string clientVersion,
        string hostSessionId,
        string runtimeStatus,
        string repoSummary,
        RepoRuntimeManifestState state)
    {
        var existing = Load();
        var manifest = new RepoRuntimeManifest
        {
            SchemaVersion = existing?.SchemaVersion ?? 1,
            RepoId = string.IsNullOrWhiteSpace(existing?.RepoId) ? repoId : existing!.RepoId,
            RepoPath = Path.GetFullPath(repoPath),
            GitRoot = Path.GetFullPath(gitRoot),
            RuntimeRoot = Path.GetFullPath(runtimeRoot),
            ActiveBranch = branch,
            RuntimeVersion = runtimeVersion,
            ClientVersion = clientVersion,
            HostSessionId = hostSessionId,
            RuntimeStatus = runtimeStatus,
            RepoSummary = repoSummary,
            State = state,
            CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow,
            LastAttachedAt = DateTimeOffset.UtcNow,
            LastRepairAt = existing?.LastRepairAt,
        };

        Save(manifest);
        return manifest;
    }

    public RepoRuntimeManifest MarkRepair(RepoRuntimeManifest manifest, RepoRuntimeManifestState state, string runtimeStatus, string summary)
    {
        var updated = new RepoRuntimeManifest
        {
            SchemaVersion = manifest.SchemaVersion,
            RepoId = manifest.RepoId,
            RepoPath = manifest.RepoPath,
            GitRoot = manifest.GitRoot,
            RuntimeRoot = manifest.RuntimeRoot,
            ActiveBranch = manifest.ActiveBranch,
            RuntimeVersion = manifest.RuntimeVersion,
            ClientVersion = manifest.ClientVersion,
            HostSessionId = manifest.HostSessionId,
            RuntimeStatus = runtimeStatus,
            RepoSummary = summary,
            State = state,
            CreatedAt = manifest.CreatedAt,
            LastAttachedAt = manifest.LastAttachedAt,
            LastRepairAt = DateTimeOffset.UtcNow,
        };

        Save(updated);
        return updated;
    }

    public void Save(RepoRuntimeManifest manifest)
    {
        authoritativeTruthStoreService.WithWriterLease(AuthoritativeManifestPath, "runtime-manifest-save", () =>
        {
            authoritativeTruthStoreService.WriteAuthoritativeThenMirror(
                AuthoritativeManifestPath,
                ManifestPath,
                JsonSerializer.Serialize(manifest, JsonOptions),
                writerLockHeld: true);
            return 0;
        });
    }
}
