using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Evidence;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;

namespace Carves.Runtime.Application.Tests;

public sealed class ExecutionRunReportServiceTests
{
    [Fact]
    public void Persist_StoppedRun_WritesDeterministicReport()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExecutionRunReportService(workspace.Paths);
        var run = CreateRun("T-CARD-188-001", "RUN-T-CARD-188-001-001", ExecutionRunStatus.Stopped);
        var envelope = new ResultEnvelope
        {
            TaskId = run.TaskId,
            ExecutionRunId = run.RunId,
            Status = "failed",
            Changes = new ResultEnvelopeChanges
            {
                FilesModified =
                [
                    "src/CARVES.Runtime.Application/ControlPlane/ResultIngestionService.cs",
                    "src/CARVES.Runtime.Application/ExecutionPolicy/ExecutionPatternService.cs",
                ],
                LinesChanged = 120,
            },
            Failure = new ResultEnvelopeFailure
            {
                Type = nameof(FailureType.Timeout),
            },
        };
        var violation = new ExecutionBoundaryViolation
        {
            TaskId = run.TaskId,
            RunId = run.RunId,
            StoppedAtStep = 4,
            TotalSteps = 5,
            Reason = ExecutionBoundaryStopReason.Timeout,
            Detail = "Execution exceeded duration budget.",
            Telemetry = new ExecutionTelemetry
            {
                FilesChanged = 2,
                ObservedPaths = envelope.Changes.FilesModified,
            },
        };
        var replan = new ExecutionBoundaryReplanRequest
        {
            TaskId = run.TaskId,
            RunId = run.RunId,
            Strategy = ExecutionBoundaryReplanStrategy.SplitTask,
            ViolationReason = ExecutionBoundaryStopReason.Timeout,
            ViolationPath = ".ai/runtime/boundary/violations/T-CARD-188-001.json",
        };

        var first = service.Persist(run, envelope, violation: violation, replan: replan);
        var second = service.Persist(run, envelope, violation: violation, replan: replan);

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(ExecutionBoundaryStopReason.Timeout, first.BoundaryReason);
        Assert.Equal(ExecutionBoundaryReplanStrategy.SplitTask, first.ReplanStrategy);
        Assert.Equal(2, first.FilesChanged);
        Assert.Equal(3, first.CompletedSteps);
        Assert.True(File.Exists(service.GetReportPath(run.TaskId, run.RunId)));
    }

    [Fact]
    public void Persist_FromTaskRunReport_UsesChangedFilesWhenNoResultEnvelopeExists()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExecutionRunReportService(workspace.Paths);
        var run = CreateRun("T-CARD-188-002", "RUN-T-CARD-188-002-001", ExecutionRunStatus.Completed);
        var taskRunReport = new TaskRunReport
        {
            TaskId = run.TaskId,
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = run.TaskId,
                ChangedFiles =
                [
                    "src/CARVES.Runtime.Application/ControlPlane/ExecutionRunService.cs",
                    "tests/Carves.Runtime.Application.Tests/ExecutionRunReportServiceTests.cs",
                ],
            },
        };

        var report = service.Persist(run, taskRunReport: taskRunReport);

        Assert.Equal(2, report.FilesChanged);
        Assert.Contains("src/carves.runtime.application/controlplane", report.ModulesTouched);
        Assert.Contains("tests/carves.runtime.application.tests", report.ModulesTouched);
    }

    [Fact]
    public void Persist_CarriesSelectedPackReferenceIntoReport()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExecutionRunReportService(workspace.Paths);
        var run = CreateRun("T-CARD-326-REPORT", "RUN-T-CARD-326-REPORT-001", ExecutionRunStatus.Completed) with
        {
            SelectedPack = new RuntimePackExecutionAttribution
            {
                PackId = "carves.runtime.core",
                PackVersion = "1.2.3",
                Channel = "stable",
                ArtifactRef = ".ai/artifacts/packs/core-1.2.3.json",
                PolicyPreset = "core-default",
                GatePreset = "strict",
                ValidatorProfile = "default-validator",
                RoutingProfile = "connected-lanes",
                EnvironmentProfile = "workspace",
                SelectionMode = "manual_local_assignment",
                SelectedAtUtc = DateTimeOffset.UtcNow,
            },
        };

        var report = service.Persist(run, taskRunReport: new TaskRunReport
        {
            TaskId = run.TaskId,
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = run.TaskId,
            },
        });

        Assert.NotNull(report.SelectedPack);
        Assert.Equal("carves.runtime.core", report.SelectedPack!.PackId);
        Assert.Equal("1.2.3", report.SelectedPack.PackVersion);
        Assert.Equal("connected-lanes", report.SelectedPack.RoutingProfile);
    }

    [Fact]
    public void Persist_RecordsAppendOnlyRunEvidenceWithContextLineage()
    {
        using var workspace = new TemporaryWorkspace();
        var evidenceStore = new RuntimeEvidenceStoreService(workspace.Paths);
        var contextEvidence = evidenceStore.RecordContextPack(
            new ContextPack
            {
                ArtifactPath = ".ai/runtime/context-packs/tasks/T-CARD-188-003.json",
                Audience = ContextPackAudience.Worker,
                TaskId = "T-CARD-188-003",
                Goal = "Carry deterministic context into execution.",
                Task = "Project execution context.",
                Budget = new ContextPackBudget
                {
                    ProfileId = "worker",
                    BudgetPosture = ContextBudgetPostures.WithinTarget,
                },
                FacetNarrowing = new ContextPackFacetNarrowing
                {
                    Phase = "task_context_build",
                },
            },
            cardId: "CARD-188",
            sessionId: null);
        var service = new ExecutionRunReportService(workspace.Paths);
        var run = CreateRun("T-CARD-188-003", "RUN-T-CARD-188-003-001", ExecutionRunStatus.Completed);

        var report = service.Persist(run, taskRunReport: new TaskRunReport
        {
            TaskId = run.TaskId,
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = run.TaskId,
                ChangedFiles = ["src/CARVES.Runtime.Application/ControlPlane/ExecutionRunReportService.cs"],
            },
        });
        var evidence = evidenceStore.TryGetLatest(run.TaskId, RuntimeEvidenceKind.ExecutionRun);

        Assert.NotNull(evidence);
        Assert.Equal(report.RunId, evidence!.RunId);
        Assert.Contains(contextEvidence.EvidenceId, evidence.SourceEvidenceIds);
        Assert.Contains($".ai/runtime/run-reports/{run.TaskId}/{run.RunId}.json", evidence.ArtifactPaths);
    }

    private static ExecutionRun CreateRun(string taskId, string runId, ExecutionRunStatus status)
    {
        return new ExecutionRun
        {
            RunId = runId,
            TaskId = taskId,
            Goal = "Detect execution loops without mutating task truth.",
            Status = status,
            CurrentStepIndex = 3,
            Steps =
            [
                new ExecutionStep { StepId = $"{runId}-001", Title = "Inspect", Kind = ExecutionStepKind.Inspect, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = $"{runId}-002", Title = "Implement", Kind = ExecutionStepKind.Implement, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = $"{runId}-003", Title = "Verify", Kind = ExecutionStepKind.Verify, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = $"{runId}-004", Title = "Writeback", Kind = ExecutionStepKind.Writeback, Status = status == ExecutionRunStatus.Stopped ? ExecutionStepStatus.Blocked : ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = $"{runId}-005", Title = "Cleanup", Kind = ExecutionStepKind.Cleanup, Status = status == ExecutionRunStatus.Stopped ? ExecutionStepStatus.Skipped : ExecutionStepStatus.Completed },
            ],
        };
    }
}
