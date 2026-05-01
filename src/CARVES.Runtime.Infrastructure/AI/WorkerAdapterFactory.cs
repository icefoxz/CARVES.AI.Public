using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

public static class WorkerAdapterFactory
{
    public static WorkerAdapterRegistry Create(AiProviderConfig config, IAiClient activeClient, IHttpTransport? transport = null)
    {
        _ = activeClient;
        var workerConfig = config.ResolveForRole("worker");
        var provider = workerConfig.Provider.ToLowerInvariant();
        transport ??= new HttpTransportClient();
        var openAiConfig = BuildRegisteredProviderConfig(workerConfig, "openai");
        var claudeConfig = BuildRegisteredProviderConfig(workerConfig, "claude");
        var geminiConfig = BuildRegisteredProviderConfig(workerConfig, "gemini");
        var adapters = new List<IWorkerAdapter>
        {
            new OpenAiWorkerAdapter(openAiConfig, transport, BuildSelectionReason(provider, "openai")),
            new ClaudeWorkerAdapter(claudeConfig, transport, BuildSelectionReason(provider, "claude")),
            new GeminiWorkerAdapter(geminiConfig, transport, BuildSelectionReason(provider, "gemini")),
            new CodexCliWorkerAdapter(BuildSelectionReason(provider, "codex")),
            new CodexWorkerAdapter(BuildSelectionReason(provider, "codex")),
            new LocalLlmWorkerAdapter(BuildDisabledReason("local")),
            new NullWorkerAdapter("Worker execution will stay governed through the null adapter because the provider is disabled or explicitly null."),
        };

        IWorkerAdapter activeAdapter = provider switch
        {
            "openai" => adapters.OfType<OpenAiWorkerAdapter>().Single(),
            "claude" => adapters.OfType<ClaudeWorkerAdapter>().Single(),
            "codex" => adapters.OfType<CodexWorkerAdapter>().Single(),
            "gemini" => adapters.OfType<GeminiWorkerAdapter>().Single(),
            "local" or "local-llm" => adapters.OfType<LocalLlmWorkerAdapter>().Single(),
            _ => adapters.OfType<NullWorkerAdapter>().Single(),
        };

        return new WorkerAdapterRegistry(adapters, activeAdapter);
    }

    private static string BuildSelectionReason(string configuredProvider, string adapterProvider)
    {
        return string.Equals(configuredProvider, adapterProvider, StringComparison.OrdinalIgnoreCase)
            ? $"Configured AI provider '{configuredProvider}' selected the {adapterProvider} worker adapter."
            : $"The {adapterProvider} worker adapter is available but inactive because provider '{configuredProvider}' is configured.";
    }

    private static string BuildDisabledReason(string providerId)
    {
        return $"Provider '{providerId}' is defined as a platform-facing worker adapter, but this runtime still uses a disabled placeholder for it.";
    }

    private static AiProviderConfig BuildRegisteredProviderConfig(AiProviderConfig defaultConfig, string providerId)
    {
        if (string.Equals(defaultConfig.Provider, providerId, StringComparison.OrdinalIgnoreCase))
        {
            return defaultConfig;
        }

        return providerId.ToLowerInvariant() switch
        {
            "openai" => new AiProviderConfig(
                Provider: "openai",
                Enabled: ShouldEnableInactiveProvider(defaultConfig),
                Model: "gpt-5-mini",
                BaseUrl: "https://api.openai.com/v1",
                ApiKeyEnvironmentVariable: "OPENAI_API_KEY",
                AllowFallbackToNull: defaultConfig.AllowFallbackToNull,
                RequestTimeoutSeconds: defaultConfig.RequestTimeoutSeconds,
                MaxOutputTokens: defaultConfig.MaxOutputTokens,
                ReasoningEffort: defaultConfig.ReasoningEffort,
                RequestFamily: null,
                Organization: defaultConfig.Organization,
                Project: defaultConfig.Project),
            "claude" => new AiProviderConfig(
                Provider: "claude",
                Enabled: ShouldEnableInactiveProvider(defaultConfig),
                Model: "claude-sonnet-4-5",
                BaseUrl: "https://api.anthropic.com/v1",
                ApiKeyEnvironmentVariable: "ANTHROPIC_API_KEY",
                AllowFallbackToNull: defaultConfig.AllowFallbackToNull,
                RequestTimeoutSeconds: defaultConfig.RequestTimeoutSeconds,
                MaxOutputTokens: defaultConfig.MaxOutputTokens,
                ReasoningEffort: defaultConfig.ReasoningEffort,
                RequestFamily: "messages_api",
                Organization: null,
                Project: null),
            "gemini" => new AiProviderConfig(
                Provider: "gemini",
                Enabled: ShouldEnableInactiveProvider(defaultConfig),
                Model: "gemini-2.5-pro",
                BaseUrl: "https://generativelanguage.googleapis.com/v1beta",
                ApiKeyEnvironmentVariable: "GEMINI_API_KEY",
                AllowFallbackToNull: defaultConfig.AllowFallbackToNull,
                RequestTimeoutSeconds: defaultConfig.RequestTimeoutSeconds,
                MaxOutputTokens: defaultConfig.MaxOutputTokens,
                ReasoningEffort: defaultConfig.ReasoningEffort,
                RequestFamily: "generate_content",
                Organization: null,
                Project: null),
            _ => defaultConfig,
        };
    }

    private static bool ShouldEnableInactiveProvider(AiProviderConfig defaultConfig)
    {
        return defaultConfig.Enabled
               && !string.Equals(defaultConfig.Provider, "null", StringComparison.OrdinalIgnoreCase);
    }
}
