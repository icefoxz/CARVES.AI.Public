using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class FleetSnapshotServiceTests
{
    [Fact]
    public void Build_ProjectsHostsReposAndMappingsFromColdRegistryTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var mappedRepo = CreateRepo(workspace, "MappedRepo");
        var orphanRepo = CreateRepo(workspace, "OrphanRepo");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(mappedRepo, "mapped-repo", "default", "balanced");
        repoRegistry.Register(orphanRepo, "orphan-repo", "default", "balanced");

        var activeHostId = "host-active";
        var stoppedHostId = "host-stopped";
        var hostRegistry = new HostRegistryService(new JsonHostRegistryRepository(workspace.Paths));
        hostRegistry.Upsert(activeHostId, Environment.MachineName, "http://127.0.0.1:5010", HostInstanceStatus.Active);
        hostRegistry.Upsert(stoppedHostId, Environment.MachineName, "http://127.0.0.1:5011", HostInstanceStatus.Stopped);

        var runtimeRepository = new JsonRepoRuntimeRegistryRepository(workspace.Paths);
        runtimeRepository.Save(new RepoRuntimeRegistry
        {
            Items =
            [
                new RepoRuntime
                {
                    RepoId = PlatformIdentity.CreateRepoRuntimeId(orphanRepo),
                    RepoPath = Path.GetFullPath(orphanRepo),
                    HostId = "host-missing",
                    Status = RepoRuntimeStatus.Idle,
                },
                new RepoRuntime
                {
                    RepoId = PlatformIdentity.CreateRepoRuntimeId(mappedRepo),
                    RepoPath = Path.GetFullPath(mappedRepo),
                    HostId = activeHostId,
                    Status = RepoRuntimeStatus.Active,
                },
            ],
        });

        var repoRuntimeService = new RepoRuntimeService(runtimeRepository, activeHostId);
        var snapshot = new FleetSnapshotService(
            hostRegistry,
            repoRuntimeService,
            new RepoHostMappingService(repoRegistry, repoRuntimeService, hostRegistry))
            .Build();

        Assert.Equal(["host-active", "host-stopped"], snapshot.Hosts.Select(item => item.HostId).ToArray());
        Assert.Equal(2, snapshot.Repos.Count);
        Assert.Equal(RepoHostMappingState.Mapped, snapshot.Repos.Single(item => item.RepoId == PlatformIdentity.CreateRepoRuntimeId(mappedRepo)).MappingState);
        Assert.Equal(RepoHostMappingState.Orphaned, snapshot.Repos.Single(item => item.RepoId == PlatformIdentity.CreateRepoRuntimeId(orphanRepo)).MappingState);
    }

    private static string CreateRepo(TemporaryWorkspace workspace, string name)
    {
        var repoRoot = Path.Combine(workspace.RootPath, "fleet", name);
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".ai"));
        File.WriteAllText(Path.Combine(repoRoot, ".ai", "STATE.md"), "Runtime stage: Stage-8 multi-repo fleet control plane");
        return repoRoot;
    }
}
