using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Tests;

public sealed class ReviewEvidenceGateServiceTests
{
    [Fact]
    public void EvaluateBeforeWriteback_ReportsMissingAcceptanceContractEvidence()
    {
        var service = new ReviewEvidenceGateService();
        var assessment = service.EvaluateBeforeWriteback(
            new TaskNode
            {
                TaskId = "T-EVIDENCE-001",
                AcceptanceContract = new AcceptanceContract
                {
                    EvidenceRequired =
                    [
                        new AcceptanceContractEvidenceRequirement
                        {
                            Type = "test_output",
                            Description = "Persist targeted test output.",
                        },
                    ],
                },
            },
            new PlannerReviewArtifact
            {
                TaskId = "T-EVIDENCE-001",
                ValidationPassed = true,
            },
            workerArtifact: null);

        Assert.False(assessment.IsSatisfied);
        Assert.Equal("test_output (Persist targeted test output.)", assessment.SummarizeMissingRequirements());
        Assert.Contains(
            "Persist targeted test output",
            assessment.BuildFollowUpActions().Single(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluateAfterWriteback_AcceptsSatisfiedEvidenceRequirements()
    {
        var service = new ReviewEvidenceGateService();
        var assessment = service.EvaluateAfterWriteback(
            new TaskNode
            {
                TaskId = "T-EVIDENCE-002",
                AcceptanceContract = new AcceptanceContract
                {
                    EvidenceRequired =
                    [
                        new AcceptanceContractEvidenceRequirement { Type = "validation_passed" },
                        new AcceptanceContractEvidenceRequirement { Type = "validation_evidence" },
                        new AcceptanceContractEvidenceRequirement { Type = "test_output" },
                        new AcceptanceContractEvidenceRequirement { Type = "writeback" },
                    ],
                },
            },
            new PlannerReviewArtifact
            {
                TaskId = "T-EVIDENCE-002",
                ValidationPassed = true,
                ValidationEvidence = ["targeted validation passed"],
            },
            new WorkerExecutionArtifact
            {
                TaskId = "T-EVIDENCE-002",
                Evidence = new ExecutionEvidence
                {
                    TaskId = "T-EVIDENCE-002",
                    TestOutputRef = ".ai/test-output/T-EVIDENCE-002.txt",
                },
            },
            new ReviewWritebackRecord
            {
                Applied = true,
            });

        Assert.True(assessment.IsSatisfied);
        Assert.Empty(assessment.MissingRequirements);
    }

    [Fact]
    public void EvaluateProjectedAfterWriteback_CanRequireProjectedResultCommit()
    {
        var service = new ReviewEvidenceGateService();
        var task = new TaskNode
        {
            TaskId = "T-EVIDENCE-003",
            AcceptanceContract = new AcceptanceContract
            {
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement { Type = "result_commit" },
                    new AcceptanceContractEvidenceRequirement { Type = "writeback" },
                ],
            },
        };

        var missing = service.EvaluateProjectedAfterWriteback(
            task,
            new PlannerReviewArtifact
            {
                TaskId = task.TaskId,
                ValidationPassed = true,
            },
            new WorkerExecutionArtifact
            {
                TaskId = task.TaskId,
                Evidence = new ExecutionEvidence
                {
                    TaskId = task.TaskId,
                    FilesWritten = ["src/Synthetic/File.cs"],
                },
            },
            new ReviewWritebackEvidenceProjection(
                WillApply: true,
                WillCaptureResultCommit: false,
                Files: ["src/Synthetic/File.cs"]));

        Assert.False(missing.IsSatisfied);
        Assert.Equal("result_commit", missing.MissingRequirements.Single().RequirementType);

        var satisfied = service.EvaluateProjectedAfterWriteback(
            task,
            new PlannerReviewArtifact
            {
                TaskId = task.TaskId,
                ValidationPassed = true,
            },
            new WorkerExecutionArtifact
            {
                TaskId = task.TaskId,
                Evidence = new ExecutionEvidence
                {
                    TaskId = task.TaskId,
                    FilesWritten = ["src/Synthetic/File.cs"],
                },
            },
            new ReviewWritebackEvidenceProjection(
                WillApply: true,
                WillCaptureResultCommit: true,
                Files: ["src/Synthetic/File.cs"]));

        Assert.True(satisfied.IsSatisfied);
    }

    [Fact]
    public void EvaluateProjectedAfterWriteback_SatisfiesMemoryWriteRequirementFromHostRoutedWriteback()
    {
        var service = new ReviewEvidenceGateService();
        var task = new TaskNode
        {
            TaskId = "T-EVIDENCE-004",
            AcceptanceContract = new AcceptanceContract
            {
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement { Type = "memory_write" },
                ],
            },
        };

        var assessment = service.EvaluateProjectedAfterWriteback(
            task,
            new PlannerReviewArtifact
            {
                TaskId = task.TaskId,
                ValidationPassed = true,
            },
            new WorkerExecutionArtifact
            {
                TaskId = task.TaskId,
                Result = new WorkerExecutionResult
                {
                    TaskId = task.TaskId,
                    RunId = $"RUN-{task.TaskId}-001",
                    BackendId = "codex_cli",
                    ProviderId = "codex",
                    AdapterId = "CodexCliWorkerAdapter",
                    Status = WorkerExecutionStatus.Succeeded,
                },
                Evidence = new ExecutionEvidence
                {
                    TaskId = task.TaskId,
                    FilesWritten = [".ai/memory/PROJECT.md"],
                    EvidenceStrength = ExecutionEvidenceStrength.Observed,
                },
            },
            new ReviewWritebackEvidenceProjection(
                WillApply: true,
                WillCaptureResultCommit: false,
                Files: [".ai/memory/PROJECT.md"]));

        Assert.True(assessment.IsSatisfied);
    }

    [Fact]
    public void EvaluateBeforeWriteback_BlocksNullWorkerAsDiagnosticOnlyEvidence()
    {
        var service = new ReviewEvidenceGateService();
        var assessment = service.EvaluateBeforeWriteback(
            new TaskNode
            {
                TaskId = "T-EVIDENCE-005",
            },
            new PlannerReviewArtifact
            {
                TaskId = "T-EVIDENCE-005",
                ValidationPassed = true,
            },
            new WorkerExecutionArtifact
            {
                TaskId = "T-EVIDENCE-005",
                Result = new WorkerExecutionResult
                {
                    TaskId = "T-EVIDENCE-005",
                    RunId = "RUN-T-EVIDENCE-005-001",
                    BackendId = "null_worker",
                    ProviderId = "null",
                    AdapterId = "NullWorkerAdapter",
                    Status = WorkerExecutionStatus.Succeeded,
                },
                Evidence = new ExecutionEvidence
                {
                    TaskId = "T-EVIDENCE-005",
                    EvidenceStrength = ExecutionEvidenceStrength.Observed,
                    EvidenceCompleteness = ExecutionEvidenceCompleteness.Partial,
                },
            });

        Assert.False(assessment.IsSatisfied);
        Assert.Contains(assessment.MissingRequirements, gap => gap.RequirementType == "null_worker_diagnostic_only");
    }

    [Fact]
    public void EvaluateBeforeWriteback_BlocksWhenNoConcreteWorkerEvidenceExists()
    {
        var service = new ReviewEvidenceGateService();
        var assessment = service.EvaluateBeforeWriteback(
            new TaskNode
            {
                TaskId = "T-EVIDENCE-006",
            },
            new PlannerReviewArtifact
            {
                TaskId = "T-EVIDENCE-006",
                ValidationPassed = true,
            },
            workerArtifact: null);

        Assert.False(assessment.IsSatisfied);
        Assert.Contains(assessment.MissingRequirements, gap => gap.RequirementType == "worker_execution_evidence");
    }

    [Fact]
    public void EvaluateBeforeWriteback_SatisfiesManualReviewNoteOnlyFromReviewArtifact()
    {
        var service = new ReviewEvidenceGateService();
        var task = new TaskNode
        {
            TaskId = "T-EVIDENCE-007",
            AcceptanceContract = new AcceptanceContract
            {
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement { Type = "manual_review_note" },
                ],
            },
        };

        var missing = service.EvaluateBeforeWriteback(
            task,
            new PlannerReviewArtifact
            {
                TaskId = task.TaskId,
                ValidationPassed = true,
                Review = new PlannerReview { Reason = string.Empty },
                PlannerComment = string.Empty,
                DecisionReason = null,
            },
            new WorkerExecutionArtifact
            {
                TaskId = task.TaskId,
                Result = new WorkerExecutionResult
                {
                    TaskId = task.TaskId,
                    RunId = $"RUN-{task.TaskId}-001",
                    BackendId = "codex_cli",
                    ProviderId = "codex",
                    AdapterId = "CodexCliWorkerAdapter",
                    Status = WorkerExecutionStatus.Succeeded,
                },
                Evidence = new ExecutionEvidence
                {
                    TaskId = task.TaskId,
                    CommandsExecuted = ["targeted test"],
                    EvidenceStrength = ExecutionEvidenceStrength.Observed,
                },
            });

        Assert.False(missing.IsSatisfied);
        Assert.Equal("manual_review_note", missing.MissingRequirements.Single().RequirementType);

        var satisfied = service.EvaluateBeforeWriteback(
            task,
            new PlannerReviewArtifact
            {
                TaskId = task.TaskId,
                ValidationPassed = true,
                Review = new PlannerReview { Reason = "Human reviewer confirmed the bounded intent." },
                PlannerComment = string.Empty,
            },
            new WorkerExecutionArtifact
            {
                TaskId = task.TaskId,
                Result = new WorkerExecutionResult
                {
                    TaskId = task.TaskId,
                    RunId = $"RUN-{task.TaskId}-001",
                    BackendId = "codex_cli",
                    ProviderId = "codex",
                    AdapterId = "CodexCliWorkerAdapter",
                    Status = WorkerExecutionStatus.Succeeded,
                },
                Evidence = new ExecutionEvidence
                {
                    TaskId = task.TaskId,
                    CommandsExecuted = ["targeted test"],
                    EvidenceStrength = ExecutionEvidenceStrength.Observed,
                },
            });

        Assert.True(satisfied.IsSatisfied);
    }

    [Fact]
    public void EvaluateProjectedAfterWriteback_DoesNotTreatHostWritebackAsWorkerExecutionEvidence()
    {
        var service = new ReviewEvidenceGateService();
        var task = new TaskNode
        {
            TaskId = "T-EVIDENCE-008",
            AcceptanceContract = new AcceptanceContract
            {
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement { Type = "worker_execution_evidence" },
                    new AcceptanceContractEvidenceRequirement { Type = "memory_write" },
                ],
            },
        };

        var assessment = service.EvaluateProjectedAfterWriteback(
            task,
            new PlannerReviewArtifact
            {
                TaskId = task.TaskId,
                ValidationPassed = true,
                Review = new PlannerReview { Reason = "Bounded host-routed memory mutation is intended." },
            },
            workerArtifact: null,
            new ReviewWritebackEvidenceProjection(
                WillApply: true,
                WillCaptureResultCommit: false,
                Files: [".ai/memory/PROJECT.md"]));

        Assert.False(assessment.IsSatisfied);
        Assert.Contains(assessment.MissingRequirements, gap => gap.RequirementType == "worker_execution_evidence");
        Assert.DoesNotContain(assessment.MissingRequirements, gap => gap.RequirementType == "memory_write");
    }

    [Fact]
    public void EvaluateProjectedAfterWriteback_DoesNotTreatWorkerMemoryPathAsAuthoritativeTruthRecord()
    {
        var service = new ReviewEvidenceGateService();
        var task = new TaskNode
        {
            TaskId = "T-EVIDENCE-009",
            AcceptanceContract = new AcceptanceContract
            {
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement { Type = "authoritative_truth_record" },
                ],
            },
        };

        var assessment = service.EvaluateProjectedAfterWriteback(
            task,
            new PlannerReviewArtifact
            {
                TaskId = task.TaskId,
                ValidationPassed = true,
                Review = new PlannerReview { Reason = "Worker proposed a memory update." },
            },
            new WorkerExecutionArtifact
            {
                TaskId = task.TaskId,
                Result = new WorkerExecutionResult
                {
                    TaskId = task.TaskId,
                    RunId = $"RUN-{task.TaskId}-001",
                    BackendId = "codex_cli",
                    ProviderId = "codex",
                    AdapterId = "CodexCliWorkerAdapter",
                    Status = WorkerExecutionStatus.Succeeded,
                },
                Evidence = new ExecutionEvidence
                {
                    TaskId = task.TaskId,
                    WorktreePath = "/tmp/worktree",
                    FilesWritten = [".ai/memory/PROJECT.md"],
                    EvidenceStrength = ExecutionEvidenceStrength.Observed,
                },
            },
            new ReviewWritebackEvidenceProjection(
                WillApply: false,
                WillCaptureResultCommit: false,
                Files: Array.Empty<string>()));

        Assert.False(assessment.IsSatisfied);
        Assert.Equal("authoritative_truth_record", assessment.MissingRequirements.Single().RequirementType);
    }
}
