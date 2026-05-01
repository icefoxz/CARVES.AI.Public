using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Safety;

public sealed class PatchSizeSafetyValidator : ISafetyValidator
{
    public SafetyValidatorResult Validate(SafetyContext context)
    {
        var violations = new List<SafetyViolation>();
        var outcome = SafetyOutcome.Allow;

        if (context.Report.Patch.FilesChanged > context.Rules.MaxFilesChanged ||
            context.Report.Patch.TotalLinesChanged > context.Rules.MaxLinesChanged)
        {
            outcome = SafetyOutcome.Blocked;
            violations.Add(new SafetyViolation("PATCH_TOO_LARGE", "Patch exceeds configured hard limits.", "error", nameof(PatchSizeSafetyValidator),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["files_changed"] = context.Report.Patch.FilesChanged.ToString(),
                    ["lines_changed"] = context.Report.Patch.TotalLinesChanged.ToString(),
                }));
        }
        else if (context.Report.Patch.FilesChanged >= context.Rules.ReviewFilesChangedThreshold ||
                 context.Report.Patch.TotalLinesChanged >= context.Rules.ReviewLinesChangedThreshold)
        {
            outcome = SafetyOutcome.NeedsReview;
            violations.Add(new SafetyViolation("PATCH_REVIEW_THRESHOLD", "Patch exceeds review threshold and needs human review.", "warning", nameof(PatchSizeSafetyValidator),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["files_changed"] = context.Report.Patch.FilesChanged.ToString(),
                    ["lines_changed"] = context.Report.Patch.TotalLinesChanged.ToString(),
                }));
        }

        return new SafetyValidatorResult
        {
            ValidatorName = nameof(PatchSizeSafetyValidator),
            Outcome = outcome,
            Summary = outcome switch
            {
                SafetyOutcome.Allow => "Patch size is within automatic execution limits.",
                SafetyOutcome.NeedsReview => "Patch size requires review.",
                _ => "Patch size exceeded hard limits.",
            },
            Violations = violations,
        };
    }
}
