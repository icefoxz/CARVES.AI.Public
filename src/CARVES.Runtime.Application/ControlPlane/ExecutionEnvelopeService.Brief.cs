using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ExecutionEnvelopeService
{
    private string BuildBrief(TaskNode task, ExecutionRun? run)
    {
        var objective = string.IsNullOrWhiteSpace(task.Description) ? task.Title : task.Description;
        var budget = budgetFactory.Create(task);
        var lines = new List<string>
        {
            $"# Task: {task.TaskId}",
            string.Empty,
            "## Objective",
            objective,
            string.Empty,
            "## Scope In",
        };

        AppendBulletList(lines, task.Scope);

        lines.Add(string.Empty);
        lines.Add("## Scope Out");
        AppendBulletList(lines, task.Constraints);

        lines.Add(string.Empty);
        lines.Add("## Acceptance");
        AppendBulletList(lines, task.Acceptance);

        if (task.AcceptanceContract is not null)
        {
            lines.Add(string.Empty);
            lines.Add("## Acceptance Contract");
            lines.AddRange(AcceptanceContractSummaryFormatter.BuildSummaryLines(task.AcceptanceContract));
        }

        lines.Add(string.Empty);
        lines.Add("## Execution Budget");
        lines.Add($"- size: {budget.Size.ToString().ToLowerInvariant()}");
        lines.Add($"- confidence: {budget.ConfidenceLevel.ToString().ToLowerInvariant()}");
        lines.Add($"- summary: {budget.Summary}");
        lines.Add($"- max retries: {budget.MaxRetries}");
        lines.Add($"- max failure density: {budget.MaxFailureDensity:0.00}");
        lines.Add($"- review boundary: {(budget.RequiresReviewBoundary ? "required" : "not required")}");
        lines.Add($"- change kinds: {string.Join(", ", budget.ChangeKinds.Select(kind => kind.ToString().ToLowerInvariant()))}");

        if (run is not null)
        {
            lines.Add(string.Empty);
            lines.Add("## Execution Run");
            lines.Add($"- run id: {run.RunId}");
            lines.Add($"- status: {run.Status.ToString().ToLowerInvariant()}");
            lines.Add($"- trigger reason: {run.TriggerReason.ToString().ToLowerInvariant()}");
            lines.Add($"- goal: {run.Goal}");
            lines.Add($"- current step: {Math.Min(run.Steps.Count, run.CurrentStepIndex + 1)}/{run.Steps.Count}");
            foreach (var step in run.Steps)
            {
                lines.Add($"- step: [{step.Status.ToString().ToLowerInvariant()}] {step.Kind.ToString().ToLowerInvariant()} - {step.Title}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("## Validation");
        AppendValidationList(lines, task.Validation.Commands, task.Validation.Checks, task.Validation.ExpectedEvidence);

        lines.Add(string.Empty);
        lines.Add("## Stop Conditions");
        AppendBulletList(lines, DefaultStopConditions);

        return string.Join('\n', lines) + "\n";
    }

    private static void AppendBulletList(List<string> lines, IEnumerable<string> values)
    {
        var items = values
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (items.Length == 0)
        {
            lines.Add("- (none)");
            return;
        }

        foreach (var item in items)
        {
            lines.Add($"- {item}");
        }
    }

    private static void AppendValidationList(
        List<string> lines,
        IReadOnlyList<IReadOnlyList<string>> commands,
        IReadOnlyList<string> checks,
        IReadOnlyList<string> expectedEvidence)
    {
        var wroteAnything = false;

        foreach (var command in commands.Where(command => command.Count > 0))
        {
            lines.Add($"- command: {string.Join(' ', command)}");
            wroteAnything = true;
        }

        foreach (var check in checks.Where(check => !string.IsNullOrWhiteSpace(check)))
        {
            lines.Add($"- check: {check}");
            wroteAnything = true;
        }

        foreach (var evidence in expectedEvidence.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            lines.Add($"- evidence: {evidence}");
            wroteAnything = true;
        }

        if (!wroteAnything)
        {
            lines.Add("- (none)");
        }
    }
}
