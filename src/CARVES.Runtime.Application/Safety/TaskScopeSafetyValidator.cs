using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Safety;

public sealed class TaskScopeSafetyValidator : ISafetyValidator
{
    public SafetyValidatorResult Validate(SafetyContext context)
    {
        if (context.Task.Scope.Count == 0 || context.Report.Patch.Paths.Count == 0)
        {
            return new SafetyValidatorResult
            {
                ValidatorName = nameof(TaskScopeSafetyValidator),
                Outcome = SafetyOutcome.Allow,
                Summary = "No task scope drift detected.",
            };
        }

        var allowed = context.Task.Scope
            .Select(Normalize)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var violations = new List<SafetyViolation>();
        foreach (var path in context.Report.Patch.Paths.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (MatchesAllowed(path, allowed))
            {
                continue;
            }

            violations.Add(new SafetyViolation(
                "TASK_SCOPE_VIOLATION",
                $"Path '{path}' is outside the declared task scope.",
                "error",
                nameof(TaskScopeSafetyValidator)));
        }

        return new SafetyValidatorResult
        {
            ValidatorName = nameof(TaskScopeSafetyValidator),
            Outcome = violations.Count == 0 ? SafetyOutcome.Allow : SafetyOutcome.Blocked,
            Summary = violations.Count == 0
                ? "All changed paths remain inside the declared task scope."
                : "One or more changed paths exceed the declared task scope.",
            Violations = violations,
        };
    }

    private static bool MatchesAllowed(string path, IReadOnlyList<string> allowed)
    {
        foreach (var scope in allowed)
        {
            if (path.Equals(scope, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (scope.EndsWith("/", StringComparison.Ordinal))
            {
                if (path.StartsWith(scope, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            if (path.StartsWith(scope + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string path)
    {
        return path.Trim().Trim('`').Replace('\\', '/');
    }
}
