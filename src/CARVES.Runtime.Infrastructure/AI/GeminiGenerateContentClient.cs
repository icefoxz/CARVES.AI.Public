using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

public sealed class GeminiGenerateContentClient : IAiClient
{
    private const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient httpClient;
    private readonly AiProviderConfig config;
    private readonly string? apiKey;

    public GeminiGenerateContentClient(HttpClient httpClient, AiProviderConfig config)
    {
        this.httpClient = httpClient;
        this.config = config;
        apiKey = Environment.GetEnvironmentVariable(config.ApiKeyEnvironmentVariable);
    }

    public string ClientName => nameof(GeminiGenerateContentClient);

    public bool IsConfigured => config.Enabled && !string.IsNullOrWhiteSpace(apiKey);

    public AiExecutionRecord Execute(AiExecutionRequest request)
    {
        if (!config.Enabled)
        {
            throw new InvalidOperationException("Gemini provider is disabled in ai_provider.json.");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"Environment variable '{config.ApiKeyEnvironmentVariable}' is not set.");
        }

        var model = request.ModelOverride ?? config.Model;
        var generationConfig = new Dictionary<string, object?>
        {
            ["maxOutputTokens"] = Math.Max(64, request.MaxOutputTokens),
            ["responseMimeType"] = "text/plain",
        };
        var thinkingConfig = GeminiThinkingConfigResolver.Resolve(model, config.ReasoningEffort);
        if (thinkingConfig is not null)
        {
            generationConfig["thinkingConfig"] = thinkingConfig;
        }

        var payload = new Dictionary<string, object?>
        {
            ["contents"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["parts"] = new object[]
                    {
                        new Dictionary<string, string>
                        {
                            ["text"] = request.Input,
                        },
                    },
                },
            },
            ["generationConfig"] = generationConfig,
        };

        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            payload["system_instruction"] = new Dictionary<string, object?>
            {
                ["parts"] = new object[]
                {
                    new Dictionary<string, string>
                    {
                        ["text"] = request.Instructions,
                    },
                },
            };
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{ResolveBaseUrl().TrimEnd('/')}/models/{model}:generateContent")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), System.Text.Encoding.UTF8, "application/json"),
        };
        message.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        message.Headers.TryAddWithoutValidation("x-goog-api-client", "carves-runtime/0.4");

        using var response = httpClient.Send(message);
        var responseBody = HttpContentSyncReader.ReadAsString(response.Content);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gemini GenerateContent API returned {(int)response.StatusCode}: {responseBody}");
        }

        var envelope = JsonSerializer.Deserialize<ResponseEnvelope>(responseBody, JsonOptions)
            ?? throw new InvalidOperationException("Gemini GenerateContent API returned an empty payload.");
        var outputText = ExtractText(envelope);
        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw new InvalidOperationException("Gemini GenerateContent API returned no text output.");
        }

        return new AiExecutionRecord
        {
            Provider = "gemini",
            WorkerAdapter = "gemini",
            WorkerAdapterReason = "Gemini native AI client executed successfully.",
            ProtocolFamily = "gemini_native",
            RequestFamily = "generate_content",
            Model = envelope.ModelVersion ?? model,
            Configured = true,
            Succeeded = true,
            RequestPreview = BuildPreview(request.Input),
            RequestHash = Hash(request.Input),
            ResponsePreview = BuildPreview(outputText),
            ResponseHash = Hash(outputText),
            OutputText = outputText,
            InputTokens = envelope.UsageMetadata?.PromptTokenCount,
            OutputTokens = envelope.UsageMetadata?.CandidatesTokenCount,
            CapturedAt = DateTimeOffset.UtcNow,
        };
    }

    private string ResolveBaseUrl()
    {
        return string.IsNullOrWhiteSpace(config.BaseUrl)
            || string.Equals(config.BaseUrl, "https://api.openai.com/v1", StringComparison.OrdinalIgnoreCase)
            ? DefaultBaseUrl
            : config.BaseUrl;
    }

    private static string ExtractText(ResponseEnvelope payload)
    {
        if (payload.Candidates is null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var candidate in payload.Candidates)
        {
            if (candidate.Content?.Parts is null)
            {
                continue;
            }

            foreach (var part in candidate.Content.Parts)
            {
                if (!string.IsNullOrWhiteSpace(part.Text))
                {
                    parts.Add(part.Text);
                }
            }
        }

        return string.Join(Environment.NewLine, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildPreview(string value)
    {
        return value.Length > 160 ? value[..160] : value;
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private sealed class ResponseEnvelope
    {
        public CandidateEnvelope[]? Candidates { get; init; }

        public UsageMetadataEnvelope? UsageMetadata { get; init; }

        public string? ModelVersion { get; init; }
    }

    private sealed class CandidateEnvelope
    {
        public ContentEnvelope? Content { get; init; }
    }

    private sealed class ContentEnvelope
    {
        public PartEnvelope[]? Parts { get; init; }
    }

    private sealed class PartEnvelope
    {
        public string? Text { get; init; }
    }

    private sealed class UsageMetadataEnvelope
    {
        public int? PromptTokenCount { get; init; }

        public int? CandidatesTokenCount { get; init; }
    }
}
