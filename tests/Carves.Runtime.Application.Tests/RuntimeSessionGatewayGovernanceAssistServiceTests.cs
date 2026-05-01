using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeSessionGatewayGovernanceAssistServiceTests
{
    [Fact]
    public void Build_ProjectsObserveAssistGovernanceTruth()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md", "# plan");
        workspace.WriteFile("docs/session-gateway/release-surface.md", "# release");
        workspace.WriteFile("docs/session-gateway/repeatability-readiness.md", "# repeatability");
        workspace.WriteFile("docs/session-gateway/governance-assist.md", "# governance assist");

        var service = new RuntimeSessionGatewayGovernanceAssistService(
            workspace.RootPath,
            () => new RuntimeSessionGatewayRepeatabilitySurface
            {
                OverallPosture = "repeatable_private_alpha_ready",
                ProgramClosureVerdict = "program_closure_complete",
                ContinuationGateOutcome = "closure_review_completed",
                ProviderVisibilitySummary = "actionability_issues=0; optional=0; disabled=1",
                SupportedIntents = ["discuss", "plan", "governed_run"],
                RecentGatewayTasks =
                [
                    new RuntimeSessionGatewayRecentTaskSurface
                    {
                        TaskId = "T-CARD-580-001",
                        CardId = "CARD-580",
                        Title = "Review Session Gateway result commit evidence",
                        Status = "review",
                        UpdatedAt = DateTimeOffset.Parse("2026-04-05T07:20:00+00:00"),
                        RecoveryAction = "none",
                        RecoveryReason = "(none)",
                        ReviewArtifactAvailable = true,
                        WorkerExecutionArtifactAvailable = true,
                        ProviderArtifactAvailable = false,
                        ReviewEvidenceStatus = "post_writeback_gap",
                        ReviewCanFinalApprove = false,
                        ReviewEvidenceSummary = "Final approval blocked after writeback projection: missing result_commit.",
                        MissingReviewEvidence = ["result_commit"],
                        AcceptanceContractBindingState = "missing_projection",
                        AcceptanceContractId = "AC-T-CARD-580-001",
                        AcceptanceContractStatus = "HumanReview",
                        AcceptanceContractEvidenceRequired = ["result_commit"],
                    },
                    new RuntimeSessionGatewayRecentTaskSurface
                    {
                        TaskId = "T-CARD-580-002",
                        CardId = "CARD-580",
                        Title = "Re-check Session Gateway delegated review evidence",
                        Status = "review",
                        UpdatedAt = DateTimeOffset.Parse("2026-04-05T07:18:00+00:00"),
                        RecoveryAction = "none",
                        RecoveryReason = "(none)",
                        ReviewArtifactAvailable = true,
                        WorkerExecutionArtifactAvailable = true,
                        ProviderArtifactAvailable = false,
                        ReviewEvidenceStatus = "post_writeback_gap",
                        ReviewCanFinalApprove = false,
                        ReviewEvidenceSummary = "Final approval blocked after writeback projection: missing result_commit.",
                        MissingReviewEvidence = ["result_commit"],
                        AcceptanceContractBindingState = "projected",
                        AcceptanceContractId = "AC-T-CARD-580-002",
                        AcceptanceContractStatus = "HumanReview",
                        ProjectedAcceptanceContractId = "AC-T-CARD-580-002",
                        ProjectedAcceptanceContractStatus = "HumanReview",
                        AcceptanceContractEvidenceRequired = ["result_commit"],
                    },
                    new RuntimeSessionGatewayRecentTaskSurface
                    {
                        TaskId = "T-CARD-581-001",
                        CardId = "CARD-581",
                        Title = "Implement Session Gateway private alpha repeatability readiness",
                        Status = "review",
                        UpdatedAt = DateTimeOffset.Parse("2026-04-05T07:14:16+00:00"),
                        RecoveryAction = "none",
                        RecoveryReason = "(none)",
                        ReviewArtifactAvailable = true,
                        WorkerExecutionArtifactAvailable = true,
                        ProviderArtifactAvailable = true,
                        ReviewEvidenceStatus = "final_ready",
                        ReviewCanFinalApprove = true,
                        ReviewEvidenceSummary = "Final approval can proceed; required acceptance evidence is already present.",
                        AcceptanceContractBindingState = "projected",
                        AcceptanceContractId = "AC-T-CARD-581-001",
                        AcceptanceContractStatus = "Accepted",
                        ProjectedAcceptanceContractId = "AC-T-CARD-581-001",
                        ProjectedAcceptanceContractStatus = "Accepted",
                        AcceptanceContractEvidenceRequired = ["result_commit"],
                    },
                ],
                RecentTimelineEntries = [],
                OperatorProofContract = RuntimeSessionGatewayOperatorProofContractCatalog.BuildPrivateAlphaContract(),
                IsValid = true,
            });

        var surface = service.Build();

        Assert.Equal("runtime-session-gateway-governance-assist", surface.SurfaceId);
        Assert.Equal("governance_assist_observe_ready", surface.OverallPosture);
        Assert.Equal("observe_assist", surface.DynamicGateMode);
        Assert.True(surface.ObserveOnly);
        Assert.False(surface.BlockingAuthority);
        Assert.Equal("program_closure_complete", surface.ProgramClosureVerdict);
        Assert.Equal(5, surface.ArtifactWeightLedger.Count);
        Assert.Equal(6, surface.ChangePressures.Count);
        Assert.Equal(3, surface.RecentReviewTaskCount);
        Assert.Equal(1, surface.ReviewFinalReadyCount);
        Assert.Equal(2, surface.ReviewEvidenceBlockedCount);
        Assert.Equal(0, surface.ReviewEvidenceUnavailableCount);
        Assert.Equal(0, surface.WorkerCompletionClaimGapCount);
        Assert.Equal(2, surface.AcceptanceContractProjectedCount);
        Assert.Equal(1, surface.AcceptanceContractBindingGapCount);
        Assert.True(surface.DecompositionCandidates.Count >= 5);
        Assert.Single(surface.ReviewEvidencePlaybook);
        Assert.Equal("acceptance_contract_binding_gaps", surface.ChangePressures[0].PressureKind);
        Assert.Equal("review_evidence_blockers", surface.ChangePressures[1].PressureKind);
        Assert.Equal("real_world_proof_gap", surface.ChangePressures[2].PressureKind);
        Assert.Equal("project-acceptance-contract-binding-t-card-580-001", surface.DecompositionCandidates[0].CandidateId);
        Assert.Equal("clear-review-evidence-t-card-580-001", surface.DecompositionCandidates[1].CandidateId);
        Assert.Equal(SessionGatewayOperatorWaitStates.WaitingOperatorSetup, surface.OperatorProofContract.CurrentOperatorState);
        Assert.Contains(surface.NonClaims, item => item.Contains("does not auto-block execution", StringComparison.Ordinal));
        Assert.Contains(surface.ArtifactWeightLedger, entry => entry.Summary.Contains("evidence-blocked", StringComparison.Ordinal));
        Assert.Contains(surface.ArtifactWeightLedger, entry =>
            entry.ArtifactKind == "acceptance_contract_binding"
            && entry.Summary.Contains("projection gap", StringComparison.Ordinal)
            && entry.Summary.Contains("T-CARD-580-001", StringComparison.Ordinal));
        Assert.Contains(surface.ChangePressures, entry =>
            entry.PressureKind == "review_evidence_blockers"
            && entry.Level == "high"
            && entry.Summary.Contains("T-CARD-580-001", StringComparison.Ordinal)
            && entry.Summary.Contains("result_commit", StringComparison.Ordinal));
        Assert.Contains(surface.ChangePressures, entry =>
            entry.PressureKind == "acceptance_contract_binding_gaps"
            && entry.Level == "medium"
            && entry.Summary.Contains("T-CARD-580-001", StringComparison.Ordinal)
            && entry.Summary.Contains("missing_projection", StringComparison.Ordinal));
        Assert.Contains(surface.DecompositionCandidates, candidate =>
            candidate.CandidateId == "project-acceptance-contract-binding-t-card-580-001"
            && candidate.BlockingState == "acceptance_contract_binding_gap"
            && candidate.Summary.Contains("missing the projected binding metadata", StringComparison.Ordinal)
            && candidate.SuggestedAction.Contains("worker route", StringComparison.Ordinal));
        Assert.Contains(surface.DecompositionCandidates, candidate =>
            candidate.CandidateId == "clear-review-evidence-t-card-580-001"
            && candidate.BlockingState == "review_evidence_blocked"
            && candidate.Summary.Contains("post_writeback_gap", StringComparison.Ordinal)
            && candidate.SuggestedAction.Contains("result_commit", StringComparison.Ordinal));
        Assert.Contains(surface.ReviewEvidencePlaybook, entry =>
            entry.PlaybookId == "review-evidence-playbook-result-commit"
            && entry.BlockedTaskCount == 2
            && entry.TaskIds.Contains("T-CARD-580-001", StringComparer.Ordinal)
            && entry.TaskIds.Contains("T-CARD-580-002", StringComparer.Ordinal)
            && entry.SuggestedAction.Contains("delegated git worktree", StringComparison.Ordinal));
        Assert.Contains("Highest-priority assist slice", surface.RecommendedNextAction, StringComparison.Ordinal);
        Assert.Contains("Project acceptance contract binding: T-CARD-580-001", surface.RecommendedNextAction, StringComparison.Ordinal);
        Assert.True(surface.IsValid);
        Assert.Empty(surface.Errors);
    }

    [Fact]
    public void Build_ProjectsWorkerCompletionClaimGapsAsObserveOnlyActionability()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md", "# plan");
        workspace.WriteFile("docs/session-gateway/release-surface.md", "# release");
        workspace.WriteFile("docs/session-gateway/repeatability-readiness.md", "# repeatability");
        workspace.WriteFile("docs/session-gateway/governance-assist.md", "# governance assist");

        var service = new RuntimeSessionGatewayGovernanceAssistService(
            workspace.RootPath,
            () => new RuntimeSessionGatewayRepeatabilitySurface
            {
                OverallPosture = "repeatable_private_alpha_ready",
                ProgramClosureVerdict = "program_closure_complete",
                ContinuationGateOutcome = "closure_review_completed",
                ProviderVisibilitySummary = "actionability_issues=0; optional=0; disabled=1",
                SupportedIntents = ["discuss", "plan", "governed_run"],
                RecentGatewayTasks =
                [
                    new RuntimeSessionGatewayRecentTaskSurface
                    {
                        TaskId = "T-CARD-582-001",
                        CardId = "CARD-582",
                        Title = "Review worker completion claim evidence",
                        Status = "review",
                        UpdatedAt = DateTimeOffset.Parse("2026-04-05T07:25:00+00:00"),
                        ReviewArtifactAvailable = true,
                        WorkerExecutionArtifactAvailable = true,
                        ProviderArtifactAvailable = false,
                        ReviewEvidenceStatus = "final_ready",
                        ReviewCanFinalApprove = true,
                        ReviewEvidenceSummary = "Final approval can proceed; required acceptance evidence is already present.",
                        WorkerCompletionClaimStatus = "partial",
                        WorkerCompletionClaimRequired = true,
                        WorkerCompletionClaimSummary = "Worker completion claim partial; missing fields: tests_run, next_recommendation. Claim is not lifecycle truth.",
                        MissingWorkerCompletionClaimFields = ["tests_run", "next_recommendation"],
                        WorkerCompletionClaimEvidencePaths = [".ai/artifacts/worker-executions/T-CARD-582-001.json"],
                        WorkerCompletionClaimNextRecommendation = "ask worker to resubmit completion claim fields",
                        AcceptanceContractBindingState = "projected",
                        AcceptanceContractId = "AC-T-CARD-582-001",
                        AcceptanceContractStatus = "HumanReview",
                        ProjectedAcceptanceContractId = "AC-T-CARD-582-001",
                        ProjectedAcceptanceContractStatus = "HumanReview",
                        AcceptanceContractEvidenceRequired = ["result_commit"],
                    },
                ],
                RecentTimelineEntries = [],
                OperatorProofContract = RuntimeSessionGatewayOperatorProofContractCatalog.BuildPrivateAlphaContract(),
                IsValid = true,
            });

        var surface = service.Build();

        Assert.Equal(1, surface.WorkerCompletionClaimGapCount);
        Assert.Contains(surface.ChangePressures, entry =>
            entry.PressureKind == "worker_completion_claim_gaps"
            && entry.Summary.Contains("T-CARD-582-001", StringComparison.Ordinal)
            && entry.Summary.Contains("not lifecycle truth", StringComparison.Ordinal));
        Assert.Contains(surface.DecompositionCandidates, candidate =>
            candidate.CandidateId == "resubmit-worker-completion-claim-t-card-582-001"
            && candidate.BlockingState == "worker_completion_claim_gap"
            && candidate.Summary.Contains("tests_run, next_recommendation", StringComparison.Ordinal)
            && candidate.SuggestedAction.Contains("resubmit completion claim", StringComparison.Ordinal)
            && candidate.EvidenceReferences.Contains("completion_claim:partial", StringComparer.Ordinal)
            && candidate.EvidenceReferences.Contains("completion_claim_missing:tests_run", StringComparer.Ordinal));
        Assert.Contains(surface.RecentGatewayTasks, task =>
            task.TaskId == "T-CARD-582-001"
            && task.WorkerCompletionClaimStatus == "partial"
            && task.MissingWorkerCompletionClaimFields.Contains("next_recommendation", StringComparer.Ordinal));
        Assert.Contains(surface.NonClaims, item => item.Contains("Worker completion claims stay worker declarations", StringComparison.Ordinal));
        Assert.True(surface.IsValid);
        Assert.Empty(surface.Errors);
    }
}
