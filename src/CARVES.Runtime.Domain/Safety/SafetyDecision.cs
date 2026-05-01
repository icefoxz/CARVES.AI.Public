using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Safety;

public sealed class SafetyDecision
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string TaskId { get; init; } = string.Empty;

    public SafetyValidationMode ValidationMode { get; init; } = SafetyValidationMode.Execution;

    public SafetyOutcome Outcome { get; init; } = SafetyOutcome.Allow;

    public bool Allowed => Outcome != SafetyOutcome.Blocked;

    public bool NeedsReview => Outcome == SafetyOutcome.NeedsReview;

    public IReadOnlyList<SafetyValidatorResult> Results { get; init; } = Array.Empty<SafetyValidatorResult>();

    public IReadOnlyList<SafetyViolation> Issues => Results
        .SelectMany(result => result.Violations)
        .ToArray();

    public static SafetyDecision Allow(string taskId, SafetyValidationMode validationMode = SafetyValidationMode.Execution, params SafetyValidatorResult[] results)
    {
        return new SafetyDecision
        {
            TaskId = taskId,
            ValidationMode = validationMode,
            Outcome = SafetyOutcome.Allow,
            Results = results,
        };
    }

    public static SafetyDecision FromResults(string taskId, SafetyValidationMode validationMode, IReadOnlyList<SafetyValidatorResult> results)
    {
        var outcome = SafetyOutcome.Allow;
        if (results.Any(result => result.Outcome == SafetyOutcome.Blocked))
        {
            outcome = SafetyOutcome.Blocked;
        }
        else if (results.Any(result => result.Outcome == SafetyOutcome.NeedsReview))
        {
            outcome = SafetyOutcome.NeedsReview;
        }

        return new SafetyDecision
        {
            TaskId = taskId,
            ValidationMode = validationMode,
            Outcome = outcome,
            Results = results,
        };
    }
}
