using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class PacketEnforcementServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [Fact]
    public void Evaluate_PlannerOnlyActionAttempt_IsRejected()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-302-TEST-001");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteExecutionPacket(workspace, task.TaskId);
        WriteWorkerExecutionArtifact(workspace, artifactRepository, task.TaskId, "RUN-T-CARD-302-TEST-001-001");

        var service = new PacketEnforcementService(workspace.Paths, taskGraphService, artifactRepository);
        var record = service.Evaluate(
            task.TaskId,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                Status = "success",
                ExecutionRunId = "RUN-T-CARD-302-TEST-001-001",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = ["src/CARVES.Runtime.Host/Program.cs"],
                },
                Validation = new ResultEnvelopeValidation
                {
                    Build = "success",
                    Tests = "success",
                },
                Next = new ResultEnvelopeNextAction
                {
                    Suggested = "Run review-task and sync-state after submit_result.",
                },
            });

        Assert.Equal("reject", record.Verdict);
        Assert.True(record.PlannerOnlyActionAttempted);
        Assert.True(record.LifecycleWritebackAttempted);
        Assert.Contains("planner_only_action_attempted", record.ReasonCodes);
    }

    [Fact]
    public void Evaluate_TruthWriteAttempt_IsQuarantined()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-302-TEST-002");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteExecutionPacket(workspace, task.TaskId);
        WriteWorkerExecutionArtifact(workspace, artifactRepository, task.TaskId, "RUN-T-CARD-302-TEST-002-001", ".ai/tasks/graph.json");

        var service = new PacketEnforcementService(workspace.Paths, taskGraphService, artifactRepository);
        var record = service.Evaluate(
            task.TaskId,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                Status = "success",
                ExecutionRunId = "RUN-T-CARD-302-TEST-002-001",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = [".ai/tasks/graph.json"],
                },
                Validation = new ResultEnvelopeValidation
                {
                    Build = "success",
                    Tests = "success",
                },
            });

        Assert.Equal("quarantine", record.Verdict);
        Assert.Contains(".ai/tasks/graph.json", record.TruthWriteFiles);
        Assert.Contains("truth_write_attempt_detected", record.ReasonCodes);
    }

    [Fact]
    public void Evaluate_ObservedOffPacketFileWithoutSelfReport_IsQuarantined()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-302-TEST-OBSERVED-OFFPACKET");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteExecutionPacket(workspace, task.TaskId);
        WriteWorkerExecutionArtifactWithProvenance(
            workspace,
            artifactRepository,
            task.TaskId,
            "RUN-T-CARD-302-TEST-OBSERVED-OFFPACKET-001",
            observedChangedFiles: ["docs/ScopeEscape.md"]);

        var service = new PacketEnforcementService(workspace.Paths, taskGraphService, artifactRepository);
        var record = service.Evaluate(
            task.TaskId,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                Status = "success",
                ExecutionRunId = "RUN-T-CARD-302-TEST-OBSERVED-OFFPACKET-001",
                Validation = new ResultEnvelopeValidation
                {
                    Build = "success",
                    Tests = "success",
                },
            });

        Assert.Equal("quarantine", record.Verdict);
        Assert.Empty(record.WorkerReportedChangedFiles);
        Assert.Empty(record.ResultEnvelopeChangedFiles);
        Assert.Empty(record.EvidenceChangedFiles);
        Assert.Contains("docs/ScopeEscape.md", record.WorkerObservedChangedFiles);
        Assert.Contains("docs/ScopeEscape.md", record.ChangedFiles);
        Assert.Contains("docs/ScopeEscape.md", record.OffPacketFiles);
        Assert.Contains("off_packet_edit_detected", record.ReasonCodes);
    }

    [Fact]
    public void Evaluate_ObservedTruthWriteWithoutSelfReport_IsQuarantined()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-302-TEST-OBSERVED-TRUTH");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteExecutionPacket(workspace, task.TaskId);
        WriteWorkerExecutionArtifactWithProvenance(
            workspace,
            artifactRepository,
            task.TaskId,
            "RUN-T-CARD-302-TEST-OBSERVED-TRUTH-001",
            observedChangedFiles: [".ai/tasks/graph.json"]);

        var service = new PacketEnforcementService(workspace.Paths, taskGraphService, artifactRepository);
        var record = service.Evaluate(
            task.TaskId,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                Status = "success",
                ExecutionRunId = "RUN-T-CARD-302-TEST-OBSERVED-TRUTH-001",
                Validation = new ResultEnvelopeValidation
                {
                    Build = "success",
                    Tests = "success",
                },
            });

        Assert.Equal("quarantine", record.Verdict);
        Assert.Empty(record.WorkerReportedChangedFiles);
        Assert.Contains(".ai/tasks/graph.json", record.WorkerObservedChangedFiles);
        Assert.Contains(".ai/tasks/graph.json", record.TruthWriteFiles);
        Assert.Contains("truth_write_attempt_detected", record.ReasonCodes);
    }

    [Fact]
    public void Evaluate_WithPacketButNoResultYet_IsPendingExecution()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-302-TEST-003");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteExecutionPacket(workspace, task.TaskId);

        var service = new PacketEnforcementService(workspace.Paths, taskGraphService, artifactRepository);
        var record = service.Evaluate(task.TaskId);

        Assert.Equal("pending_execution", record.Verdict);
        Assert.True(record.PacketPresent);
        Assert.True(record.PacketContractValid);
        Assert.False(record.ResultPresent);
        Assert.False(record.WorkerArtifactPresent);
    }

    private static TaskNode CreateTask(string taskId)
    {
        return new TaskNode
        {
            TaskId = taskId,
            CardId = "CARD-302",
            Title = "Packet enforcement test",
            Description = "Validate packet enforcement behavior.",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Execution,
            Priority = "P1",
            Scope = ["src/CARVES.Runtime.Host/Program.cs"],
            Acceptance = ["packet enforcement truth is emitted"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    private static void WriteExecutionPacket(TemporaryWorkspace workspace, string taskId)
    {
        var packet = new ExecutionPacket
        {
            PacketId = $"EP-{taskId}-v1",
            TaskRef = new ExecutionPacketTaskRef
            {
                CardId = "CARD-302",
                TaskId = taskId,
                TaskRevision = 1,
            },
            Goal = "Test packet enforcement.",
            PlannerIntent = Carves.Runtime.Domain.Planning.PlannerIntent.Execution,
            Permissions = new ExecutionPacketPermissions
            {
                EditableRoots = ["src/"],
                ReadOnlyRoots = ["docs/"],
                TruthRoots = ["carves://truth/tasks", "carves://truth/runtime"],
                RepoMirrorRoots = [".ai/"],
            },
            WorkerAllowedActions = ["read", "edit", "build", "test", "carves.submit_result", "carves.request_replan"],
            PlannerOnlyActions = ["carves.review_task", "carves.sync_state"],
        };

        workspace.WriteFile(
            $".ai/runtime/execution-packets/{taskId}.json",
            JsonSerializer.Serialize(packet, JsonOptions));
    }

    private static void WriteWorkerExecutionArtifact(
        TemporaryWorkspace workspace,
        JsonRuntimeArtifactRepository artifactRepository,
        string taskId,
        string runId,
        params string[] filesWritten)
    {
        var evidencePath = $".ai/artifacts/worker-executions/{runId}/evidence.json";
        workspace.WriteFile(evidencePath, "{\"taskId\":\"" + taskId + "\"}");
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = runId,
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = runId,
                WorkerId = "CodexCliWorkerAdapter",
                EvidenceSource = ExecutionEvidenceSource.Host,
                FilesWritten = filesWritten,
                EvidencePath = evidencePath,
                EvidenceCompleteness = ExecutionEvidenceCompleteness.Complete,
                EvidenceStrength = ExecutionEvidenceStrength.Replayable,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                EndedAt = DateTimeOffset.UtcNow,
                ExitStatus = 0,
            },
        });
        artifactRepository.SaveSafetyArtifact(new SafetyArtifact
        {
            Decision = SafetyDecision.Allow(taskId),
        });
    }

    private static void WriteWorkerExecutionArtifactWithProvenance(
        TemporaryWorkspace workspace,
        JsonRuntimeArtifactRepository artifactRepository,
        string taskId,
        string runId,
        IReadOnlyList<string>? reportedChangedFiles = null,
        IReadOnlyList<string>? observedChangedFiles = null,
        IReadOnlyList<string>? filesWritten = null)
    {
        var evidencePath = $".ai/artifacts/worker-executions/{runId}/evidence.json";
        workspace.WriteFile(evidencePath, "{\"taskId\":\"" + taskId + "\"}");
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = runId,
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
                ChangedFiles = reportedChangedFiles ?? Array.Empty<string>(),
                ObservedChangedFiles = observedChangedFiles ?? Array.Empty<string>(),
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = runId,
                WorkerId = "CodexCliWorkerAdapter",
                EvidenceSource = ExecutionEvidenceSource.Host,
                FilesWritten = filesWritten ?? Array.Empty<string>(),
                EvidencePath = evidencePath,
                EvidenceCompleteness = ExecutionEvidenceCompleteness.Complete,
                EvidenceStrength = ExecutionEvidenceStrength.Replayable,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                EndedAt = DateTimeOffset.UtcNow,
                ExitStatus = 0,
            },
        });
        artifactRepository.SaveSafetyArtifact(new SafetyArtifact
        {
            Decision = SafetyDecision.Allow(taskId),
        });
    }
}
