namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentFailureRecoveryClosure(RuntimeAgentFailureRecoveryClosureSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent failure classification and recovery closure",
            $"Boundary document: {surface.BoundaryDocumentPath}",
            $"Runtime failure model path: {surface.RuntimeFailureModelPath}",
            $"Retry policy path: {surface.RetryPolicyPath}",
            $"Recovery policy path: {surface.RecoveryPolicyPath}",
            $"Runtime consistency path: {surface.RuntimeConsistencyPath}",
            $"Lifecycle reconciliation path: {surface.LifecycleReconciliationPath}",
            $"Expired-run classification path: {surface.ExpiredRunClassificationPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Truth owner: {surface.TruthOwner}",
            $"Recovery ownership: {surface.RecoveryOwnership}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            $"Retryable failure kinds: {surface.RetryableFailureKinds.Count}",
        };

        lines.AddRange(surface.RetryableFailureKinds.Select(item => $"- failure-kind: {item}"));
        lines.Add($"Recovery outcomes: {surface.RecoveryOutcomes.Count}");
        lines.AddRange(surface.RecoveryOutcomes.Select(item => $"- recovery-outcome: {item}"));
        lines.Add($"Read-only visibility commands: {surface.ReadOnlyVisibilityCommands.Count}");
        lines.AddRange(surface.ReadOnlyVisibilityCommands.Select(item => $"- visibility-command: {item}"));
        lines.Add($"Operator handoff commands: {surface.OperatorHandoffCommands.Count}");
        lines.AddRange(surface.OperatorHandoffCommands.Select(item => $"- handoff-command: {item}"));
        lines.Add($"Failure classes: {surface.FailureClasses.Count}");

        foreach (var failureClass in surface.FailureClasses)
        {
            lines.Add($"- failure-class: {failureClass.FailureClassId}");
            lines.Add($"  summary: {failureClass.Summary}");
            lines.Add($"  retry posture: {failureClass.RetryPosture}");
            lines.Add($"  runtime outcome: {failureClass.RuntimeOutcome}");
            lines.Add($"  operator handoff: {failureClass.OperatorHandoff}");
            lines.Add($"  failure kinds: {(failureClass.FailureKinds.Count == 0 ? "(none)" : string.Join(", ", failureClass.FailureKinds))}");
            lines.Add($"  signals: {(failureClass.Signals.Count == 0 ? "(none)" : string.Join(", ", failureClass.Signals))}");
            lines.Add($"  governing refs: {(failureClass.GoverningRefs.Count == 0 ? "(none)" : string.Join(", ", failureClass.GoverningRefs))}");
            lines.AddRange(failureClass.NonClaims.Select(item => $"  non-claim: {item}"));
        }

        lines.Add($"Blocked behaviors: {surface.BlockedBehaviors.Count}");
        lines.AddRange(surface.BlockedBehaviors.Select(item => $"- blocked-behavior: {item}"));
        lines.Add($"Non-claims: {surface.NonClaims.Count}");
        lines.AddRange(surface.NonClaims.Select(item => $"- {item}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        lines.Add($"Validation warnings: {surface.Warnings.Count}");
        lines.AddRange(surface.Warnings.Select(warning => $"- warning: {warning}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
