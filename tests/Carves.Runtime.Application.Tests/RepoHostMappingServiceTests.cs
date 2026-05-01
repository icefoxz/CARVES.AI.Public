using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RepoHostMappingServiceTests
{
    [Fact]
    public void RefreshManagedMappings_ReusesStableRepoIdentityWithoutDuplicates()
    {
        using var workspace = new TemporaryWorkspace();
        var repoAlpha = CreateRepo(workspace, "RepoAlpha");
        var repoBeta = CreateRepo(workspace, "RepoBeta");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(repoAlpha, "repo-alpha", "default", "balanced");
        repoRegistry.Register(repoBeta, "repo-beta", "default", "balanced");
        var hostId = PlatformIdentity.CreateHostId(workspace.RootPath, Environment.MachineName);
        var hostRegistry = new HostRegistryService(new JsonHostRegistryRepository(workspace.Paths));
        hostRegistry.Upsert(hostId, Environment.MachineName, "http://127.0.0.1:5010", HostInstanceStatus.Active);
        var repoRuntimeService = new RepoRuntimeService(new JsonRepoRuntimeRegistryRepository(workspace.Paths), hostId);
        var seeded = repoRuntimeService.Upsert(repoAlpha, RepoRuntimeStatus.Idle);
        var mappingService = new RepoHostMappingService(repoRegistry, repoRuntimeService, hostRegistry);

        var refreshed = mappingService.RefreshManagedMappings();
        var registry = repoRuntimeService.List();
        var mappings = mappingService.List();

        Assert.Equal(2, refreshed.Count);
        Assert.Equal(2, registry.Count);
        Assert.Equal(seeded.RepoId, registry.Single(item => string.Equals(item.RepoPath, Path.GetFullPath(repoAlpha), StringComparison.Ordinal)).RepoId);
        Assert.All(mappings, item => Assert.Equal(RepoHostMappingState.Mapped, item.MappingState));
    }

    [Fact]
    public void List_MarksOrphanedAndUnknownRepoStatesFromColdRegistryTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var mappedRepo = CreateRepo(workspace, "MappedRepo");
        var orphanRepo = CreateRepo(workspace, "OrphanRepo");
        var unknownRepo = CreateRepo(workspace, "UnknownRepo");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(mappedRepo, "mapped-repo", "default", "balanced");
        repoRegistry.Register(orphanRepo, "orphan-repo", "default", "balanced");

        var activeHostId = "host-active";
        var unknownHostId = "host-unknown";
        var hostRegistry = new HostRegistryService(new JsonHostRegistryRepository(workspace.Paths));
        hostRegistry.Upsert(activeHostId, Environment.MachineName, "http://127.0.0.1:5010", HostInstanceStatus.Active);
        hostRegistry.Upsert(unknownHostId, Environment.MachineName, "http://127.0.0.1:5011", HostInstanceStatus.Unknown);

        var runtimeRepository = new JsonRepoRuntimeRegistryRepository(workspace.Paths);
        runtimeRepository.Save(new RepoRuntimeRegistry
        {
            Items =
            [
                new RepoRuntime
                {
                    RepoId = PlatformIdentity.CreateRepoRuntimeId(mappedRepo),
                    RepoPath = Path.GetFullPath(mappedRepo),
                    HostId = activeHostId,
                    Status = RepoRuntimeStatus.Active,
                },
                new RepoRuntime
                {
                    RepoId = PlatformIdentity.CreateRepoRuntimeId(orphanRepo),
                    RepoPath = Path.GetFullPath(orphanRepo),
                    HostId = unknownHostId,
                    Status = RepoRuntimeStatus.Idle,
                },
                new RepoRuntime
                {
                    RepoId = PlatformIdentity.CreateRepoRuntimeId(unknownRepo),
                    RepoPath = Path.GetFullPath(unknownRepo),
                    HostId = activeHostId,
                    Status = RepoRuntimeStatus.Idle,
                },
            ],
        });

        var mappingService = new RepoHostMappingService(
            repoRegistry,
            new RepoRuntimeService(runtimeRepository, activeHostId),
            hostRegistry);

        var mappings = mappingService.List();

        Assert.Equal(RepoHostMappingState.Mapped, mappings.Single(item => item.RepoId == PlatformIdentity.CreateRepoRuntimeId(mappedRepo)).MappingState);
        Assert.Equal(RepoHostMappingState.Orphaned, mappings.Single(item => item.RepoId == PlatformIdentity.CreateRepoRuntimeId(orphanRepo)).MappingState);
        Assert.Equal(RepoHostMappingState.UnknownRepo, mappings.Single(item => item.RepoId == PlatformIdentity.CreateRepoRuntimeId(unknownRepo)).MappingState);
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
