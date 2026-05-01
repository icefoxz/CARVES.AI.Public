using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Safety;

public sealed class ManagedControlPlaneSafetyValidator : ISafetyValidator
{
    private static readonly string[] DerivedMarkdownViews =
    [
        ".ai/CURRENT_TASK.md",
        ".ai/STATE.md",
        ".ai/TASK_QUEUE.md",
    ];

    public SafetyValidatorResult Validate(SafetyContext context)
    {
        var touchedManagedPaths = context.Report.Patch.Paths
            .Where(path => MatchesAny(path, context.Rules.ManagedControlPlanePaths))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (touchedManagedPaths.Length == 0)
        {
            return new SafetyValidatorResult
            {
                ValidatorName = nameof(ManagedControlPlaneSafetyValidator),
                Outcome = SafetyOutcome.Allow,
                Summary = "Patch does not modify managed control-plane state.",
            };
        }

        var violations = new List<SafetyViolation>();
        var outcome = SafetyOutcome.NeedsReview;

        foreach (var path in touchedManagedPaths)
        {
            if (MatchesAny(path, DerivedMarkdownViews))
            {
                outcome = SafetyOutcome.Blocked;
                violations.Add(new SafetyViolation(
                    "DERIVED_VIEW_WRITE_FORBIDDEN",
                    $"Path '{path}' is a derived markdown view and must be written through markdown sync.",
                    "error",
                    nameof(ManagedControlPlaneSafetyValidator)));
                continue;
            }

            violations.Add(new SafetyViolation(
                "CONTROL_PLANE_WRITE_REVIEW_REQUIRED",
                $"Path '{path}' is machine-managed control-plane state and requires explicit review.",
                "warning",
                nameof(ManagedControlPlaneSafetyValidator)));
        }

        return new SafetyValidatorResult
        {
            ValidatorName = nameof(ManagedControlPlaneSafetyValidator),
            Outcome = outcome,
            Summary = outcome == SafetyOutcome.Blocked
                ? "Patch attempts to write derived markdown views directly."
                : "Patch touches machine-managed control-plane state.",
            Violations = violations,
        };
    }

    private static bool MatchesAny(string path, IReadOnlyList<string> prefixes)
    {
        return prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
