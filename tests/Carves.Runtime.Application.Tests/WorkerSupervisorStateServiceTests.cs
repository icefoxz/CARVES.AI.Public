using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class WorkerSupervisorStateServiceTests
{
    [Fact]
    public void RegisterPendingLaunch_PersistsSupervisorStateWithoutRawLaunchToken()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonWorkerSupervisorStateRepository(workspace.Paths);
        var service = new WorkerSupervisorStateService(repository);

        var launch = service.RegisterPendingLaunch(
            "repo-supervisor-s1",
            "codex-worker-supervised",
            "phase S1 pending supervised worker launch",
            workerInstanceId: "worker-instance-s1",
            hostSessionId: "host-session-s1",
            actorSessionId: "actor-worker-s1",
            providerProfile: "codex-cli",
            capabilityProfile: "external-codex-worker",
            scheduleBinding: "worker-evidence-ping",
            launchTokenTtl: TimeSpan.FromMinutes(3));

        var snapshot = repository.Load();
        var record = Assert.Single(snapshot.Entries);
        var persistedJson = File.ReadAllText(workspace.Paths.PlatformWorkerSupervisorStateLiveStateFile);

        Assert.Equal("worker-instance-s1", launch.WorkerInstanceId);
        Assert.Equal("repo-supervisor-s1", launch.RepoId);
        Assert.Equal("codex-worker-supervised", launch.WorkerIdentity);
        Assert.StartsWith("wlt-", launch.LaunchTokenId, StringComparison.Ordinal);
        Assert.StartsWith($"{launch.LaunchTokenId}.", launch.LaunchToken, StringComparison.Ordinal);
        Assert.DoesNotContain(launch.LaunchToken, persistedJson, StringComparison.Ordinal);
        Assert.Contains("\"launch_token_hash\"", persistedJson, StringComparison.Ordinal);
        Assert.Contains("\"ownership_mode\": \"host_owned\"", persistedJson, StringComparison.Ordinal);
        Assert.Contains("\"state\": \"requested\"", persistedJson, StringComparison.Ordinal);

        Assert.Equal(WorkerSupervisorOwnershipMode.HostOwned, record.OwnershipMode);
        Assert.Equal(WorkerSupervisorInstanceState.Requested, record.State);
        Assert.Equal("host-session-s1", record.HostSessionId);
        Assert.Equal("actor-worker-s1", record.ActorSessionId);
        Assert.Equal("codex-cli", record.ProviderProfile);
        Assert.Equal("external-codex-worker", record.CapabilityProfile);
        Assert.Equal("worker-evidence-ping", record.ScheduleBinding);
        Assert.Equal(launch.LaunchTokenId, record.LaunchTokenId);
        Assert.NotEqual(launch.LaunchToken, record.LaunchTokenHash);
        Assert.True(service.VerifyLaunchToken(record, launch.LaunchToken));
        Assert.False(service.VerifyLaunchToken(record, "wrong-token"));
    }

    [Fact]
    public void RegisterPendingLaunch_RejectsExistingWorkerInstanceAndPreservesSnapshot()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonWorkerSupervisorStateRepository(workspace.Paths);
        var service = new WorkerSupervisorStateService(repository);

        service.RegisterPendingLaunch(
            "repo-z",
            "z-worker",
            "first launch",
            workerInstanceId: "worker-instance-z",
            hostSessionId: "host-old");
        var duplicate = Assert.Throws<InvalidOperationException>(() => service.RegisterPendingLaunch(
            "repo-a",
            "a-worker",
            "replacement launch",
            workerInstanceId: "worker-instance-z",
            hostSessionId: "host-new",
            actorSessionId: "actor-new"));
        service.RegisterPendingLaunch(
            "repo-b",
            "b-worker",
            "second launch",
            workerInstanceId: "worker-instance-b");

        var entries = repository.Load().Entries;

        Assert.Contains(
            "Worker supervisor instance 'worker-instance-z' already exists with state Requested",
            duplicate.Message,
            StringComparison.Ordinal);
        Assert.Equal(2, entries.Count);
        Assert.Collection(
            entries,
            first =>
            {
                Assert.Equal("repo-b", first.RepoId);
                Assert.Equal("worker-instance-b", first.WorkerInstanceId);
            },
            second =>
            {
                Assert.Equal("repo-z", second.RepoId);
                Assert.Equal("worker-instance-z", second.WorkerInstanceId);
                Assert.Equal("host-old", second.HostSessionId);
                Assert.Null(second.ActorSessionId);
            });
    }

    [Fact]
    public void MarkActorSessionLost_UpdatesBoundSupervisorInstanceOnly()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonWorkerSupervisorStateRepository(workspace.Paths);
        var service = new WorkerSupervisorStateService(repository);
        var launch = service.RegisterPendingLaunch(
            "repo-supervisor-s5",
            "codex-worker-supervised",
            "phase S5 pending supervised worker launch",
            workerInstanceId: "worker-instance-s5",
            actorSessionId: "actor-worker-s5");

        service.BindActorSession(
            launch.WorkerInstanceId,
            "actor-worker-s5",
            Environment.ProcessId,
            DateTimeOffset.UtcNow,
            "phase S5 bound supervised worker");
        var updated = service.MarkActorSessionLost(
            "actor-worker-s5",
            "phase S5 host liveness reconciliation");
        var missing = service.MarkActorSessionLost(
            "actor-worker-missing",
            "phase S5 missing actor should not create state");

        var record = Assert.Single(repository.Load().Entries);
        Assert.NotNull(updated);
        Assert.Null(missing);
        Assert.Equal(WorkerSupervisorInstanceState.Lost, updated.State);
        Assert.Equal(WorkerSupervisorInstanceState.Lost, record.State);
        Assert.Equal("actor-worker-s5", record.ActorSessionId);
        Assert.Equal("phase S5 host liveness reconciliation", record.LastReason);
    }

    [Fact]
    public void RecordHostProcessHandle_RequiresRunningSupervisorAndPersistsHandleProof()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonWorkerSupervisorStateRepository(workspace.Paths);
        var service = new WorkerSupervisorStateService(repository);
        var launch = service.RegisterPendingLaunch(
            "repo-supervisor-r5",
            "codex-worker-supervised",
            "phase R5 pending supervised worker launch",
            workerInstanceId: "worker-instance-r5",
            actorSessionId: "actor-worker-r5");

        var rejected = Assert.Throws<InvalidOperationException>(() => service.RecordHostProcessHandle(
            launch.WorkerInstanceId,
            "host-process-handle-r5",
            "host-session-r5",
            "phase R5 handle cannot bind before process registration"));

        service.BindActorSession(
            launch.WorkerInstanceId,
            "actor-worker-r5",
            Environment.ProcessId,
            DateTimeOffset.UtcNow,
            "phase R5 bound supervised worker");
        var updated = service.RecordHostProcessHandle(
            launch.WorkerInstanceId,
            "host-process-handle-r5",
            "host-session-r5",
            "phase R5 host process handle proof",
            stdoutLogPath: "logs/worker-r5.stdout.log",
            stderrLogPath: "logs/worker-r5.stderr.log");
        var record = Assert.Single(repository.Load().Entries);

        Assert.Contains(
            "cannot hold a Host process handle while it is Requested",
            rejected.Message,
            StringComparison.Ordinal);
        Assert.Equal(WorkerSupervisorInstanceState.Running, updated.State);
        Assert.Equal("host-process-handle-r5", updated.HostProcessHandleId);
        Assert.Equal("host-session-r5", updated.HostProcessHandleOwnerSessionId);
        Assert.NotNull(updated.HostProcessHandleAcquiredAt);
        Assert.Equal("logs/worker-r5.stdout.log", updated.HostProcessStdoutLogPath);
        Assert.Equal("logs/worker-r5.stderr.log", updated.HostProcessStderrLogPath);
        Assert.Equal(updated.HostProcessHandleId, record.HostProcessHandleId);
        Assert.Equal(updated.HostProcessHandleOwnerSessionId, record.HostProcessHandleOwnerSessionId);
        Assert.Equal(updated.HostProcessHandleAcquiredAt, record.HostProcessHandleAcquiredAt);
    }
}
