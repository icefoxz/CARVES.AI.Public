using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed record ContextBudgetPolicy(
    string PolicyId,
    string ProfileId,
    string Model,
    string EstimatorVersion,
    int ModelLimitTokens,
    int TargetTokens,
    int AdvisoryTokens,
    int HardSafetyTokens,
    int MaxContextTokens,
    int ReservedHeadroomTokens,
    int CoreBudgetTokens,
    int RelevantBudgetTokens);

public static class ContextBudgetPolicyResolver
{
    public const string EstimatorVersion = "chars_div_4.v1";

    public static ContextBudgetPolicy Resolve(ContextPackAudience audience, string? model, int? overrideMaxContextTokens = null)
    {
        var normalizedModel = string.IsNullOrWhiteSpace(model) ? "default" : model.Trim();
        var profile = ResolveProfile(audience);
        var modelLimitTokens = ResolveModelLimit(normalizedModel);
        var maxContextTokens = overrideMaxContextTokens is > 0
            ? Math.Min(overrideMaxContextTokens.Value, profile.HardSafetyTokens)
            : profile.AdvisoryTokens;
        var reservedHeadroomTokens = Math.Max(0, profile.HardSafetyTokens - maxContextTokens);
        var coreBudgetTokens = Math.Min(profile.CoreBudgetTokens, maxContextTokens);
        var relevantBudgetTokens = Math.Max(0, maxContextTokens - coreBudgetTokens);

        return new ContextBudgetPolicy(
            PolicyId: "context-pack-budget.v2",
            ProfileId: profile.ProfileId,
            Model: normalizedModel,
            EstimatorVersion: EstimatorVersion,
            ModelLimitTokens: modelLimitTokens,
            TargetTokens: profile.TargetTokens,
            AdvisoryTokens: profile.AdvisoryTokens,
            HardSafetyTokens: profile.HardSafetyTokens,
            MaxContextTokens: maxContextTokens,
            ReservedHeadroomTokens: reservedHeadroomTokens,
            CoreBudgetTokens: coreBudgetTokens,
            RelevantBudgetTokens: relevantBudgetTokens);
    }

    public static int EstimateTokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var length = value.Trim().Length;
        return Math.Max(1, (length + 3) / 4);
    }

    public static int EstimateTokens(IEnumerable<string> values)
    {
        return values.Sum(EstimateTokens);
    }

    private static int ResolveModelLimit(string model)
    {
        if (model.Contains("mini", StringComparison.OrdinalIgnoreCase)
            || model.Contains("haiku", StringComparison.OrdinalIgnoreCase)
            || model.Contains("flash", StringComparison.OrdinalIgnoreCase))
        {
            return 8_000;
        }

        return 16_000;
    }

    private static ContextBudgetProfile ResolveProfile(ContextPackAudience audience)
    {
        return audience switch
        {
            ContextPackAudience.Planner => new ContextBudgetProfile(
                ProfileId: "planner",
                TargetTokens: 940,
                AdvisoryTokens: 1_100,
                HardSafetyTokens: 1_500,
                CoreBudgetTokens: 550),
            _ => new ContextBudgetProfile(
                ProfileId: "worker",
                TargetTokens: 900,
                AdvisoryTokens: 1_000,
                HardSafetyTokens: 1_300,
                CoreBudgetTokens: 550),
        };
    }

    private sealed record ContextBudgetProfile(
        string ProfileId,
        int TargetTokens,
        int AdvisoryTokens,
        int HardSafetyTokens,
        int CoreBudgetTokens);
}
