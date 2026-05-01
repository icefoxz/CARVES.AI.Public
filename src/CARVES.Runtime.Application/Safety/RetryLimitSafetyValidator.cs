using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Safety;

public sealed class RetryLimitSafetyValidator : ISafetyValidator
{
    public SafetyValidatorResult Validate(SafetyContext context)
    {
        if (context.Report.Validation.Passed)
        {
            return new SafetyValidatorResult
            {
                ValidatorName = nameof(RetryLimitSafetyValidator),
                Outcome = SafetyOutcome.Allow,
                Summary = "Retry count reached the configured limit, but the current execution passed validation and may be written back.",
            };
        }

        if (context.Task.RetryCount < context.Rules.MaxRetryCount)
        {
            return new SafetyValidatorResult
            {
                ValidatorName = nameof(RetryLimitSafetyValidator),
                Outcome = SafetyOutcome.Allow,
                Summary = "Retry count remains within configured limits.",
            };
        }

        return new SafetyValidatorResult
        {
            ValidatorName = nameof(RetryLimitSafetyValidator),
            Outcome = SafetyOutcome.Blocked,
            Summary = "Retry count exceeded the configured limit.",
            Violations =
            [
                new SafetyViolation("RETRY_LIMIT_REACHED", $"Task retry count {context.Task.RetryCount} reached the limit {context.Rules.MaxRetryCount}.", "error", nameof(RetryLimitSafetyValidator)),
            ],
        };
    }
}
