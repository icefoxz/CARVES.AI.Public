using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Infrastructure.AI;

public static class AiClientFactory
{
    public static IAiClient Create(AiProviderConfig config)
    {
        var nullClient = new NullAiClient();
        if (!config.Enabled || string.Equals(config.Provider, "null", StringComparison.OrdinalIgnoreCase))
        {
            return nullClient;
        }

        IAiClient primary = config.Provider.ToLowerInvariant() switch
        {
            "openai" => new OpenAiResponsesClient(
                new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(Math.Max(5, config.RequestTimeoutSeconds)),
                },
                config),
            "gemini" => new GeminiGenerateContentClient(
                new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(Math.Max(5, config.RequestTimeoutSeconds)),
                },
                config),
            "claude" => new UnsupportedAiClient("claude"),
            "codex" => new UnsupportedAiClient("codex"),
            "local" or "local-llm" => new UnsupportedAiClient("local"),
            _ => new UnsupportedAiClient(config.Provider),
        };

        return config.AllowFallbackToNull ? new FallbackAiClient(primary, nullClient) : primary;
    }
}
