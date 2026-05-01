using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Application.Safety;

public sealed class SafetyService
{
    private static readonly string[] RequiredDerivedViewPaths =
    [
        ".ai/CURRENT_TASK.md",
        ".ai/STATE.md",
        ".ai/TASK_QUEUE.md",
    ];

    private readonly IReadOnlyList<ISafetyValidator> validators;
    private readonly SafetyTaskClassifier taskClassifier;

    public SafetyService(IReadOnlyList<ISafetyValidator> validators, SafetyTaskClassifier? taskClassifier = null)
    {
        this.validators = validators;
        this.taskClassifier = taskClassifier ?? new SafetyTaskClassifier();
    }

    public IReadOnlyList<SafetyViolation> DescribeBaseline(SafetyRules rules)
    {
        var violations = new List<SafetyViolation>();

        if (rules.RestrictedPaths.Count == 0)
        {
            violations.Add(new SafetyViolation("RESTRICTED_PATHS_EMPTY", "Restricted paths are not configured.", "error", nameof(SafetyService)));
        }

        if (!rules.WorkerWritablePaths.Any())
        {
            violations.Add(new SafetyViolation("WRITABLE_PATHS_EMPTY", "Worker writable paths are not configured.", "error", nameof(SafetyService)));
        }

        if (!rules.ManagedControlPlanePaths.Any())
        {
            violations.Add(new SafetyViolation("CONTROL_PLANE_PATHS_EMPTY", "Managed control-plane paths are not configured.", "error", nameof(SafetyService)));
        }

        if (rules.MaxRetryCount <= 0)
        {
            violations.Add(new SafetyViolation("RETRY_LIMIT_INVALID", "Max retry count must be greater than zero.", "error", nameof(SafetyService)));
        }

        if (rules.ReviewFilesChangedThreshold > rules.MaxFilesChanged || rules.ReviewLinesChangedThreshold > rules.MaxLinesChanged)
        {
            violations.Add(new SafetyViolation("REVIEW_THRESHOLD_INERT", "Review thresholds must not exceed hard patch limits.", "warning", nameof(SafetyService)));
        }

        foreach (var requiredPath in RequiredDerivedViewPaths)
        {
            if (!rules.ManagedControlPlanePaths.Contains(requiredPath, StringComparer.OrdinalIgnoreCase))
            {
                violations.Add(new SafetyViolation("DERIVED_VIEW_PATH_MISSING", $"Managed control-plane paths must include '{requiredPath}'.", "error", nameof(SafetyService)));
            }
        }

        if (rules.RequireTestsForSourceChanges && !rules.WorkerWritablePaths.Any(path => path.StartsWith("tests", StringComparison.OrdinalIgnoreCase)))
        {
            violations.Add(new SafetyViolation("TEST_PATH_MISSING", "Tests are required for source changes but no tests path is writable.", "error", nameof(SafetyService)));
        }

        return violations;
    }

    public SafetyDecision Evaluate(SafetyContext context)
    {
        var mode = context.ValidationMode;
        var results = validators
            .Where(validator => taskClassifier.ShouldRunValidator(mode, validator))
            .Select(validator => validator.Validate(context))
            .ToArray();

        if (results.Length == 0)
        {
            return SafetyDecision.Allow(context.Task.TaskId, mode);
        }

        return SafetyDecision.FromResults(context.Task.TaskId, mode, results);
    }
}
