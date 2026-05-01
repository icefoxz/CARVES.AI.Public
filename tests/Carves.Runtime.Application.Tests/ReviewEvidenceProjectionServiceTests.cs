using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class ReviewEvidenceProjectionServiceTests
{
    [Fact]
    public void Build_ProjectsPostWritebackGapWhenResultCommitCannotBeCaptured()
    {
        using var workspace = new TemporaryWorkspace();
        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-REVIEW-EVIDENCE-001");
        Directory.CreateDirectory(worktreePath);
        var sourcePath = Path.Combine(worktreePath, "src", "Synthetic", "Projection.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "namespace Synthetic; public sealed class Projection {}");

        var task = CreateTask("T-REVIEW-EVIDENCE-001", "result_commit");
        var reviewArtifact = CreateReviewArtifact(task.TaskId);
        var workerArtifact = CreateWorkerArtifact(task.TaskId, worktreePath, "src/Synthetic/Projection.cs");

        var projection = new ReviewEvidenceProjectionService(workspace.RootPath, new StubGitClient())
            .Build(task, reviewArtifact, workerArtifact);

        Assert.Equal("post_writeback_gap", projection.Status);
        Assert.False(projection.CanFinalApprove);
        Assert.True(projection.CanWritebackProceed);
        Assert.True(projection.WillApplyWriteback);
        Assert.False(projection.WillCaptureResultCommit);
        Assert.Contains("result_commit", projection.Summary, StringComparison.Ordinal);
        Assert.Contains(projection.MissingAfterWriteback, item => item.RequirementType == "result_commit");
    }

    [Fact]
    public void Build_ProjectsFinalReadyWhenDelegatedGitWritebackCanCaptureResultCommit()
    {
        using var workspace = new TemporaryWorkspace();
        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-REVIEW-EVIDENCE-002");
        Directory.CreateDirectory(Path.Combine(worktreePath, ".git"));
        var sourcePath = Path.Combine(worktreePath, "src", "Synthetic", "Projection.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "namespace Synthetic; public sealed class Projection {}");

        var task = CreateTask("T-REVIEW-EVIDENCE-002", "result_commit");
        var reviewArtifact = CreateReviewArtifact(task.TaskId);
        var workerArtifact = CreateWorkerArtifact(task.TaskId, worktreePath, "src/Synthetic/Projection.cs");

        var projection = new ReviewEvidenceProjectionService(workspace.RootPath, new RepositoryGitClient())
            .Build(task, reviewArtifact, workerArtifact);

        Assert.Equal("final_ready", projection.Status);
        Assert.True(projection.CanFinalApprove);
        Assert.True(projection.CanWritebackProceed);
        Assert.True(projection.WillApplyWriteback);
        Assert.True(projection.WillCaptureResultCommit);
        Assert.Empty(projection.MissingAfterWriteback);
    }

    [Fact]
    public void Build_BlocksFinalApprovalWhenClosureDecisionBlocksWriteback()
    {
        using var workspace = new TemporaryWorkspace();
        var task = new TaskNode
        {
            TaskId = "T-REVIEW-EVIDENCE-CLOSURE",
            Title = "Project closure blockers",
            Status = DomainTaskStatus.Review,
            Scope = ["src/Synthetic/Closure.cs"],
            Acceptance = ["closure blockers are projected"],
        };
        var reviewArtifact = CreateReviewArtifact(
            task.TaskId,
            patchSummary: "files=0; lines=0; paths=(none)");
        var workerArtifact = CreateWorkerArtifact(
            task.TaskId,
            worktreePath: string.Empty,
            relativePath: null,
            commandsExecuted: ["synthetic command evidence"]);

        var projection = new ReviewEvidenceProjectionService(workspace.RootPath, new StubGitClient())
            .Build(task, reviewArtifact, workerArtifact);

        Assert.Equal("closure_blocked", projection.Status);
        Assert.False(projection.CanFinalApprove);
        Assert.True(projection.CanWritebackProceed);
        Assert.False(projection.ClosureWritebackAllowed);
        Assert.Equal("writeback_blocked", projection.ClosureStatus);
        Assert.Equal("block_writeback", projection.ClosureDecision);
        Assert.Contains(projection.ClosureBlockers, blocker => blocker.StartsWith("contract_matrix", StringComparison.Ordinal));
        Assert.Contains("closure decision", projection.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ProjectsWorkerCompletionClaimReadbackWithoutGrantingLifecycleTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-REVIEW-EVIDENCE-CLAIM", "result_commit");
        var reviewArtifact = CreateReviewArtifact(
            task.TaskId,
            completionClaim: new ReviewClosureCompletionClaimSummary
            {
                Required = true,
                Status = "partial",
                PresentFields = ["changed_files"],
                MissingFields = ["tests_run", "next_recommendation"],
                EvidencePaths = [".ai/artifacts/worker-executions/T-REVIEW-EVIDENCE-CLAIM.json"],
                NextRecommendation = "ask worker to resubmit completion claim fields",
            });
        var workerArtifact = CreateWorkerArtifact(
            task.TaskId,
            worktreePath: string.Empty,
            relativePath: "src/Synthetic/Projection.cs");

        var projection = new ReviewEvidenceProjectionService(workspace.RootPath, new StubGitClient())
            .Build(task, reviewArtifact, workerArtifact);

        Assert.Equal("partial", projection.CompletionClaimStatus);
        Assert.True(projection.CompletionClaimRequired);
        Assert.Equal(["changed_files"], projection.CompletionClaimPresentFields);
        Assert.Equal(["tests_run", "next_recommendation"], projection.CompletionClaimMissingFields);
        Assert.Equal([".ai/artifacts/worker-executions/T-REVIEW-EVIDENCE-CLAIM.json"], projection.CompletionClaimEvidencePaths);
        Assert.Equal("ask worker to resubmit completion claim fields", projection.CompletionClaimNextRecommendation);
        Assert.Contains("Claim is not lifecycle truth", projection.CompletionClaimSummary, StringComparison.Ordinal);
        Assert.Equal("failed", projection.HostValidationStatus);
        Assert.True(projection.HostValidationRequired);
        Assert.Contains("completion_claim_not_present:partial", projection.HostValidationBlockers);
        Assert.Contains("ReviewBundle evidence", projection.HostValidationSummary, StringComparison.Ordinal);
        Assert.False(projection.CanFinalApprove);
    }

    [Fact]
    public void Build_ProjectsAppliedWritebackAfterWorkspaceCleanup()
    {
        using var workspace = new TemporaryWorkspace();
        var removedWorktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-REVIEW-EVIDENCE-003");
        var task = CreateTask("T-REVIEW-EVIDENCE-003", "writeback");
        var reviewArtifact = CreateReviewArtifact(
            task.TaskId,
            new ReviewWritebackRecord
            {
                Applied = true,
                AppliedAt = DateTimeOffset.UtcNow,
                SourcePath = removedWorktreePath,
                ResultCommit = "abc123",
                Files = ["src/Synthetic/Projection.cs"],
                Summary = "Materialized 1 approved file.",
            });
        var workerArtifact = CreateWorkerArtifact(task.TaskId, removedWorktreePath, "src/Synthetic/Projection.cs");

        var projection = new ReviewEvidenceProjectionService(workspace.RootPath, new StubGitClient())
            .Build(task, reviewArtifact, workerArtifact);

        Assert.Equal("writeback_applied", projection.Status);
        Assert.False(projection.CanFinalApprove);
        Assert.False(projection.CanWritebackProceed);
        Assert.False(projection.WillApplyWriteback);
        Assert.True(projection.WillCaptureResultCommit);
        Assert.Contains("writeback is applied", projection.Summary, StringComparison.Ordinal);
        Assert.Empty(projection.MissingAfterWriteback);
        Assert.Null(projection.WritebackFailureMessage);
    }

    private static TaskNode CreateTask(string taskId, string evidenceType)
    {
        return new TaskNode
        {
            TaskId = taskId,
            Title = "Project review evidence",
            Status = DomainTaskStatus.Review,
            Scope = ["src/Synthetic/Projection.cs"],
            Acceptance = ["review evidence is projected"],
            AcceptanceContract = new AcceptanceContract
            {
                ContractId = $"AC-{taskId}",
                Title = $"Acceptance contract for {taskId}",
                Status = AcceptanceContractLifecycleStatus.HumanReview,
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement
                    {
                        Type = evidenceType,
                        Description = $"Projection requires {evidenceType}.",
                    },
                ],
            },
        };
    }

    private static PlannerReviewArtifact CreateReviewArtifact(
        string taskId,
        ReviewWritebackRecord? writeback = null,
        string patchSummary = "files=1; added=3; removed=0; estimated=False; paths=src/Synthetic/Projection.cs",
        ReviewClosureCompletionClaimSummary? completionClaim = null)
    {
        return new PlannerReviewArtifact
        {
            TaskId = taskId,
            Review = new PlannerReview
            {
                Verdict = PlannerVerdict.PauseForReview,
                Reason = "Waiting for human approval.",
                DecisionStatus = ReviewDecisionStatus.PendingReview,
                AcceptanceMet = true,
            },
            ResultingStatus = DomainTaskStatus.Review,
            TransitionReason = "Validated work stopped at the review boundary.",
            PlannerComment = "Validated work stopped at the review boundary.",
            PatchSummary = patchSummary,
            ValidationPassed = true,
            ValidationEvidence = ["projection validation passed"],
            SafetyOutcome = SafetyOutcome.Allow,
            SafetyIssues = [],
            Writeback = writeback ?? new ReviewWritebackRecord(),
            ClosureBundle = completionClaim is null
                ? new ReviewClosureBundle()
                : new ReviewClosureBundle
                {
                    CompletionClaim = completionClaim,
                },
        };
    }

    private static WorkerExecutionArtifact CreateWorkerArtifact(
        string taskId,
        string worktreePath,
        string? relativePath,
        IReadOnlyList<string>? commandsExecuted = null)
    {
        return new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                WorktreePath = worktreePath,
                FilesWritten = string.IsNullOrWhiteSpace(relativePath) ? [] : [relativePath],
                CommandsExecuted = commandsExecuted ?? Array.Empty<string>(),
            },
        };
    }

    private sealed class RepositoryGitClient : StubGitClient
    {
        public override bool IsRepository(string repoRoot)
        {
            return true;
        }
    }
}
