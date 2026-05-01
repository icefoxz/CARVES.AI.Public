using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeSessionGatewayGovernanceAssist(RuntimeSessionGatewayGovernanceAssistSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime Session Gateway governance assist",
            $"Execution plan: {surface.ExecutionPlanPath}",
            $"Release surface: {surface.ReleaseSurfacePath}",
            $"Repeatability doc: {surface.RepeatabilityReadinessPath}",
            $"Governance assist doc: {surface.GovernanceAssistPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Repeatability posture: {surface.RepeatabilityPosture}",
            $"Program closure verdict: {surface.ProgramClosureVerdict}",
            $"Continuation gate outcome: {surface.ContinuationGateOutcome}",
            $"Broker mode: {surface.BrokerMode}",
            $"Truth owner: {surface.TruthOwner}",
            $"Governance assist ownership: {surface.GovernanceAssistOwnership}",
            $"Dynamic gate mode: {surface.DynamicGateMode}",
            $"Observe only: {surface.ObserveOnly}",
            $"Blocking authority: {surface.BlockingAuthority}",
            $"Provider visibility: {surface.ProviderVisibilitySummary}",
            $"Supported intents: {string.Join(", ", surface.SupportedIntents)}",
            $"Artifact weight total: {surface.ArtifactWeightTotal}",
            $"High pressure count: {surface.HighPressureCount}",
            $"Recent review queue: {surface.RecentReviewTaskCount}",
            $"Review final-ready count: {surface.ReviewFinalReadyCount}",
            $"Review evidence blocked count: {surface.ReviewEvidenceBlockedCount}",
            $"Review evidence unavailable count: {surface.ReviewEvidenceUnavailableCount}",
            $"Worker completion claim gap count: {surface.WorkerCompletionClaimGapCount}",
            $"Acceptance contract projected count: {surface.AcceptanceContractProjectedCount}",
            $"Acceptance contract binding gap count: {surface.AcceptanceContractBindingGapCount}",
            $"Recommended next action: {surface.RecommendedNextAction}",
        };

        AppendSessionGatewayOperatorProofContract(lines, surface.OperatorProofContract);
        lines.Add($"Artifact weight ledger: {surface.ArtifactWeightLedger.Count}");
        lines.AddRange(surface.ArtifactWeightLedger.Select(entry =>
            $"- weight: kind={entry.ArtifactKind}; class={entry.WeightClass}; score={entry.WeightScore}; summary={entry.Summary}; evidence={string.Join(", ", entry.EvidenceReferences)}"));
        lines.Add($"Change pressures (highest priority first): {surface.ChangePressures.Count}");
        lines.AddRange(surface.ChangePressures.Select(entry =>
            $"- pressure: kind={entry.PressureKind}; level={entry.Level}; summary={entry.Summary}; evidence={string.Join(", ", entry.EvidenceReferences)}"));
        lines.Add($"Decomposition candidates (highest priority first): {surface.DecompositionCandidates.Count}");
        lines.AddRange(surface.DecompositionCandidates.Select(candidate =>
            $"- candidate: {candidate.CandidateId}; title={candidate.Title}; state={candidate.BlockingState}; proof={candidate.PreferredProofSource}; summary={candidate.Summary}; next={candidate.SuggestedAction}; evidence={string.Join(", ", candidate.EvidenceReferences)}"));
        lines.Add($"Review evidence playbook (highest priority first): {surface.ReviewEvidencePlaybook.Count}");
        lines.AddRange(surface.ReviewEvidencePlaybook.Select(entry =>
            $"- playbook: {entry.PlaybookId}; kind={entry.EvidenceKind}; label={entry.DisplayLabel}; blocked_tasks={entry.BlockedTaskCount}; task_ids={string.Join(", ", entry.TaskIds)}; summary={entry.Summary}; next={entry.SuggestedAction}; evidence={string.Join(", ", entry.EvidenceReferences)}"));
        lines.Add($"Recent gateway tasks: {surface.RecentGatewayTasks.Count}");
        lines.AddRange(surface.RecentGatewayTasks.Select(task =>
            $"- task: {task.TaskId}; card={task.CardId ?? "(none)"}; status={task.Status}; updated_at={task.UpdatedAt:O}; review_artifact={task.ReviewArtifactAvailable.ToString().ToLowerInvariant()}; worker_execution_artifact={task.WorkerExecutionArtifactAvailable.ToString().ToLowerInvariant()}; provider_artifact={task.ProviderArtifactAvailable.ToString().ToLowerInvariant()}; contract_binding={task.AcceptanceContractBindingState}; contract={FormatGatewayContractSummary(task.AcceptanceContractId, task.AcceptanceContractStatus)}; projected_contract={FormatGatewayContractSummary(task.ProjectedAcceptanceContractId, task.ProjectedAcceptanceContractStatus)}; contract_evidence={FormatGatewayContractEvidence(task.AcceptanceContractEvidenceRequired)}; review_evidence={task.ReviewEvidenceStatus}; can_final_approve={task.ReviewCanFinalApprove}; missing_review_evidence={FormatGatewayGovernanceMissingReviewEvidence(task.MissingReviewEvidence)}; worker_completion_claim={FormatGatewayWorkerCompletionClaim(task)}; missing_worker_completion_claim_fields={FormatGatewayWorkerCompletionClaimMissingFields(task)}; worker_completion_claim_evidence={FormatGatewayWorkerCompletionClaimEvidencePaths(task)}; review_summary={task.ReviewEvidenceSummary}"));
        lines.Add($"Non-claims: {surface.NonClaims.Count}");
        lines.AddRange(surface.NonClaims.Select(item => $"- {item}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        lines.Add($"Validation warnings: {surface.Warnings.Count}");
        lines.AddRange(surface.Warnings.Select(warning => $"- warning: {warning}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }

    private static string FormatGatewayGovernanceMissingReviewEvidence(IReadOnlyList<string> missingEvidence)
    {
        return missingEvidence.Count == 0
            ? "(none)"
            : string.Join(", ", missingEvidence);
    }
}
