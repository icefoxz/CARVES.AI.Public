using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Safety;

public sealed class TaskIntegritySafetyValidator : ISafetyValidator
{
    public SafetyValidatorResult Validate(SafetyContext context)
    {
        var violations = new List<SafetyViolation>();

        if (string.IsNullOrWhiteSpace(context.Task.TaskId))
        {
            violations.Add(new SafetyViolation("TASK_ID_MISSING", "Task truth is missing task_id.", "error", nameof(TaskIntegritySafetyValidator)));
        }

        if (string.IsNullOrWhiteSpace(context.Task.Title))
        {
            violations.Add(new SafetyViolation("TASK_TITLE_MISSING", "Task truth is missing title.", "error", nameof(TaskIntegritySafetyValidator)));
        }

        if (context.ValidationMode != SafetyValidationMode.Execution && context.Report.Validation.Commands.Count > 0)
        {
            violations.Add(new SafetyViolation(
                "NON_EXECUTION_VALIDATION_COMMANDS_SKIPPED",
                $"Task type '{context.Task.TaskType}' carries execution validation commands; non-execution safety mode will ignore them.",
                "warning",
                nameof(TaskIntegritySafetyValidator)));
        }

        return new SafetyValidatorResult
        {
            ValidatorName = nameof(TaskIntegritySafetyValidator),
            Outcome = violations.Any(violation => string.Equals(violation.Severity, "error", StringComparison.OrdinalIgnoreCase))
                ? SafetyOutcome.Blocked
                : SafetyOutcome.Allow,
            Summary = violations.Count == 0
                ? "Task truth and validation mode are internally consistent."
                : "Task integrity check found one or more issues.",
            Violations = violations,
        };
    }
}
