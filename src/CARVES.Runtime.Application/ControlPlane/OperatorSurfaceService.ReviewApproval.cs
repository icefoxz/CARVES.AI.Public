using System.Text.Json;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Platform.SurfaceModels;
using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult ApproveReview(
        string taskId,
        string reason,
        bool autoContinueAfterApprove = true,
        bool provisional = false)
    {
        var task = taskGraphService.GetTask(taskId);
        if (task.Status != DomainTaskStatus.Review)
        {
            return OperatorCommandResult.Failure($"Task {taskId} is not in REVIEW state.");
        }

        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(taskId);
        if (reviewArtifact is null)
        {
            return OperatorCommandResult.Failure($"No review artifact exists for {taskId}.");
        }

        var roleGovernancePolicy = runtimePolicyBundleService.LoadRoleGovernancePolicy();
        var approvalSeparationViolation = TaskRoleBindingMetadata.EvaluateApprovalSeparation(task, roleGovernancePolicy);
        if (!string.IsNullOrWhiteSpace(approvalSeparationViolation))
        {
            return OperatorCommandResult.Failure(approvalSeparationViolation);
        }

        var workerArtifact = artifactRepository.TryLoadWorkerExecutionArtifact(taskId);
        if (provisional && !AllowsProvisionalAcceptance(task))
        {
            return OperatorCommandResult.Failure(
                $"Task {taskId} does not allow provisional acceptance under its acceptance contract.");
        }

        var modeEReviewPreflight = CreateModeEReviewPreflightService().Build(taskId);
        if (modeEReviewPreflight.Applies
            && !modeEReviewPreflight.CanProceedToReviewApproval
            && !(provisional && modeEReviewPreflight.CanProceedToProvisionalApproval))
        {
            return OperatorCommandResult.Failure(BuildModeEReviewPreflightFailure(taskId, modeEReviewPreflight));
        }

        var evidenceBeforeWriteback = reviewEvidenceGateService.EvaluateBeforeWriteback(task, reviewArtifact, workerArtifact);
        if (!provisional && !evidenceBeforeWriteback.IsSatisfied)
        {
            return OperatorCommandResult.Failure(evidenceBeforeWriteback.BuildFailureMessage(taskId));
        }

        var writebackPreview = reviewWritebackService.Preview(reviewArtifact, workerArtifact);
        if (!writebackPreview.CanProceed)
        {
            return OperatorCommandResult.Failure(
                writebackPreview.FailureMessage ?? $"Review writeback failed for {taskId}.");
        }

        var projectedEvidenceAfterWriteback = reviewEvidenceGateService.EvaluateProjectedAfterWriteback(
            task,
            reviewArtifact,
            workerArtifact,
            writebackPreview.ToEvidenceProjection());
        if (!provisional && !projectedEvidenceAfterWriteback.IsSatisfied)
        {
            return OperatorCommandResult.Failure(projectedEvidenceAfterWriteback.BuildFailureMessage(taskId));
        }

        var closurePatchPaths = ResolveClosurePatchPaths(
            reviewArtifact,
            workerArtifact,
            writebackPreview.Files);
        var finalHostValidation = provisional
            ? null
            : BuildFinalReviewHostValidation(task, reviewArtifact, workerArtifact);
        var intendedApprovalReview = new PlannerReview
        {
            Verdict = PlannerVerdict.Complete,
            Reason = reason,
            DecisionStatus = ReviewDecisionStatus.Approved,
            AcceptanceMet = true,
            BoundaryPreserved = true,
            ScopeDriftDetected = false,
        };
        if (!provisional)
        {
            var closurePreview = reviewArtifactFactory.RecordDecision(
                reviewArtifact,
                task,
                intendedApprovalReview,
                DomainTaskStatus.Completed,
                reason,
                closurePatchPaths: closurePatchPaths,
                hostValidationOverride: finalHostValidation);
            if (!closurePreview.ClosureBundle.ClosureDecision.WritebackAllowed)
            {
                return OperatorCommandResult.Failure(
                    BuildClosureDecisionFailure(taskId, closurePreview.ClosureBundle.ClosureDecision));
            }
        }

        var writebackAttempt = reviewWritebackService.Apply(
            reviewArtifact,
            workerArtifact,
            provisional ? "provisionally accepted" : "approved");
        if (!writebackAttempt.CanProceed)
        {
            return OperatorCommandResult.Failure(writebackAttempt.FailureMessage ?? $"Review writeback failed for {taskId}.");
        }

        var evidenceAfterWriteback = reviewEvidenceGateService.EvaluateAfterWriteback(
            task,
            reviewArtifact,
            workerArtifact,
            writebackAttempt.Record);
        if (!provisional && !evidenceAfterWriteback.IsSatisfied)
        {
            return OperatorCommandResult.Failure(evidenceAfterWriteback.BuildFailureMessage(taskId));
        }

        var review = provisional
            ? new PlannerReview
            {
                Verdict = PlannerVerdict.PauseForReview,
                Reason = reason,
                DecisionStatus = ReviewDecisionStatus.ProvisionalAccepted,
                DecisionDebt = BuildDecisionDebt(reason, evidenceAfterWriteback),
                AcceptanceMet = true,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
                FollowUpSuggestions =
                [
                    "Keep the task at the review boundary until the provisional debt is discharged.",
                ],
            }
            : new PlannerReview
            {
                Verdict = PlannerVerdict.Complete,
                Reason = reason,
                DecisionStatus = ReviewDecisionStatus.Approved,
                AcceptanceMet = evidenceAfterWriteback.IsSatisfied,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
            };

        task.SetResultCommit(writebackAttempt.Record.ResultCommit ?? task.ResultCommit);
        task = ApplyReviewDecision(
            task,
            provisional ? DomainTaskStatus.Review : DomainTaskStatus.Completed,
            review,
            provisional ? AcceptanceContractHumanDecision.ProvisionalAccept : AcceptanceContractHumanDecision.Accept);
        task = new PlannerEmergenceService(paths, taskGraphService, executionRunService).CaptureReviewOutcome(
            task,
            provisional ? PlannerVerdict.PauseForReview : PlannerVerdict.Complete,
            reason);
        if (!provisional)
        {
            task = executionRunService.TryReconcileInactiveTaskRun(
                       task,
                       "Review approved the task before the follow-up execution run materialized; retained the run as informational history only.")
                   ?? task;
        }

        taskGraphService.ReplaceTask(task);

        var updatedReview = reviewArtifactFactory.RecordDecision(
            reviewArtifact,
            task,
            review,
            provisional ? DomainTaskStatus.Review : DomainTaskStatus.Completed,
            reason,
            writebackAttempt.Record.ResultCommit,
            writebackAttempt.Record,
            ResolveClosurePatchPaths(reviewArtifact, workerArtifact, writebackAttempt.Record.Files),
            hostValidationOverride: finalHostValidation);
        artifactRepository.SavePlannerReviewArtifact(updatedReview);
        new RuntimeEvidenceStoreService(paths).RecordReview(
            task,
            updatedReview,
            sourceEvidenceIds: ResolveReviewSourceEvidenceIds(taskId));

        if (!provisional)
        {
            artifactRepository.SaveMergeCandidateArtifact(reviewArtifactFactory.CreateMergeCandidate(updatedReview, reason));
        }

        var releasedManagedWorkspaceLeaseCount = !provisional && writebackAttempt.Record.Applied
            ? managedWorkspaceLeaseService.ReleaseForTask(taskId)
            : 0;

        var session = provisional
            ? devLoopService.GetSession()
            : devLoopService.ResolveReviewDecision(taskId, $"Review approved for {taskId}: {reason}");
        if (!provisional)
        {
            var graphAfterApproval = taskGraphService.Load();
            var unlockedTaskIds = graphAfterApproval.ListTasks()
                .Where(candidate => candidate.Status == DomainTaskStatus.Pending)
                .Where(candidate => candidate.Dependencies.Contains(taskId, StringComparer.Ordinal))
                .Where(candidate => AcceptanceContractExecutionGate.IsReadyForDispatch(candidate, graphAfterApproval.CompletedTaskIds()))
                .Select(candidate => candidate.TaskId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            foreach (var unlockedTaskId in unlockedTaskIds)
            {
                devLoopService.QueuePlannerWake(
                    PlannerWakeReason.DependencyUnlocked,
                    PlannerWakeSourceKind.DependencyUnlock,
                    $"Review approval for {taskId} unlocked dependent task {unlockedTaskId}.",
                    $"{unlockedTaskId} became ready after review approval for {taskId}.",
                    unlockedTaskId);
            }
        }

        markdownSyncService.Sync(taskGraphService.Load(), session: devLoopService.GetSession());
        var lines = new List<string>
        {
            provisional
                ? $"Provisionally accepted review for {taskId}; task remains in REVIEW until the recorded debt is cleared."
                : $"Approved review for {taskId}; task is now COMPLETED and merge-candidate evidence was emitted.",
        };
        if (writebackAttempt.Record.Applied)
        {
            lines.Add(writebackAttempt.Record.Summary);
        }

        if (releasedManagedWorkspaceLeaseCount > 0)
        {
            lines.Add($"Released {releasedManagedWorkspaceLeaseCount} managed workspace lease(s) for {taskId}.");
        }

        var dispatch = dispatchProjectionService.Build(taskGraphService.Load(), session, systemConfig.MaxParallelTasks);
        if (!provisional
            && autoContinueAfterApprove
            && session is not null
            && dispatch.AutoContinueOnApprove
            && string.Equals(dispatch.State, "dispatchable", StringComparison.Ordinal)
            && session.Status is not (Carves.Runtime.Domain.Runtime.RuntimeSessionStatus.Paused or Carves.Runtime.Domain.Runtime.RuntimeSessionStatus.Stopped or Carves.Runtime.Domain.Runtime.RuntimeSessionStatus.Failed))
        {
            var autoContinueGate = RuntimeRoleModeExecutionGate.EvaluateReviewAutoContinue(roleGovernancePolicy);
            if (autoContinueGate.Allowed)
            {
                var cycle = devLoopService.Tick(dryRun: false);
                lines.Add($"Auto-continued after approve: {cycle.Message}");
            }
            else
            {
                lines.Add($"Auto-continue after approve held: {autoContinueGate.Summary} outcome={autoContinueGate.Outcome}.");
            }
        }

        return new OperatorCommandResult(0, lines);
    }

    private static IReadOnlyList<string> ResolveClosurePatchPaths(
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact,
        IReadOnlyList<string> writebackFiles)
    {
        return PlannerReviewArtifactFactory.ResolveClosurePatchPaths(reviewArtifact, workerArtifact, writebackFiles);
    }

    private static string BuildClosureDecisionFailure(string taskId, ReviewClosureDecision decision)
    {
        var blockers = decision.Blockers.Count == 0
            ? "(none)"
            : string.Join(", ", decision.Blockers);
        return $"Cannot approve review for {taskId}: closure decision blocks writeback_allowed=false; status={decision.Status}; decision={decision.Decision}; blockers={blockers}.";
    }

    private ReviewClosureHostValidationSummary? BuildFinalReviewHostValidation(
        TaskNode task,
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact)
    {
        if (!RequiresFinalReviewHostValidation(reviewArtifact, workerArtifact))
        {
            return null;
        }

        ResultEnvelope envelope;
        try
        {
            envelope = resultIngestionService.Read(task.TaskId);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException)
        {
            return BuildFinalReviewHostValidationSummary(
                new ResultValidityDecision(
                    false,
                    "result_envelope_missing",
                    $"Host validation cannot approve review writeback for '{task.TaskId}' because the result envelope is missing or invalid: {exception.Message}"),
                workerArtifact);
        }

        var latestRunId = executionRunService.ListRuns(task.TaskId).LastOrDefault()?.RunId;
        var decision = new ResultValidityPolicy(paths).Evaluate(
            task.TaskId,
            envelope,
            workerArtifact,
            latestRunId);
        return BuildFinalReviewHostValidationSummary(decision, workerArtifact);
    }

    private static bool RequiresFinalReviewHostValidation(
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact)
    {
        var workerClaim = workerArtifact?.Result.CompletionClaim;
        if (workerClaim is not null
            && (workerClaim.Required
                || !string.Equals(workerClaim.Status, "not_required", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(workerClaim.PacketId)
                || !string.IsNullOrWhiteSpace(workerClaim.SourceExecutionPacketId)
                || !string.Equals(workerClaim.PacketValidationStatus, "not_evaluated", StringComparison.OrdinalIgnoreCase)
                || workerClaim.PacketValidationBlockers.Count > 0))
        {
            return true;
        }

        var closureClaim = reviewArtifact.ClosureBundle.CompletionClaim;
        if (closureClaim.Required
            || !string.Equals(closureClaim.Status, "not_recorded", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(closureClaim.PacketId)
            || !string.IsNullOrWhiteSpace(closureClaim.SourceExecutionPacketId)
            || !string.Equals(closureClaim.PacketValidationStatus, "not_evaluated", StringComparison.OrdinalIgnoreCase)
            || closureClaim.PacketValidationBlockers.Count > 0)
        {
            return true;
        }

        return reviewArtifact.ClosureBundle.HostValidation.Required;
    }

    private static ReviewClosureHostValidationSummary BuildFinalReviewHostValidationSummary(
        ResultValidityDecision decision,
        WorkerExecutionArtifact? workerArtifact)
    {
        var claim = workerArtifact?.Result.CompletionClaim;
        var evidence = decision.Evidence ?? workerArtifact?.Evidence;
        return new ReviewClosureHostValidationSummary
        {
            Status = decision.Valid ? "passed" : "failed",
            Required = true,
            ReasonCode = decision.ReasonCode,
            Message = decision.Valid
                ? "Final Host validation accepted the result envelope and worker evidence before review writeback."
                : decision.Message,
            WorkerPacketId = claim?.PacketId,
            SourceExecutionPacketId = claim?.SourceExecutionPacketId,
            CompletionClaimStatus = claim?.Status ?? "not_recorded",
            CompletionClaimPacketValidationStatus = claim?.PacketValidationStatus ?? "not_evaluated",
            CompletionClaimHostValidationRequired = claim?.HostValidationRequired ?? true,
            CompletionClaimClaimsTruthAuthority = claim?.ClaimIsTruth ?? false,
            Blockers = decision.Valid ? Array.Empty<string>() : [decision.ReasonCode],
            EvidenceRefs = BuildFinalReviewHostValidationEvidenceRefs(evidence),
            Notes =
            [
                "Final Host validation was evaluated during approve-review before review writeback.",
                "This validation uses the current result envelope and worker artifact; stale ReviewBundle claims cannot bypass it.",
                "The result remains a candidate until review approval and writeback complete.",
            ],
        };
    }

    private static IReadOnlyList<string> BuildFinalReviewHostValidationEvidenceRefs(ExecutionEvidence? evidence)
    {
        if (evidence is null)
        {
            return Array.Empty<string>();
        }

        return new[]
            {
                evidence.EvidencePath,
                evidence.CommandLogRef,
                evidence.BuildOutputRef,
                evidence.TestOutputRef,
                evidence.PatchRef,
            }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private ModeEReviewPreflightService CreateModeEReviewPreflightService()
    {
        var memoryPatternWritebackRouteAuthorizationService = new MemoryPatternWritebackRouteAuthorizationService(repoRoot);
        return new ModeEReviewPreflightService(
            paths,
            taskGraphService,
            artifactRepository,
            new PacketEnforcementService(paths, taskGraphService, artifactRepository),
            reviewEvidenceGateService,
            memoryPatternWritebackRouteAuthorizationService);
    }

    private static string BuildModeEReviewPreflightFailure(
        string taskId,
        RuntimeBrokeredReviewPreflightSurface preflight)
    {
        var blockers = preflight.Blockers.Count == 0
            ? "(none)"
            : string.Join(", ", preflight.Blockers.Select(static blocker => blocker.BlockerId));
        return $"Cannot approve review for {taskId}: Mode E review preflight is blocked before writeback: {blockers}. {preflight.Summary}";
    }
}
