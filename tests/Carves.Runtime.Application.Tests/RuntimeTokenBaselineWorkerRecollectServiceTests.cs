using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenBaselineWorkerRecollectServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions RepoJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Persist_RecollectsWorkerTelemetryAndWritesFrozenCohortArtifacts()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var task = new TaskNode
        {
            TaskId = "T-P0A-WORKER-001",
            Title = "Replay worker request envelope",
            Description = "Rebuild the canonical worker request envelope from task truth.",
            Status = DomainTaskStatus.Completed,
            TaskType = TaskType.Execution,
            Scope = ["src/CARVES.Runtime.Application/AI/"],
            Acceptance = ["baseline recollect captures a worker request"],
            LastWorkerRunId = "RUN-T-P0A-WORKER-001-001",
        };
        new JsonTaskGraphRepository(paths).Save(new DomainTaskGraph([task]));
        WriteExecutionPacket(paths, task.TaskId, new ExecutionPacket
        {
            PacketId = "EP-T-P0A-WORKER-001-v1",
            TaskRef = new ExecutionPacketTaskRef
            {
                CardId = "CARD-P0A-WORKER-001",
                TaskId = task.TaskId,
                TaskRevision = 1,
            },
            Goal = "Replay worker request envelope.",
            PlannerIntent = Carves.Runtime.Domain.Planning.PlannerIntent.Execution,
            Scope = task.Scope,
            Context = new ExecutionPacketContext
            {
                ContextPackRef = $".ai/runtime/context-packs/tasks/{task.TaskId}.json",
            },
            Permissions = new ExecutionPacketPermissions
            {
                EditableRoots = task.Scope,
            },
            Budgets = new ExecutionPacketBudgets
            {
                MaxFilesChanged = 4,
                MaxLinesChanged = 120,
                MaxShellCommands = 4,
            },
        });
        WriteContextPack(paths, task.TaskId, new ContextPack
        {
            PackId = "task-T-P0A-WORKER-001",
            ArtifactPath = $".ai/runtime/context-packs/tasks/{task.TaskId}.json",
            TaskId = task.TaskId,
            Goal = "Replay worker request envelope.",
            Task = "Rebuild telemetry from frozen worker inputs.",
            Constraints = ["Stay inside worker truth."],
            PromptInput = "Context Pack\n\nGoal:\nReplay\n\nTask:\nRebuild telemetry.",
            PromptSections =
            [
                new RenderedPromptSection
                {
                    SectionId = "goal",
                    SectionKind = "goal",
                    SourceItemId = task.TaskId,
                    RendererVersion = "prose_v1",
                    StartChar = 0,
                    EndChar = 19,
                },
            ],
            Budget = new ContextPackBudget
            {
                ProfileId = "worker",
                Model = "gpt-5-mini",
                EstimatorVersion = ContextBudgetPolicyResolver.EstimatorVersion,
                ModelLimitTokens = 16000,
                TargetTokens = 900,
                AdvisoryTokens = 1000,
                HardSafetyTokens = 1300,
                MaxContextTokens = 1000,
                ReservedHeadroomTokens = 300,
                CoreBudgetTokens = 550,
                RelevantBudgetTokens = 450,
                UsedTokens = 120,
                TrimmedTokens = 0,
                FixedTokensEstimate = 120,
                DynamicTokensEstimate = 0,
                TotalContextTokensEstimate = 120,
                BudgetPosture = "balanced",
            },
            Trimmed =
            [
                new ContextPackTrimmedItem
                {
                    Key = "windowed_read:src/CARVES.Runtime.Application/AI/WorkerAiRequestFactory.cs",
                    Layer = ContextPackLayer.Relevant,
                    Priority = ContextPackPriority.Modules,
                    EstimatedTokens = 22,
                    Reason = "budget_trim",
                },
                new ContextPackTrimmedItem
                {
                    Key = "task",
                    Layer = ContextPackLayer.Core,
                    Priority = ContextPackPriority.Task,
                    EstimatedTokens = 18,
                    Reason = "budget_trim",
                },
            ],
        });
        WriteExecutionRunReport(paths, new ExecutionRunReport
        {
            RunId = "RUN-T-P0A-WORKER-001-001",
            TaskId = task.TaskId,
            Goal = "Replay worker request envelope.",
            RunStatus = ExecutionRunStatus.Completed,
            ModulesTouched = [],
            StepKinds = [ExecutionStepKind.Inspect, ExecutionStepKind.Implement, ExecutionStepKind.Verify],
            FilesChanged = 0,
            CompletedSteps = 3,
            TotalSteps = 3,
            Fingerprint = "fp-worker-recollect-test",
            RecordedAtUtc = new DateTimeOffset(2026, 4, 21, 10, 0, 0, TimeSpan.Zero),
        });

        var service = new RuntimeTokenBaselineWorkerRecollectService(
            paths,
            "TestRepo",
            AiProviderConfig.CreateProviderDefaults("openai", enabled: true, allowFallbackToNull: false, requestTimeoutSeconds: 30, maxOutputTokens: 500, reasoningEffort: "low"),
            new StubGitClient(),
            new JsonTaskGraphRepository(paths),
            new WorkerAiRequestFactory(500, 30, "gpt-5-mini", "low"),
            new LlmRequestEnvelopeAttributionService(paths),
            new RuntimeSurfaceRouteGraphService(paths));

        var result = service.Persist([task.TaskId], "phase_0a_worker_recollect_test", new DateOnly(2026, 4, 21));

        Assert.Equal("phase_0a_worker_recollect_test", result.Cohort.CohortId);
        Assert.Equal(1, result.RecollectedTaskCount);
        Assert.Equal(1, result.AttributionRecordCount);
        Assert.Equal(1, result.DirectToLlmRouteEdgeCount);
        Assert.Equal("local_estimate_only", result.Cohort.TokenAccountingSourcePolicy);
        Assert.Equal(["worker"], result.Cohort.RequestKinds);
        var recollectedTask = Assert.Single(result.Tasks);
        Assert.Equal(task.TaskId, recollectedTask.TaskId);
        Assert.Equal("RUN-T-P0A-WORKER-001-001", recollectedTask.RunId);
        Assert.StartsWith("worker-request-", recollectedTask.RequestId, StringComparison.Ordinal);
        Assert.StartsWith("REQENV-", recollectedTask.AttributionId, StringComparison.Ordinal);
        Assert.True(result.AttemptedTaskCohort.CoversFrozenReplayTaskSet);
        Assert.Equal(1, result.AttemptedTaskCohort.AttemptedTaskCount);
        Assert.Equal(1, result.AttemptedTaskCohort.SuccessfulAttemptedTaskCount);
        Assert.Equal(0, result.AttemptedTaskCohort.FailedAttemptedTaskCount);
        Assert.Equal(0, result.AttemptedTaskCohort.IncompleteAttemptedTaskCount);
        var attemptedTask = Assert.Single(result.AttemptedTaskCohort.Tasks);
        Assert.Equal("Completed", attemptedTask.LatestRunStatus);
        Assert.True(attemptedTask.SuccessfulAttempted);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.CohortJsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));

        var records = new LlmRequestEnvelopeAttributionService(paths).ListAll();
        var record = Assert.Single(records);
        Assert.Equal("worker", record.RequestKind);
        Assert.Equal("local_estimate", record.TokenAccountingSource);
        Assert.Equal(task.TaskId, record.TaskId);
        Assert.Equal("RUN-T-P0A-WORKER-001-001", record.RunId);
        Assert.Equal("task-T-P0A-WORKER-001", record.PackId);
        Assert.NotEmpty(record.Segments);
        Assert.Contains(record.Segments, segment => string.Equals(segment.SegmentKind, "context_pack", StringComparison.Ordinal) && segment.TokensEst > 0);
        var trimmedWindowedReads = Assert.Single(record.Segments, segment => string.Equals(segment.SegmentId, "context_pack:trimmed:windowed_reads", StringComparison.Ordinal));
        Assert.False(trimmedWindowedReads.Included);
        Assert.True(trimmedWindowedReads.Trimmed);
        Assert.Equal(22, trimmedWindowedReads.TrimBeforeTokensEst);
        Assert.Equal(0, trimmedWindowedReads.TrimAfterTokensEst);
        var trimmedTask = Assert.Single(record.Segments, segment => string.Equals(segment.SegmentId, "context_pack:trimmed:task", StringComparison.Ordinal));
        Assert.Equal(18, trimmedTask.TrimBeforeTokensEst);

        var routeEdges = new RuntimeSurfaceRouteGraphService(paths).ListRouteEdges();
        var directEdge = Assert.Single(routeEdges, edge => string.Equals(edge.ObservedRouteKind, "direct_to_llm", StringComparison.Ordinal));
        Assert.Equal($".ai/runtime/context-packs/tasks/{task.TaskId}.json", directEdge.SurfaceId);
        Assert.Equal(1, directEdge.LlmReinjectionCount);
    }

    private static void WriteExecutionPacket(ControlPlanePaths paths, string taskId, ExecutionPacket packet)
    {
        var path = Path.Combine(paths.AiRoot, "runtime", "execution-packets", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(packet, RepoJsonOptions));
    }

    private static void WriteContextPack(ControlPlanePaths paths, string taskId, ContextPack pack)
    {
        var path = Path.Combine(paths.AiRoot, "runtime", "context-packs", "tasks", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(pack, RepoJsonOptions));
    }

    private static void WriteExecutionRunReport(ControlPlanePaths paths, ExecutionRunReport report)
    {
        var path = Path.Combine(paths.AiRoot, "runtime", "run-reports", report.TaskId, $"{report.RunId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, RepoJsonOptions));
    }
}
