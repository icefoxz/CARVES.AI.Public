using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeSessionGatewayRepeatability(RuntimeSessionGatewayRepeatabilitySurface surface)
    {
        var lines = new List<string>
        {
            "Runtime Session Gateway repeatability readiness",
            $"Execution plan: {surface.ExecutionPlanPath}",
            $"Release surface: {surface.ReleaseSurfacePath}",
            $"Repeatability doc: {surface.RepeatabilityReadinessPath}",
            $"Dogfood validation doc: {surface.DogfoodValidationPath}",
            $"Operator proof contract doc: {surface.OperatorProofContractPath}",
            $"Alpha setup doc: {surface.AlphaSetupPath}",
            $"Alpha quickstart doc: {surface.AlphaQuickstartPath}",
            $"Known limitations doc: {surface.KnownLimitationsPath}",
            $"Bug report bundle doc: {surface.BugReportBundlePath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Private alpha handoff posture: {surface.PrivateAlphaHandoffPosture}",
            $"Dogfood validation posture: {surface.DogfoodValidationPosture}",
            $"Program closure verdict: {surface.ProgramClosureVerdict}",
            $"Continuation gate outcome: {surface.ContinuationGateOutcome}",
            $"Broker mode: {surface.BrokerMode}",
            $"Truth owner: {surface.TruthOwner}",
            $"Repeatability ownership: {surface.RepeatabilityOwnership}",
            $"Thin shell route: {surface.ThinShellRoute}",
            $"Session collection route: {surface.SessionCollectionRoute}",
            $"Message route: {surface.MessageRouteTemplate}",
            $"Events route: {surface.EventsRouteTemplate}",
            $"Accepted operation route: {surface.AcceptedOperationRouteTemplate}",
            $"Provider visibility: {surface.ProviderVisibilitySummary}",
            $"Supported intents: {string.Join(", ", surface.SupportedIntents)}",
            $"Recommended next action: {surface.RecommendedNextAction}",
        };

        AppendSessionGatewayOperatorProofContract(lines, surface.OperatorProofContract);
        lines.Add($"Recovery commands: {surface.RecoveryCommands.Count}");
        lines.AddRange(surface.RecoveryCommands.Select(item => $"- recovery: {item}"));
        lines.Add($"Artifact bundle commands: {surface.ArtifactBundleCommands.Count}");
        lines.AddRange(surface.ArtifactBundleCommands.Select(item => $"- bundle: {item}"));
        lines.Add($"Rerun commands: {surface.RerunCommands.Count}");
        lines.AddRange(surface.RerunCommands.Select(item => $"- rerun: {item}"));
        lines.Add($"Provider statuses: {surface.ProviderStatuses.Count}");
        lines.AddRange(surface.ProviderStatuses.Select(item => $"- provider-status: {item}"));
        lines.Add($"Recent gateway tasks: {surface.RecentGatewayTasks.Count}");
        lines.AddRange(surface.RecentGatewayTasks.Select(task =>
            $"- task: {task.TaskId}; card={task.CardId ?? "(none)"}; status={task.Status}; updated_at={task.UpdatedAt:O}; recovery={task.RecoveryAction}; review_artifact={task.ReviewArtifactAvailable.ToString().ToLowerInvariant()}; worker_execution_artifact={task.WorkerExecutionArtifactAvailable.ToString().ToLowerInvariant()}; provider_artifact={task.ProviderArtifactAvailable.ToString().ToLowerInvariant()}; contract_binding={task.AcceptanceContractBindingState}; contract={FormatGatewayContractSummary(task.AcceptanceContractId, task.AcceptanceContractStatus)}; projected_contract={FormatGatewayContractSummary(task.ProjectedAcceptanceContractId, task.ProjectedAcceptanceContractStatus)}; contract_evidence={FormatGatewayContractEvidence(task.AcceptanceContractEvidenceRequired)}; review_evidence={task.ReviewEvidenceStatus}; can_final_approve={task.ReviewCanFinalApprove}; missing_review_evidence={FormatGatewayMissingReviewEvidence(task.MissingReviewEvidence)}; worker_completion_claim={FormatGatewayWorkerCompletionClaim(task)}; missing_worker_completion_claim_fields={FormatGatewayWorkerCompletionClaimMissingFields(task)}; worker_completion_claim_evidence={FormatGatewayWorkerCompletionClaimEvidencePaths(task)}; review_summary={task.ReviewEvidenceSummary}"));
        lines.Add($"Recent timeline entries: {surface.RecentTimelineEntries.Count}");
        lines.AddRange(surface.RecentTimelineEntries.Select(entry =>
            $"- timeline: {entry.RecordedAt:O}; kind={entry.EventKind}; stage={entry.Stage}; task={entry.TaskId ?? "(none)"}; operation={entry.OperationId ?? "(none)"}; summary={entry.Summary}"));
        lines.Add($"Non-claims: {surface.NonClaims.Count}");
        lines.AddRange(surface.NonClaims.Select(item => $"- {item}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        lines.Add($"Validation warnings: {surface.Warnings.Count}");
        lines.AddRange(surface.Warnings.Select(warning => $"- warning: {warning}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }

    private static string FormatGatewayMissingReviewEvidence(IReadOnlyList<string> missingEvidence)
    {
        return missingEvidence.Count == 0
            ? "(none)"
            : string.Join(", ", missingEvidence);
    }

    private static string FormatGatewayContractSummary(string? contractId, string? contractStatus)
    {
        return $"{contractId ?? "(none)"}@{contractStatus ?? "(none)"}";
    }

    private static string FormatGatewayContractEvidence(IReadOnlyList<string> evidence)
    {
        return evidence.Count == 0
            ? "(none)"
            : string.Join(", ", evidence);
    }

    private static string FormatGatewayWorkerCompletionClaim(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return $"{task.WorkerCompletionClaimStatus}; required={task.WorkerCompletionClaimRequired.ToString().ToLowerInvariant()}";
    }

    private static string FormatGatewayWorkerCompletionClaimMissingFields(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return task.MissingWorkerCompletionClaimFields.Count == 0
            ? "(none)"
            : string.Join(", ", task.MissingWorkerCompletionClaimFields);
    }

    private static string FormatGatewayWorkerCompletionClaimEvidencePaths(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return task.WorkerCompletionClaimEvidencePaths.Count == 0
            ? "(none)"
            : string.Join(", ", task.WorkerCompletionClaimEvidencePaths);
    }
}
