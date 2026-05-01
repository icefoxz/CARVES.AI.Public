using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;
using Carves.Runtime.Infrastructure.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed partial class Stage5PlatformTests
{
    [Fact]
    public void RuntimeInstanceManager_List_PrunesInstancesWithoutRegisteredRepo()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoPrune");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-prune", "default", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var gateway = new LocalRepoRuntimeGateway();
        var repository = new JsonRuntimeInstanceRepository(workspace.Paths);
        repository.Save(
        [
            new Carves.Runtime.Domain.Platform.RuntimeInstance
            {
                RepoId = "repo-prune",
                RepoPath = managedRepo,
                Stage = "Stage-8A fleet discovery and registry completed",
            },
            new Carves.Runtime.Domain.Platform.RuntimeInstance
            {
                RepoId = "repo-orphan",
                RepoPath = Path.Combine(workspace.RootPath, "managed", "RepoOrphan"),
                Stage = "Stage-7 operator OS",
            },
        ]);

        var manager = new RuntimeInstanceManager(
            repository,
            repoRegistry,
            new RepoRuntimeService(
                new JsonRepoRuntimeRegistryRepository(workspace.Paths),
                PlatformIdentity.CreateHostId(workspace.RootPath, Environment.MachineName)),
            gateway,
            governance,
            new RepoTruthProjectionService(gateway));

        var instances = manager.List();
        var persisted = repository.Load();

        Assert.Single(instances);
        Assert.Equal("repo-prune", instances[0].RepoId);
        Assert.Single(persisted);
        Assert.Equal("repo-prune", persisted[0].RepoId);
    }

    [Fact]
    public void RuntimeInstanceManager_Inspect_ReconcilesExistingInstanceToRegisteredRepoTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoReconcile");
        var previousRepoPath = CreateManagedRepo(workspace, "RepoReconcileOld");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-reconcile", "planner-high-context", "manual-review");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var gateway = new LocalRepoRuntimeGateway();
        var repository = new JsonRuntimeInstanceRepository(workspace.Paths);
        repository.Save(
        [
            new Carves.Runtime.Domain.Platform.RuntimeInstance
            {
                RepoId = "repo-reconcile",
                RepoPath = previousRepoPath,
                Stage = "Stage-7 operator OS",
                Status = RuntimeInstanceStatus.Paused,
                ActiveSessionId = "default",
                ProviderBindingId = "default",
                PolicyBindingId = "balanced",
                GatewayMode = RepoRuntimeGatewayMode.Remote,
            },
        ]);

        var manager = new RuntimeInstanceManager(
            repository,
            repoRegistry,
            new RepoRuntimeService(
                new JsonRepoRuntimeRegistryRepository(workspace.Paths),
                PlatformIdentity.CreateHostId(workspace.RootPath, Environment.MachineName)),
            gateway,
            governance,
            new RepoTruthProjectionService(gateway));

        var instance = manager.Inspect("repo-reconcile");
        var persisted = Assert.Single(repository.Load());

        Assert.Equal("repo-reconcile", instance.RepoId);
        Assert.Equal(managedRepo, instance.RepoPath);
        Assert.Equal(managedRepo, persisted.RepoPath);
        Assert.Equal(RuntimeInstanceStatus.Paused, instance.Status);
        Assert.Equal("planner-high-context", instance.ProviderBindingId);
        Assert.Equal("manual-review", instance.PolicyBindingId);
        Assert.Equal(RepoRuntimeGatewayMode.Local, instance.GatewayMode);
        Assert.Equal("Stage-4 continuous development runtime", instance.Stage);
    }
}
