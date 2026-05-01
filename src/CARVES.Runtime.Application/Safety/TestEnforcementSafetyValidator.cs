using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Safety;

public sealed class TestEnforcementSafetyValidator : ISafetyValidator
{
    public SafetyValidatorResult Validate(SafetyContext context)
    {
        var touchesSource = context.Report.Patch.Paths.Any(path => path.StartsWith("src/", StringComparison.OrdinalIgnoreCase));
        if (!touchesSource || !context.Rules.RequireTestsForSourceChanges)
        {
            return new SafetyValidatorResult
            {
                ValidatorName = nameof(TestEnforcementSafetyValidator),
                Outcome = SafetyOutcome.Allow,
                Summary = "Source-test gate is not triggered.",
            };
        }

        var hasTestCommand = context.Report.Request.ValidationCommands.Any(command =>
            command.Any(part => part.Contains("tests", StringComparison.OrdinalIgnoreCase)) ||
            command.Any(part => string.Equals(part, "test", StringComparison.OrdinalIgnoreCase)) ||
            command.Any(part => string.Equals(part, "run", StringComparison.OrdinalIgnoreCase)));

        if (hasTestCommand)
        {
            return new SafetyValidatorResult
            {
                ValidatorName = nameof(TestEnforcementSafetyValidator),
                Outcome = SafetyOutcome.Allow,
                Summary = context.Report.DryRun
                    ? "Test gate is configured; execution is a dry-run."
                    : "Test gate is satisfied by explicit validation commands.",
            };
        }

        return new SafetyValidatorResult
        {
            ValidatorName = nameof(TestEnforcementSafetyValidator),
            Outcome = SafetyOutcome.Blocked,
            Summary = "Source changes require explicit tests, but no test command is configured.",
            Violations =
            [
                new SafetyViolation("TEST_GATE_MISSING", "Task touches src/ but does not declare a test or validation command.", "error", nameof(TestEnforcementSafetyValidator)),
            ],
        };
    }
}
