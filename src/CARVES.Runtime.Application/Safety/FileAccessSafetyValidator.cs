using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Safety;

public sealed class FileAccessSafetyValidator : ISafetyValidator
{
    public SafetyValidatorResult Validate(SafetyContext context)
    {
        var violations = new List<SafetyViolation>();
        foreach (var path in context.Report.Patch.Paths)
        {
            if (MatchesAny(path, context.Rules.ProtectedPaths))
            {
                violations.Add(new SafetyViolation("PROTECTED_PATH", $"Path '{path}' is protected.", "error", nameof(FileAccessSafetyValidator)));
                continue;
            }

            if (MatchesAny(path, context.Rules.MemoryWritePaths))
            {
                if (!context.Task.Capabilities.Contains(context.Rules.MemoryWriteCapability, StringComparer.OrdinalIgnoreCase))
                {
                    violations.Add(new SafetyViolation("MEMORY_WRITE_FORBIDDEN", $"Path '{path}' requires capability '{context.Rules.MemoryWriteCapability}'.", "error", nameof(FileAccessSafetyValidator)));
                }

                continue;
            }

            if (MatchesAny(path, context.Rules.ManagedControlPlanePaths))
            {
                continue;
            }

            if (MatchesAny(path, context.Rules.RestrictedPaths))
            {
                violations.Add(new SafetyViolation("RESTRICTED_PATH", $"Path '{path}' is restricted.", "error", nameof(FileAccessSafetyValidator)));
                continue;
            }

            if (!MatchesAny(path, context.Rules.WorkerWritablePaths))
            {
                violations.Add(new SafetyViolation("UNWRITABLE_PATH", $"Path '{path}' is outside writable roots.", "error", nameof(FileAccessSafetyValidator)));
            }
        }

        var outcome = violations.Count == 0 ? SafetyOutcome.Allow : SafetyOutcome.Blocked;
        return new SafetyValidatorResult
        {
            ValidatorName = nameof(FileAccessSafetyValidator),
            Outcome = outcome,
            Summary = violations.Count == 0 ? "All touched paths are allowed." : "One or more touched paths violate write policy.",
            Violations = violations,
        };
    }

    private static bool MatchesAny(string path, IReadOnlyList<string> prefixes)
    {
        return prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
