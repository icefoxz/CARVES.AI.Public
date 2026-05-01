using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Safety;

public sealed class LoopDetectionSafetyValidator : ISafetyValidator
{
    public SafetyValidatorResult Validate(SafetyContext context)
    {
        if (context.Task.RetryCount == 0)
        {
            return new SafetyValidatorResult
            {
                ValidatorName = nameof(LoopDetectionSafetyValidator),
                Outcome = SafetyOutcome.Allow,
                Summary = "No retry-loop signal is present.",
            };
        }

        if (context.Report.Validation.Passed)
        {
            return new SafetyValidatorResult
            {
                ValidatorName = nameof(LoopDetectionSafetyValidator),
                Outcome = SafetyOutcome.Allow,
                Summary = "Task is being retried, but the current execution passed validation.",
            };
        }

        var nextAttempt = context.Task.RetryCount + 1;
        if (nextAttempt >= context.Rules.MaxRetryCount)
        {
            return new SafetyValidatorResult
            {
                ValidatorName = nameof(LoopDetectionSafetyValidator),
                Outcome = SafetyOutcome.Blocked,
                Summary = "Repeated validation failures reached the retry ceiling.",
                Violations =
                [
                    new SafetyViolation(
                        "REPEATED_FAILURE_LOOP",
                        $"Task retry count {context.Task.RetryCount} would reach the configured limit {context.Rules.MaxRetryCount} after another failed attempt.",
                        "error",
                        nameof(LoopDetectionSafetyValidator)),
                ],
            };
        }

        return new SafetyValidatorResult
        {
            ValidatorName = nameof(LoopDetectionSafetyValidator),
            Outcome = SafetyOutcome.NeedsReview,
            Summary = "A retried task failed validation again and requires review before more automated attempts.",
            Violations =
            [
                new SafetyViolation(
                    "RETRY_REVIEW_REQUIRED",
                    $"Task retry count {context.Task.RetryCount} indicates a repeated failure pattern.",
                    "warning",
                    nameof(LoopDetectionSafetyValidator)),
            ],
        };
    }
}
