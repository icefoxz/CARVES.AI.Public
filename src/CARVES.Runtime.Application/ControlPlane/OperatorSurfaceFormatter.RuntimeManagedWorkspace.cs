using Carves.Runtime.Application.Platform.SurfaceModels;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeManagedWorkspace(RuntimeManagedWorkspaceSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime managed workspace",
            $"Policy document: {surface.PolicyDocumentPath}",
            $"Working modes document: {surface.WorkingModesDocumentPath}",
            $"Implementation plan: {surface.ImplementationPlanPath}",
            $"Mode D hardening document: {surface.ModeDHardeningDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Overall posture: {surface.OverallPosture}",
            $"Operational state: {surface.OperationalState}",
            $"Safe to start new execution: {surface.SafeToStartNewExecution}",
            $"Safe to discuss: {surface.SafeToDiscuss}",
            $"Safe to cleanup: {surface.SafeToCleanup}",
            $"Hard mode baseline: {surface.HardModeBaseline}",
            $"Mode D profile: {surface.ModeDProfileId}",
            $"Mode D hardening state: {surface.ModeDHardeningState}",
            $"Scoped workspace boundary: {surface.ScopedWorkspaceBoundary}",
            $"Official truth ingress: {surface.OfficialTruthIngressPolicy}",
            $"Replan trigger policy: {surface.ReplanTriggerPolicy}",
            $"IDE agent posture: {surface.IdeAgentPosture}",
            $"Path policy enforcement: {surface.PathPolicyEnforcementState}",
            $"Path policy summary: {surface.PathPolicyEnforcementSummary}",
            $"Plan handle: {surface.PlanHandle ?? "(none)"}",
            $"Planning card: {surface.PlanningCardId ?? "(none)"}",
            $"Formal planning state: {surface.FormalPlanningState ?? "(none)"}",
            $"Bound tasks: {surface.BoundTaskIds.Count}",
        };

        lines.AddRange(surface.BoundTaskIds.Select(taskId => $"- bound-task: {taskId}"));
        lines.Add($"Active leases: {surface.ActiveLeases.Count}");
        foreach (var lease in surface.ActiveLeases)
        {
            lines.Add($"- lease: {lease.LeaseId} | task={lease.TaskId} | workspace={lease.WorkspacePath} | status={lease.Status} | expires_at={lease.ExpiresAt:O}");
            lines.Add($"  - writable-paths: {(lease.AllowedWritablePaths.Count == 0 ? "(none)" : string.Join(", ", lease.AllowedWritablePaths))}");
            lines.Add($"  - operation-classes: {(lease.AllowedOperationClasses.Count == 0 ? "(none)" : string.Join(", ", lease.AllowedOperationClasses))}");
            lines.Add($"  - tools-or-adapters: {(lease.AllowedToolsOrAdapters.Count == 0 ? "(none)" : string.Join(", ", lease.AllowedToolsOrAdapters))}");
            lines.Add($"  - approval-posture: {lease.ApprovalPosture}");
            lines.Add($"  - cleanup-posture: {lease.CleanupPosture}");
        }

        lines.Add($"Recoverable residue posture: {surface.RecoverableResiduePosture}");
        if (surface.RecoverableResidueCount > 0)
        {
            lines.Add($"Recoverable residue count: {surface.RecoverableResidueCount}");
            lines.Add($"Highest recoverable residue severity: {surface.HighestRecoverableResidueSeverity}");
            lines.Add($"Recoverable residue blocks auto-run: {surface.RecoverableResidueBlocksAutoRun}");
            lines.Add($"Recoverable residue summary: {surface.RecoverableResidueSummary}");
            lines.Add($"Recoverable residue next action: {surface.RecoverableResidueRecommendedNextAction}");
            lines.Add($"Recoverable cleanup action id: {surface.RecoverableCleanupActionId}");
            lines.Add($"Recoverable cleanup action mode: {surface.RecoverableCleanupActionMode}");
            foreach (var residue in surface.RecoverableResidues)
            {
                lines.Add($"- residue: {residue.ResidueId} | class={residue.ResidueClass} | kind={residue.Kind} | severity={residue.Severity} | lease={residue.LeaseId ?? "(none)"} | task={residue.TaskId ?? "(none)"}");
                lines.Add($"  - summary: {residue.Summary}");
                lines.Add($"  - workspace-path: {residue.WorkspacePath ?? "(none)"}");
                lines.Add($"  - recoverable: {residue.Recoverable}");
                lines.Add($"  - blocks-auto-run: {residue.BlocksAutoRun}");
                lines.Add($"  - next-action: {residue.RecommendedNextAction}");
            }

            lines.Add("Available actions:");
            lines.AddRange(surface.AvailableActions.Count == 0
                ? ["- none"]
                : surface.AvailableActions.Select(action => $"- {action.ActionId} | {action.Kind} | mode={action.ActionMode} | {action.Command}"));
        }

        lines.Add($"Path policies: {surface.PathPolicies.Count}");
        foreach (var policy in surface.PathPolicies)
        {
            lines.Add($"- policy: {policy.PolicyClass} | effect={policy.EnforcementEffect} | summary={policy.Summary}");
            lines.AddRange(policy.Examples.Select(example => $"  - example: {example}"));
        }

        lines.Add($"Mode D hardening checks: {surface.ModeDHardeningChecks.Count}");
        foreach (var check in surface.ModeDHardeningChecks)
        {
            lines.Add($"- check: {check.CheckId} | state={check.State} | action={check.RequiredAction}");
            lines.Add($"  - summary: {check.Summary}");
        }

        lines.Add($"Recommended next action: {surface.RecommendedNextAction}");
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
