using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Infrastructure.AI;

internal static class OpenAiCompatibleRequestFamily
{
    public const string ResponsesApi = "responses_api";
    public const string ChatCompletions = "chat_completions";

    public static string Resolve(AiProviderConfig config)
    {
        var explicitFamily = Normalize(config.RequestFamily);
        if (!string.IsNullOrWhiteSpace(explicitFamily))
        {
            return explicitFamily;
        }

        return ShouldDefaultToChatCompletions(config.BaseUrl)
            ? ChatCompletions
            : ResponsesApi;
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            ResponsesApi => ResponsesApi,
            ChatCompletions => ChatCompletions,
            _ => null,
        };
    }

    private static bool ShouldDefaultToChatCompletions(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Contains("groq.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Contains("deepseek.com", StringComparison.OrdinalIgnoreCase);
    }
}
