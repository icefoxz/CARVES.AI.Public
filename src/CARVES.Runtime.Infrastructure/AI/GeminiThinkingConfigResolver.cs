namespace Carves.Runtime.Infrastructure.AI;

internal static class GeminiThinkingConfigResolver
{
    public static Dictionary<string, object?>? Resolve(string? model, string? reasoningEffort)
    {
        if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(reasoningEffort))
        {
            return null;
        }

        var normalizedEffort = NormalizeReasoningEffort(reasoningEffort);
        if (normalizedEffort is null)
        {
            return null;
        }

        if (model.StartsWith("gemini-2.5", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, object?>
            {
                ["thinkingBudget"] = normalizedEffort switch
                {
                    "low" => 128,
                    "medium" => 1024,
                    "high" => -1,
                    _ => -1,
                },
            };
        }

        if (model.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, object?>
            {
                ["thinkingLevel"] = normalizedEffort,
            };
        }

        return null;
    }

    private static string? NormalizeReasoningEffort(string reasoningEffort)
    {
        return reasoningEffort.Trim().ToLowerInvariant() switch
        {
            "low" => "low",
            "medium" => "medium",
            "high" => "high",
            _ => null,
        };
    }
}
