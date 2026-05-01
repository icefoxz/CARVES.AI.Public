using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.ControlPlane;

namespace Carves.Runtime.Application.Tests;

public sealed class AuthoritativeTruthStoreServiceTests
{
    [Fact]
    public void BuildSurface_UsesExternalRootAndKnownFamilies()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new AuthoritativeTruthStoreService(workspace.Paths);

        var surface = service.BuildSurface();

        Assert.True(surface.ExternalToRepo);
        Assert.StartsWith("authoritative-truth-store", surface.SurfaceId, StringComparison.Ordinal);
        Assert.Contains(surface.Families, family => family.FamilyId == "task_graph");
        Assert.Contains(surface.Families, family => family.FamilyId == "execution_packets");
        Assert.Contains(surface.Families, family => family.FamilyId == "runtime_manifest");
    }

    [Fact]
    public void BuildSurface_ReportsActiveWriterLeaseWhenHeld()
    {
        using var workspace = new TemporaryWorkspace();
        var lockService = new ControlPlaneLockService(workspace.RootPath);
        var service = new AuthoritativeTruthStoreService(workspace.Paths, lockService);

        using var _ = lockService.Acquire(
            AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope,
            TimeSpan.FromSeconds(1),
            new ControlPlaneLockOptions
            {
                Resource = service.TaskGraphFile.Replace('\\', '/'),
                Operation = "task-graph-save",
            });

        var surface = service.BuildSurface();

        Assert.NotNull(surface.WriterLock);
        var writerLock = surface.WriterLock!;
        Assert.Equal("active", writerLock.State);
        Assert.Equal("task-graph-save", writerLock.Operation);
        Assert.Equal(service.TaskGraphFile.Replace('\\', '/'), writerLock.Resource);
    }

    [Fact]
    public void RuntimeManifestService_SavesAuthoritativeThenLoadsAuthoritativeFirst()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeManifestService(workspace.Paths);
        var truthStore = new AuthoritativeTruthStoreService(workspace.Paths);
        var manifest = new RepoRuntimeManifest
        {
            RepoId = "repo-test",
            RepoPath = workspace.RootPath,
            GitRoot = workspace.RootPath,
            ActiveBranch = "main",
            RuntimeVersion = "v-test",
            ClientVersion = "cli-test",
            HostSessionId = "host-1",
            RuntimeStatus = "healthy",
            RepoSummary = "authoritative manifest",
            State = RepoRuntimeManifestState.Healthy,
        };

        service.Save(manifest);
        File.WriteAllText(
            service.ManifestPath,
            JsonSerializer.Serialize(new RepoRuntimeManifest
            {
                SchemaVersion = manifest.SchemaVersion,
                RepoId = manifest.RepoId,
                RepoPath = manifest.RepoPath,
                GitRoot = manifest.GitRoot,
                ActiveBranch = manifest.ActiveBranch,
                RuntimeVersion = manifest.RuntimeVersion,
                ClientVersion = manifest.ClientVersion,
                HostSessionId = manifest.HostSessionId,
                RepoSummary = "mirror drift should not win",
                RuntimeStatus = manifest.RuntimeStatus,
                State = manifest.State,
                CreatedAt = manifest.CreatedAt,
                LastAttachedAt = manifest.LastAttachedAt,
                LastRepairAt = manifest.LastRepairAt,
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            }));

        var loaded = service.Load();

        Assert.NotNull(loaded);
        Assert.Equal("authoritative manifest", loaded!.RepoSummary);
        Assert.True(File.Exists(truthStore.RuntimeManifestFile));
        Assert.True(File.Exists(service.ManifestPath));
    }

    [Fact]
    public void WriteAuthoritativeThenMirror_RecordsMirrorSyncReceiptForSuccessfulSync()
    {
        using var workspace = new TemporaryWorkspace();
        var truthStore = new AuthoritativeTruthStoreService(workspace.Paths);

        truthStore.WriteAuthoritativeThenMirror(
            truthStore.TaskGraphFile,
            workspace.Paths.TaskGraphFile,
            """{"version":1}""");

        var surface = truthStore.BuildSurface();
        var family = Assert.Single(surface.Families, item => item.FamilyId == "task_graph");

        Assert.Equal("in_sync", family.MirrorSync.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(family.MirrorSync.ReceiptPath));
        Assert.NotNull(family.MirrorSync.LastAuthoritativeWriteAt);
        Assert.NotNull(family.MirrorSync.LastMirrorSyncAttemptAt);
        Assert.NotNull(family.MirrorSync.LastSuccessfulMirrorSyncAt);
        Assert.Contains("synchronized", family.MirrorSync.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSurface_DetectsMirrorDriftWhenMirrorDiverges()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeManifestService(workspace.Paths);
        var truthStore = new AuthoritativeTruthStoreService(workspace.Paths);
        var manifest = new RepoRuntimeManifest
        {
            RepoId = "repo-drift",
            RepoPath = workspace.RootPath,
            GitRoot = workspace.RootPath,
            ActiveBranch = "main",
            RuntimeVersion = "v-test",
            ClientVersion = "cli-test",
            HostSessionId = "host-1",
            RuntimeStatus = "healthy",
            RepoSummary = "authoritative manifest",
            State = RepoRuntimeManifestState.Healthy,
        };

        try
        {
            service.Save(manifest);
            File.WriteAllText(
                service.ManifestPath,
                JsonSerializer.Serialize(new RepoRuntimeManifest
                {
                    SchemaVersion = manifest.SchemaVersion,
                    RepoId = manifest.RepoId,
                    RepoPath = manifest.RepoPath,
                    GitRoot = manifest.GitRoot,
                    ActiveBranch = manifest.ActiveBranch,
                    RuntimeVersion = manifest.RuntimeVersion,
                    ClientVersion = manifest.ClientVersion,
                    HostSessionId = manifest.HostSessionId,
                    RepoSummary = "mirror drifted",
                    RuntimeStatus = manifest.RuntimeStatus,
                    State = manifest.State,
                    CreatedAt = manifest.CreatedAt,
                    LastAttachedAt = manifest.LastAttachedAt,
                    LastRepairAt = manifest.LastRepairAt,
                }, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = true,
                }));

            var surface = truthStore.BuildSurface();
            var runtimeManifestFamily = Assert.Single(surface.Families, family => family.FamilyId == "runtime_manifest");

            Assert.True(runtimeManifestFamily.MirrorDriftDetected);
            Assert.Equal("drifted", runtimeManifestFamily.MirrorState);
            Assert.Contains("drifted", runtimeManifestFamily.MirrorSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("in_sync", runtimeManifestFamily.MirrorSync.Outcome);
        }
        finally
        {
            if (Directory.Exists(truthStore.AuthoritativeRoot))
            {
                Directory.Delete(truthStore.AuthoritativeRoot, true);
            }
        }
    }
}
