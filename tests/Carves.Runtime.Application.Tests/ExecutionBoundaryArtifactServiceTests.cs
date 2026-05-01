using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Tests;

public sealed class ExecutionBoundaryArtifactServiceTests
{
    [Fact]
    public void Persist_WritesBudgetTelemetryViolationAndReplan()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExecutionBoundaryArtifactService(workspace.Paths.AiRoot);
        var assessment = new ExecutionBoundaryAssessment
        {
            Budget = new ExecutionBudget
            {
                Size = ExecutionBudgetSize.Small,
                ConfidenceLevel = ExecutionConfidenceLevel.Medium,
                MaxFiles = 3,
                MaxLinesChanged = 180,
                MaxRetries = 2,
                MaxFailureDensity = 0.35,
                MaxDurationMinutes = 20,
                Summary = "small budget",
                Rationale = "test",
            },
            Confidence = new ExecutionConfidence
            {
                Level = ExecutionConfidenceLevel.Medium,
                SuccessRate = 0.5,
            },
            Telemetry = new ExecutionTelemetry
            {
                FilesChanged = 4,
                LinesChanged = 220,
                RetryCount = 1,
                FailureCount = 0,
                FailureDensity = 0,
                DurationSeconds = 60,
                ObservedPaths = ["src/A.cs"],
                ChangeKinds = [ExecutionChangeKind.SourceCode],
            },
            RiskLevel = ExecutionRiskLevel.High,
            RiskScore = 3,
            Decision = ExecutionBoundaryDecision.Stop,
            ShouldStop = true,
            StopReason = ExecutionBoundaryStopReason.SizeExceeded,
            StopDetail = "4 > 3",
        };
        var violation = new ExecutionBoundaryViolation
        {
            TaskId = "T-CARD-174-001",
            Reason = ExecutionBoundaryStopReason.SizeExceeded,
            Detail = "4 > 3",
            Budget = assessment.Budget,
            Telemetry = assessment.Telemetry,
            Confidence = assessment.Confidence,
        };
        var replan = new ExecutionBoundaryReplanRequest
        {
            TaskId = "T-CARD-174-001",
            RunId = "RUN-T-CARD-174-001-001",
            StoppedAtStep = 4,
            TotalSteps = 5,
            RunGoal = "Boundary enforcement",
            Strategy = ExecutionBoundaryReplanStrategy.SplitTask,
            ViolationReason = ExecutionBoundaryStopReason.SizeExceeded,
            ViolationPath = service.GetViolationPath("T-CARD-174-001"),
            Constraints = new ExecutionBoundaryReplanConstraints
            {
                MaxFiles = 2,
                MaxLinesChanged = 90,
                AllowedChangeKinds = [ExecutionChangeKind.SourceCode],
            },
            FollowUpSuggestions = ["Split the task."],
        };
        var run = new ExecutionRun
        {
            RunId = "RUN-T-CARD-174-001-001",
            TaskId = "T-CARD-174-001",
            Status = ExecutionRunStatus.Running,
            TriggerReason = ExecutionRunTriggerReason.Initial,
            Goal = "Boundary enforcement",
            CurrentStepIndex = 3,
            Steps =
            [
                new ExecutionStep { StepId = "1", Title = "Inspect", Kind = ExecutionStepKind.Inspect, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = "2", Title = "Implement", Kind = ExecutionStepKind.Implement, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = "3", Title = "Verify", Kind = ExecutionStepKind.Verify, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = "4", Title = "Writeback", Kind = ExecutionStepKind.Writeback, Status = ExecutionStepStatus.InProgress },
                new ExecutionStep { StepId = "5", Title = "Cleanup", Kind = ExecutionStepKind.Cleanup, Status = ExecutionStepStatus.Pending },
            ],
        };
        var decision = new BoundaryDecision
        {
            TaskId = "T-CARD-174-001",
            RunId = run.RunId,
            EvidenceStatus = "complete",
            SafetyStatus = "passed_upstream",
            TestStatus = "success",
            WritebackDecision = BoundaryWritebackDecision.AdmitToReview,
            ReasonCodes = ["review_boundary"],
            ReviewerRequired = true,
            DecisionConfidence = 0.8,
            Summary = "Execution result is valid but must stop at the review boundary.",
        };

        var artifacts = service.Persist("T-CARD-174-001", assessment, violation, replan, run, decision);

        Assert.True(File.Exists(artifacts.BudgetPath));
        Assert.True(File.Exists(artifacts.TelemetryPath));
        Assert.True(File.Exists(artifacts.ViolationPath!));
        Assert.True(File.Exists(artifacts.ReplanPath!));
        Assert.True(File.Exists(artifacts.DecisionPath!));
        Assert.Equal("boundary.v1", service.LoadViolation("T-CARD-174-001")!.SchemaVersion);
        Assert.Equal(ExecutionBoundaryReplanStrategy.SplitTask, service.LoadReplan("T-CARD-174-001")!.Strategy);
        Assert.Equal(run.RunId, service.LoadBudget("T-CARD-174-001")!.RunId);
        Assert.Equal(run.RunId, service.LoadTelemetry("T-CARD-174-001")!.RunId);
        Assert.Equal(BoundaryWritebackDecision.AdmitToReview, service.LoadDecision("T-CARD-174-001")!.WritebackDecision);
    }
}
