using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Host;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

public sealed partial class HostClientSurfaceTests
{
    [Fact]
    public void AgentGateway_CanQueryStatusThroughHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            Assert.True(start.ExitCode == 0, start.CombinedOutput);

            var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "query", "status");

            Assert.True(result.ExitCode == 0, result.CombinedOutput);
            Assert.Contains("\"accepted\": true", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"outcome\": \"query_status\"", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"kind\": \"agent_status_context\"", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void Attach_ThroughHostReportsAttachModeAndReadiness()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = CreateGitSandbox();
        targetRepo.WriteFile("src/Sample.cs", "namespace Attached.Sample; public sealed class SampleService { }");
        targetRepo.CommitAll("Initial commit");

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var attach = ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-surface");

            Assert.Equal(0, attach.ExitCode);
            Assert.Contains("CARVES runtime attached.", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Attach mode: fresh_init", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Protocol mode: carves_development", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Project understanding: fresh (refreshed)", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Readiness: ready", attach.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void ProjectRepoAttach_WithoutExplicitPath_UsesThinClientDiscoveryAndWritesRuntimeManifest()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = CreateGitSandbox();
        targetRepo.WriteFile("src/ThinClient.cs", "namespace Attached.ThinClient; public sealed class ThinClientService { }");
        targetRepo.CommitAll("Initial commit");

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var attachCapability = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "attach-flow");
            var attach = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "attach");
            var manifestPath = Path.Combine(targetRepo.RootPath, ".ai", "runtime.json");
            var handshakePath = Path.Combine(targetRepo.RootPath, ".ai", "runtime", "attach-handshake.json");
            var systemConfigPath = Path.Combine(targetRepo.RootPath, ".ai", "config", "system.json");
            var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
            var hostSessionPath = paths.PlatformHostSessionLiveStateFile;
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            using var handshake = JsonDocument.Parse(File.ReadAllText(handshakePath));
            using var hostSession = JsonDocument.Parse(File.ReadAllText(hostSessionPath));

            Assert.Equal(0, attachCapability.ExitCode);
            Assert.Equal(0, attach.ExitCode);
            Assert.Contains("Connected to host:", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("CARVES runtime attached.", attach.StandardOutput, StringComparison.Ordinal);
            Assert.True(File.Exists(systemConfigPath));
            Assert.True(File.Exists(manifestPath));
            Assert.True(File.Exists(handshakePath));
            Assert.True(File.Exists(hostSessionPath));
            Assert.Equal(Path.GetFullPath(targetRepo.RootPath), manifest.RootElement.GetProperty("repo_path").GetString());
            Assert.False(string.IsNullOrWhiteSpace(manifest.RootElement.GetProperty("host_session_id").GetString()));
            Assert.Equal(
                manifest.RootElement.GetProperty("host_session_id").GetString(),
                handshake.RootElement.GetProperty("acknowledgement").GetProperty("host_session_id").GetString());
            Assert.False(string.IsNullOrWhiteSpace(hostSession.RootElement.GetProperty("session_id").GetString()));
            Assert.Equal("active", hostSession.RootElement.GetProperty("status").GetString(), ignoreCase: true);
            Assert.Contains(
                hostSession.RootElement.GetProperty("attached_repos").EnumerateArray().Select(item => item.GetProperty("repo_path").GetString()),
                path => string.Equals(Path.GetFullPath(path!), Path.GetFullPath(targetRepo.RootPath), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void Attach_RejectsConflictingClientUntilForceTakeoverIsExplicit()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = CreateGitSandbox();
        targetRepo.WriteFile("src/AttachConflict.cs", "namespace Attached.Conflict; public sealed class AttachConflictService { }");
        targetRepo.CommitAll("Initial commit");

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var firstAttach = ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-attach-conflict");
            var conflictingAttach = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "attach");
            var forcedAttach = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "attach", "--force");
            var handshakePath = Path.Combine(targetRepo.RootPath, ".ai", "runtime", "attach-handshake.json");
            using var handshake = JsonDocument.Parse(File.ReadAllText(handshakePath));

            Assert.Equal(0, firstAttach.ExitCode);
            Assert.NotEqual(0, conflictingAttach.ExitCode);
            Assert.Contains("already controlled by client", conflictingAttach.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, forcedAttach.ExitCode);
            Assert.Equal(
                Path.GetFullPath(targetRepo.RootPath),
                Path.GetFullPath(handshake.RootElement.GetProperty("request").GetProperty("client_repo_root").GetString()!));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void HostRoutedCardLifecycle_ManagesDraftsAndEnforcesApprovedOnlyTaskGraphEntry()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = CreateGitSandbox();
        targetRepo.WriteFile("src/CardLifecycle.cs", "namespace Attached.Cards; public sealed class CardLifecycleService { }");
        targetRepo.CommitAll("Initial commit");
        var payloadDirectory = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads");
        Directory.CreateDirectory(payloadDirectory);
        var createPayload = Path.Combine(payloadDirectory, "card-create.json");
        File.WriteAllText(createPayload, JsonSerializer.Serialize(new
        {
            card_id = "CARD-225",
            title = "Managed card",
            goal = "Manage card lifecycle through host.",
            acceptance = new[] { "card exists" },
        }));
        var updatePayload = Path.Combine(payloadDirectory, "card-update.json");
        File.WriteAllText(updatePayload, JsonSerializer.Serialize(new
        {
            title = "Managed card updated",
            notes = new[] { "operator reviewed" },
        }));
        var taskGraphPayload = Path.Combine(payloadDirectory, "taskgraph-draft.json");
        File.WriteAllText(taskGraphPayload, JsonSerializer.Serialize(new
        {
            card_id = "CARD-225",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-225-001",
                    title = "First task",
                    description = "first task"
                },
            },
        }));

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var attach = ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-card-lifecycle");

            var create = ProgramHarness.Run("--repo-root", sandbox.RootPath, "create-card-draft", createPayload, "--repo-id", "repo-card-lifecycle");
            var list = ProgramHarness.Run("--repo-root", sandbox.RootPath, "list-cards", "--repo-id", "repo-card-lifecycle");
            var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "inspect-card", "CARD-225", "--repo-id", "repo-card-lifecycle");
            var gated = ProgramHarness.Run("--repo-root", sandbox.RootPath, "create-taskgraph-draft", taskGraphPayload, "--repo-id", "repo-card-lifecycle");
            var update = ProgramHarness.Run("--repo-root", sandbox.RootPath, "update-card", "CARD-225", updatePayload, "--repo-id", "repo-card-lifecycle");
            var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "approve-card", "CARD-225", "approved for planning", "--repo-id", "repo-card-lifecycle");
            var draft = ProgramHarness.Run("--repo-root", sandbox.RootPath, "create-taskgraph-draft", taskGraphPayload, "--repo-id", "repo-card-lifecycle");

            Assert.Equal(0, attach.ExitCode);
            Assert.Equal(0, create.ExitCode);
            Assert.Contains("Created card draft CARD-225", create.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, list.ExitCode);
            Assert.Contains("\"card_id\": \"CARD-225\"", list.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, inspect.ExitCode);
            Assert.Contains("\"lifecycle_state\": \"draft\"", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.NotEqual(0, gated.ExitCode);
            Assert.Contains("cannot enter planning", gated.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, update.ExitCode);
            Assert.Equal(0, approve.ExitCode);
            Assert.Contains("Status: approved", approve.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, draft.ExitCode);
            Assert.Contains("Created taskgraph draft", draft.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void HostHandshake_RefreshesRepoHostMappingsWithoutDuplicatingRepoIdentity()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = CreateGitSandbox();
        targetRepo.WriteFile("src/Sample.cs", "namespace Attached.Sample; public sealed class SampleService { }");
        targetRepo.CommitAll("Initial commit");

        ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-host-mapping");
        var registryPath = ControlPlanePaths.FromRepoRoot(sandbox.RootPath).PlatformRepoRuntimeRegistryLiveStateFile;
        var originalRegistry = File.ReadAllText(registryPath);
        var registry = JsonNode.Parse(originalRegistry)!.AsObject();
        var items = registry["items"]!.AsArray();
        var originalRepoId = items[0]!["repo_id"]!.GetValue<string>();
        items[0]!["host_id"] = string.Empty;
        File.WriteAllText(registryPath, registry.ToJsonString(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        }));

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "status");
            var refreshed = Assert.Single(
                ReadRepoRuntimeRegistry(sandbox.RootPath),
                item => string.Equals(item.RepoId, originalRepoId, StringComparison.Ordinal));

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, status.ExitCode);
            Assert.Equal(originalRepoId, refreshed.RepoId);
            Assert.False(string.IsNullOrWhiteSpace(refreshed.HostId));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void FleetStatus_UsesColdRegistryTruthAndProjectsMappings()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = CreateGitSandbox();
        targetRepo.WriteFile("src/Fleet.cs", "namespace Fleet.Target; public sealed class FleetService { }");
        targetRepo.CommitAll("Initial commit");

        ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-fleet-snapshot");

        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
        Assert.Equal(0, start.ExitCode);
        StopHost(sandbox.RootPath);

        var registryPath = ControlPlanePaths.FromRepoRoot(sandbox.RootPath).PlatformRepoRuntimeRegistryLiveStateFile;
        var registry = JsonNode.Parse(File.ReadAllText(registryPath))!.AsObject();
        var items = registry["items"]!.AsArray();
        items[0]!["host_id"] = string.Empty;
        File.WriteAllText(registryPath, registry.ToJsonString(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        }));

        var snapshot = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "fleet", "status");
        var fullSnapshot = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "fleet", "status", "--full");
        var targetRepoRuntimeId = PlatformIdentity.CreateRepoRuntimeId(targetRepo.RootPath);

        Assert.Equal(0, snapshot.ExitCode);
        Assert.Contains("Fleet Snapshot:", snapshot.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Hosts:", snapshot.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Repos:", snapshot.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mappings:", snapshot.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("(orphan)", snapshot.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(targetRepoRuntimeId, snapshot.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, fullSnapshot.ExitCode);
        Assert.Contains("last_seen=", fullSnapshot.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("path=", fullSnapshot.StandardOutput, StringComparison.Ordinal);
    }


    [Fact]
    public void HostRoutedInteractionSurface_ExposesIntentProtocolPromptAndDiscussionContext()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var intent = ProgramHarness.Run("--repo-root", sandbox.RootPath, "intent", "draft", "--persist");
            var protocol = ProgramHarness.Run("--repo-root", sandbox.RootPath, "protocol", "status");
            var prompt = ProgramHarness.Run("--repo-root", sandbox.RootPath, "prompt", "kernel");
            var discuss = ProgramHarness.Run("--repo-root", sandbox.RootPath, "discuss", "context");

            Assert.Equal(0, intent.ExitCode);
            Assert.Contains("\"state\": \"drafted\"", intent.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, protocol.ExitCode);
            Assert.Contains("\"kind\": \"conversation_protocol\"", protocol.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, prompt.ExitCode);
            Assert.Contains("carves-prompt-kernel", prompt.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, discuss.ExitCode);
            Assert.Contains("\"conversation_phase\":", discuss.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"prompt\":", discuss.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

}
