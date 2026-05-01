using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Safety;

public sealed class ArchitectureRulesSafetyValidator : ISafetyValidator
{
    public SafetyValidatorResult Validate(SafetyContext context)
    {
        var touchedModules = context.Report.Patch.Paths
            .Select(TryResolveModule)
            .Where(module => !string.IsNullOrWhiteSpace(module))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var violations = new List<SafetyViolation>();
        foreach (var module in touchedModules)
        {
            if (!context.ModuleDependencyMap.Entries.ContainsKey(module!) && context.Rules.ReviewRequiredForNewModule)
            {
                violations.Add(new SafetyViolation("NEW_MODULE_REVIEW_REQUIRED", $"Module '{module}' is not registered in module_dependencies.json.", "warning", nameof(ArchitectureRulesSafetyValidator)));
            }
        }

        return new SafetyValidatorResult
        {
            ValidatorName = nameof(ArchitectureRulesSafetyValidator),
            Outcome = violations.Count == 0 ? SafetyOutcome.Allow : SafetyOutcome.NeedsReview,
            Summary = violations.Count == 0 ? "Touched modules are known to the dependency map." : "One or more touched modules require architectural review.",
            Violations = violations,
        };
    }

    private static string? TryResolveModule(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("src/CARVES.Runtime.", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1].Replace("CARVES", "Carves", StringComparison.Ordinal) : null;
    }
}
