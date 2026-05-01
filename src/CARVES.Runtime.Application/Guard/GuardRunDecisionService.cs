namespace Carves.Runtime.Application.Guard;

public sealed class GuardRunDecisionService
{
    public GuardRunResult Compose(GuardRuntimeExecutionResult execution, GuardDecision disciplineDecision)
    {
        var runtimeViolations = BuildRuntimeViolations(disciplineDecision.RunId, execution);
        var runtimeWarnings = BuildRuntimeWarnings(disciplineDecision.RunId, execution);
        var violations = runtimeViolations.Concat(disciplineDecision.Violations).ToArray();
        var warnings = runtimeWarnings.Concat(disciplineDecision.Warnings).ToArray();
        var outcome = violations.Length > 0
            ? GuardDecisionOutcome.Block
            : warnings.Length > 0
                ? GuardDecisionOutcome.Review
                : GuardDecisionOutcome.Allow;
        var evidenceRefs = new List<string>
        {
            $"guard-run:{disciplineDecision.RunId}",
            $"guard-policy:{disciplineDecision.PolicyId}",
        };
        evidenceRefs.AddRange(violations.Select(violation => violation.EvidenceRef));
        evidenceRefs.AddRange(warnings.Select(warning => warning.EvidenceRef));

        var decision = new GuardDecision(
            disciplineDecision.RunId,
            outcome,
            disciplineDecision.PolicyId,
            BuildSummary(outcome, execution),
            violations,
            warnings,
            evidenceRefs.Distinct(StringComparer.Ordinal).ToArray(),
            RequiresRuntimeTaskTruth: true);
        return new GuardRunResult(decision, execution, disciplineDecision);
    }

    private static IReadOnlyList<GuardViolation> BuildRuntimeViolations(string runId, GuardRuntimeExecutionResult execution)
    {
        if (RuntimeExecutionPassed(execution))
        {
            return Array.Empty<GuardViolation>();
        }

        var ruleId = ResolveRuntimeBlockRuleId(execution);
        return
        [
            new GuardViolation(
                ruleId,
                GuardSeverity.Block,
                ResolveRuntimeBlockMessage(ruleId),
                FilePath: null,
                BuildRuntimeEvidence(execution),
                $"guard-rule:{runId}:{ruleId}:0")
        ];
    }

    private static IReadOnlyList<GuardWarning> BuildRuntimeWarnings(string runId, GuardRuntimeExecutionResult execution)
    {
        if (!execution.Accepted || IsBlockedOutcome(execution.Outcome))
        {
            return Array.Empty<GuardWarning>();
        }

        if (string.Equals(execution.Outcome, "review", StringComparison.OrdinalIgnoreCase)
            || string.Equals(execution.TaskStatus, "Review", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new GuardWarning(
                    "runtime.execution_review_required",
                    "Task-aware execution reached the Runtime review boundary.",
                    FilePath: null,
                    BuildRuntimeEvidence(execution),
                    $"guard-rule:{runId}:runtime.execution_review_required:0")
            ];
        }

        return Array.Empty<GuardWarning>();
    }

    private static bool RuntimeExecutionPassed(GuardRuntimeExecutionResult execution)
    {
        if (!execution.Accepted || IsBlockedOutcome(execution.Outcome))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(execution.FailureKind)
            && !string.Equals(execution.FailureKind, "None", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsBlockedOutcome(string outcome)
    {
        return outcome.Contains("block", StringComparison.OrdinalIgnoreCase)
               || outcome.Contains("denied", StringComparison.OrdinalIgnoreCase)
               || outcome.Contains("failed", StringComparison.OrdinalIgnoreCase)
               || outcome.Contains("rejected", StringComparison.OrdinalIgnoreCase)
               || outcome.Contains("approval_wait", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveRuntimeBlockRuleId(GuardRuntimeExecutionResult execution)
    {
        var combined = $"{execution.Outcome} {execution.FailureKind} {execution.Summary}";
        if (combined.Contains("packet", StringComparison.OrdinalIgnoreCase))
        {
            return "runtime.packet_enforcement_blocked";
        }

        if (combined.Contains("safety", StringComparison.OrdinalIgnoreCase))
        {
            return "runtime.safety_blocked";
        }

        if (combined.Contains("boundary", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("policydenied", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("policy denied", StringComparison.OrdinalIgnoreCase))
        {
            return "runtime.execution_boundary_denied";
        }

        return "runtime.execution_blocked";
    }

    private static string ResolveRuntimeBlockMessage(string ruleId)
    {
        return ruleId switch
        {
            "runtime.execution_boundary_denied" => "Runtime execution boundary denied the task run.",
            "runtime.safety_blocked" => "Runtime safety evaluation blocked the task run.",
            "runtime.packet_enforcement_blocked" => "Runtime packet enforcement blocked the task run.",
            _ => "Runtime task-aware execution did not pass.",
        };
    }

    private static string BuildRuntimeEvidence(GuardRuntimeExecutionResult execution)
    {
        var failureKind = string.IsNullOrWhiteSpace(execution.FailureKind) ? "none" : execution.FailureKind;
        return $"outcome={execution.Outcome}; task_status={execution.TaskStatus}; failure_kind={failureKind}; summary={execution.Summary}";
    }

    private static string BuildSummary(GuardDecisionOutcome outcome, GuardRuntimeExecutionResult execution)
    {
        return outcome switch
        {
            GuardDecisionOutcome.Allow => $"Task-aware Guard run allowed {execution.TaskId}.",
            GuardDecisionOutcome.Review => $"Task-aware Guard run requires review for {execution.TaskId}.",
            GuardDecisionOutcome.Block => $"Task-aware Guard run blocked {execution.TaskId}.",
            _ => $"Task-aware Guard run evaluated {execution.TaskId}.",
        };
    }
}
