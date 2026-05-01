namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentWorkingModes(RuntimeAgentWorkingModesSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent working modes",
            $"Working modes document: {surface.WorkingModesDocumentPath}",
            $"Collaboration plane document: {surface.CollaborationPlaneDocumentPath}",
            $"CLI-first document: {surface.CliFirstDocumentPath}",
            $"Implementation plan: {surface.ImplementationPlanPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Compatibility baseline: {surface.CompatibilityBaseline}",
            $"First general hard mode: {surface.FirstGeneralHardMode}",
            $"Primary IDE strong mode: {surface.PrimaryIdeStrongMode}",
            $"Current mode: {surface.CurrentMode}",
            $"Current mode summary: {surface.CurrentModeSummary}",
            $"Strongest runtime supported mode: {surface.StrongestRuntimeSupportedMode}",
            $"External agent recommended mode: {surface.ExternalAgentRecommendedMode}",
            $"External agent recommendation posture: {surface.ExternalAgentRecommendationPosture}",
            $"External agent recommendation summary: {surface.ExternalAgentRecommendationSummary}",
            $"External agent recommended action: {surface.ExternalAgentRecommendedAction}",
            $"External agent constraint tier: {surface.ExternalAgentConstraintTier}",
            $"External agent constraint summary: {surface.ExternalAgentConstraintSummary}",
            $"External agent stronger-mode blockers: {surface.ExternalAgentStrongerModeBlockerCount}",
            $"External agent first stronger-mode blocker: {surface.ExternalAgentFirstStrongerModeBlockerId ?? "(none)"}",
            $"External agent first stronger-mode blocker target: {surface.ExternalAgentFirstStrongerModeBlockerTargetMode ?? "(none)"}",
            $"External agent first stronger-mode blocker action: {surface.ExternalAgentFirstStrongerModeBlockerRequiredAction ?? "(none)"}",
            $"External agent first stronger-mode blocker constraint: {surface.ExternalAgentFirstStrongerModeBlockerConstraintClass ?? "(none)"}",
            $"External agent first stronger-mode blocker enforcement: {surface.ExternalAgentFirstStrongerModeBlockerEnforcementLevel ?? "(none)"}",
            $"Planning coupling posture: {surface.PlanningCouplingPosture}",
            $"Planning coupling summary: {surface.PlanningCouplingSummary}",
            $"Plan handle: {surface.PlanHandle ?? "(none)"}",
            $"Planning card: {surface.PlanningCardId ?? "(none)"}",
            $"Managed workspace posture: {surface.ManagedWorkspacePosture}",
            $"Path policy enforcement: {surface.PathPolicyEnforcementState}",
            $"Collaboration plane summary: {surface.CollaborationPlaneSummary}",
            $"Mode E operational activation: {surface.ModeEOperationalActivationState}",
            $"Mode E activation task: {surface.ModeEActivationTaskId ?? "(none)"}",
            $"Mode E activation summary: {surface.ModeEOperationalActivationSummary}",
            $"Mode E result return channel: {surface.ModeEActivationResultReturnChannel ?? "(none)"}",
            $"Mode E activation commands: {surface.ModeEActivationCommands.Count}",
            $"Mode E activation next action: {surface.ModeEActivationRecommendedNextAction}",
            $"Mode E activation blocking checks: {surface.ModeEActivationBlockingCheckCount}",
            $"Mode E activation first blocker: {surface.ModeEActivationFirstBlockingCheckId ?? "(none)"}",
            $"Mode E activation first blocker action: {surface.ModeEActivationFirstBlockingCheckRequiredAction ?? "(none)"}",
            $"Mode E activation first blocker summary: {surface.ModeEActivationFirstBlockingCheckSummary ?? "(none)"}",
            $"Mode E activation playbook summary: {surface.ModeEActivationPlaybookSummary}",
            $"Mode E activation playbook steps: {surface.ModeEActivationPlaybookStepCount}",
            $"Mode E activation first playbook command: {surface.ModeEActivationFirstPlaybookStepCommand ?? "(none)"}",
            $"Mode E activation first playbook summary: {surface.ModeEActivationFirstPlaybookStepSummary ?? "(none)"}",
        };

        lines.AddRange(surface.ModeEActivationCommands.Select(command => $"- mode-e command: {command}"));
        foreach (var step in surface.ModeEActivationPlaybookSteps)
        {
            lines.Add($"- mode-e playbook step {step.Order}: {step.StepId} | command={step.Command} | applies={step.AppliesWhen}");
            lines.Add($"  - summary: {step.Summary}");
        }

        lines.Add($"Mode E activation checks: {surface.ModeEActivationChecks.Count}");
        foreach (var check in surface.ModeEActivationChecks)
        {
            lines.Add($"- mode-e check: {check.CheckId} | state={check.State} | required_action={check.RequiredAction}");
            lines.Add($"  - summary: {check.Summary}");
        }

        lines.Add($"External agent stronger-mode blocker details: {surface.ExternalAgentStrongerModeBlockers.Count}");
        foreach (var blocker in surface.ExternalAgentStrongerModeBlockers)
        {
            lines.Add($"- external-agent blocker: {blocker.BlockerId} | target={blocker.TargetModeId} | constraint={blocker.ConstraintClass} | enforcement={blocker.EnforcementLevel} | command={blocker.RequiredCommand}");
            lines.Add($"  - summary: {blocker.Summary}");
            lines.Add($"  - required_action: {blocker.RequiredAction}");
        }

        lines.Add($"Constraint classes: {surface.ConstraintClasses.Count}");
        foreach (var constraintClass in surface.ConstraintClasses)
        {
            lines.Add($"- constraint-class: {constraintClass.ClassId} | enforcement={constraintClass.EnforcementLevel} | modes={string.Join(", ", constraintClass.AppliesToModes)}");
            lines.Add($"  - summary: {constraintClass.Summary}");
        }

        lines.Add($"Supported modes: {surface.SupportedModes.Count}");

        foreach (var mode in surface.SupportedModes)
        {
            lines.Add($"- mode: {mode.ModeId} | display={mode.DisplayName} | constraint={mode.ConstraintStrength} | planning={mode.PlanningCoupling} | portability={mode.Portability} | status={mode.RuntimeStatus}");
            lines.Add($"  - summary: {mode.Summary}");
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
