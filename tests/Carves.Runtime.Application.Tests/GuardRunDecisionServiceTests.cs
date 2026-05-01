using Carves.Runtime.Application.Guard;

namespace Carves.Runtime.Application.Tests;

public sealed class GuardRunDecisionServiceTests
{
    [Fact]
    public void Compose_AllowsAcceptedRuntimeAndCleanDiscipline()
    {
        var result = new GuardRunDecisionService().Compose(
            Execution(accepted: true, outcome: "completed", changedFiles: ["src/todo.ts", "tests/todo.test.ts"]),
            Discipline(GuardDecisionOutcome.Allow));

        Assert.Equal(GuardDecisionOutcome.Allow, result.Decision.Outcome);
        Assert.True(result.Decision.RequiresRuntimeTaskTruth);
        Assert.Empty(result.Decision.Violations);
    }

    [Fact]
    public void Compose_BlocksBoundaryDeniedRuntimeExecution()
    {
        var result = new GuardRunDecisionService().Compose(
            Execution(
                accepted: false,
                outcome: "blocked",
                summary: "Worker execution boundary denied the command prefix.",
                failureKind: "PolicyDenied"),
            Discipline(GuardDecisionOutcome.Allow));

        Assert.Equal(GuardDecisionOutcome.Block, result.Decision.Outcome);
        Assert.Contains(result.Decision.Violations, violation => violation.RuleId == "runtime.execution_boundary_denied");
    }

    [Fact]
    public void Compose_BlocksSafetyBlockedRuntimeExecution()
    {
        var result = new GuardRunDecisionService().Compose(
            Execution(
                accepted: true,
                outcome: "blocked",
                summary: "Safety evaluation blocked the worker result."),
            Discipline(GuardDecisionOutcome.Allow));

        Assert.Equal(GuardDecisionOutcome.Block, result.Decision.Outcome);
        Assert.Contains(result.Decision.Violations, violation => violation.RuleId == "runtime.safety_blocked");
    }

    [Fact]
    public void Compose_BlocksPacketEnforcementRuntimeExecution()
    {
        var result = new GuardRunDecisionService().Compose(
            Execution(
                accepted: true,
                outcome: "blocked",
                summary: "Packet enforcement blocked a changed file."),
            Discipline(GuardDecisionOutcome.Allow));

        Assert.Equal(GuardDecisionOutcome.Block, result.Decision.Outcome);
        Assert.Contains(result.Decision.Violations, violation => violation.RuleId == "runtime.packet_enforcement_blocked");
    }

    [Fact]
    public void Compose_BlocksChangeDisciplineViolation()
    {
        var discipline = Discipline(
            GuardDecisionOutcome.Block,
            violations:
            [
                new GuardViolation(
                    "path.protected_prefix",
                    GuardSeverity.Block,
                    "Protected path changed.",
                    ".ai/tasks/generated.json",
                    "path=.ai/tasks/generated.json",
                    "guard-rule:guard-run-test:path.protected_prefix:0"),
            ]);

        var result = new GuardRunDecisionService().Compose(
            Execution(accepted: true, outcome: "completed", changedFiles: [".ai/tasks/generated.json"]),
            discipline);

        Assert.Equal(GuardDecisionOutcome.Block, result.Decision.Outcome);
        Assert.Contains(result.Decision.Violations, violation => violation.RuleId == "path.protected_prefix");
        Assert.Contains(result.Decision.EvidenceRefs, reference => reference.Contains("path.protected_prefix", StringComparison.Ordinal));
    }

    [Fact]
    public void Compose_ReviewsRuntimeExecutionWhenReviewBoundaryReached()
    {
        var result = new GuardRunDecisionService().Compose(
            Execution(
                accepted: true,
                outcome: "review",
                summary: "Runtime execution requires human review.",
                changedFiles: ["src/todo.ts"],
                taskStatus: "Review"),
            Discipline(GuardDecisionOutcome.Allow));

        Assert.Equal(GuardDecisionOutcome.Review, result.Decision.Outcome);
        Assert.Contains(result.Decision.Warnings, warning => warning.RuleId == "runtime.execution_review_required");
        Assert.Contains(result.Decision.EvidenceRefs, reference => reference.Contains("runtime.execution_review_required", StringComparison.Ordinal));
    }

    private static GuardRuntimeExecutionResult Execution(
        bool accepted,
        string outcome,
        IReadOnlyList<string>? changedFiles = null,
        string summary = "Task run completed.",
        string? failureKind = null,
        string? taskStatus = null)
    {
        return new GuardRuntimeExecutionResult(
            "T-GUARD-RUN-001",
            accepted,
            outcome,
            TaskStatus: taskStatus ?? (accepted ? "Completed" : "Blocked"),
            summary,
            failureKind,
            RunId: "run-1",
            ExecutionRunId: "execution-run-1",
            ChangedFiles: changedFiles ?? Array.Empty<string>(),
            NextAction: "review task");
    }

    private static GuardDecision Discipline(
        GuardDecisionOutcome outcome,
        IReadOnlyList<GuardViolation>? violations = null,
        IReadOnlyList<GuardWarning>? warnings = null)
    {
        return new GuardDecision(
            "guard-run-test",
            outcome,
            "alpha-test",
            "Discipline decision.",
            violations ?? Array.Empty<GuardViolation>(),
            warnings ?? Array.Empty<GuardWarning>(),
            ["guard-run:guard-run-test", "guard-policy:alpha-test"],
            RequiresRuntimeTaskTruth: false);
    }
}
