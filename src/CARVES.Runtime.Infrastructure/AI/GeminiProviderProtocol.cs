using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed class GeminiProviderProtocol : IProviderProtocol
{
    private const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly AiProviderConfig config;
    private readonly string? apiKey;

    public GeminiProviderProtocol(AiProviderConfig config)
    {
        this.config = config;
        apiKey = Environment.GetEnvironmentVariable(config.ApiKeyEnvironmentVariable);
    }

    public string ProviderId => "gemini";

    public ProviderProtocolMetadata Metadata { get; } = new()
    {
        ProtocolId = nameof(GeminiProviderProtocol),
        ProtocolFamily = "gemini_native",
        RequestFamily = "generate_content",
        SupportsStreaming = false,
        SupportsToolCalls = false,
        SupportsJsonMode = true,
        SupportsSystemPrompt = true,
        SupportsFileUpload = false,
    };

    public bool IsConfigured => config.Enabled && !string.IsNullOrWhiteSpace(apiKey);

    public WorkerBackendHealthSummary CheckHealth()
    {
        return new WorkerBackendHealthSummary
        {
            State = IsConfigured ? WorkerBackendHealthState.Healthy : WorkerBackendHealthState.Unavailable,
            Summary = IsConfigured
                ? "Gemini native worker protocol is configured."
                : $"Gemini native worker protocol is unavailable because '{config.ApiKeyEnvironmentVariable}' is not set or provider is disabled.",
        };
    }

    public HttpTransportRequest BuildRequest(WorkerExecutionRequest request)
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
        var reasoningEffort = string.IsNullOrWhiteSpace(request.ReasoningEffort) ? config.ReasoningEffort : request.ReasoningEffort;
        var thinkingConfig = GeminiThinkingConfigResolver.Resolve(model, reasoningEffort);
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

        return new HttpTransportRequest
        {
            Method = "POST",
            Url = $"{ResolveBaseUrl().TrimEnd('/')}/models/{model}:generateContent",
            Body = JsonSerializer.Serialize(payload, JsonOptions),
            ContentType = "application/json",
            TimeoutSeconds = Math.Max(5, request.TimeoutSeconds),
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-goog-api-key"] = apiKey,
                ["x-goog-api-client"] = "carves-runtime/0.4",
            },
        };
    }

    public ProviderProtocolResult ParseResponse(WorkerExecutionRequest request, HttpTransportResponse response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return FailureFromResponse(response, ClassifyFailureKind(response.StatusCode, response.Body));
        }

        var payload = JsonSerializer.Deserialize<ResponseEnvelope>(response.Body, JsonOptions)
            ?? throw new InvalidOperationException("Gemini provider returned an empty payload.");
        var outputText = ExtractText(payload);
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return new ProviderProtocolResult
            {
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.InvalidOutput,
                FailureLayer = WorkerFailureLayer.Protocol,
                Configured = true,
                Model = payload.ModelVersion ?? request.ModelOverride ?? config.Model,
                Summary = "Gemini provider returned no text output.",
                FailureReason = "Gemini provider returned no text output.",
                RawResponse = response.Body,
                HttpStatusCode = response.StatusCode,
                TransportLatencyMs = response.LatencyMs,
                Events =
                [
                    new WorkerEvent
                    {
                        EventType = WorkerEventType.RawError,
                        Summary = "Gemini provider returned no text output.",
                        RawPayload = response.Body,
                        Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["provider"] = ProviderId,
                            ["protocol_family"] = Metadata.ProtocolFamily,
                        },
                    },
                ],
            };
        }

        return new ProviderProtocolResult
        {
            Status = WorkerExecutionStatus.Succeeded,
            FailureKind = WorkerFailureKind.None,
            FailureLayer = WorkerFailureLayer.None,
            Configured = true,
            Model = payload.ModelVersion ?? request.ModelOverride ?? config.Model,
            Summary = outputText,
            OutputText = outputText,
            RawResponse = response.Body,
            HttpStatusCode = response.StatusCode,
            TransportLatencyMs = response.LatencyMs,
            InputTokens = payload.UsageMetadata?.PromptTokenCount,
            OutputTokens = payload.UsageMetadata?.CandidatesTokenCount,
            Events =
            [
                new WorkerEvent
                {
                    EventType = WorkerEventType.FinalSummary,
                    Summary = outputText,
                    RawPayload = response.Body,
                    Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["provider"] = ProviderId,
                        ["protocol_family"] = Metadata.ProtocolFamily,
                        ["request_family"] = Metadata.RequestFamily,
                    },
                },
            ],
        };
    }

    public ProviderProtocolResult FromException(WorkerExecutionRequest request, Exception exception)
    {
        var failureKind = exception.Message.Contains("api key", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase)
            ? WorkerFailureKind.EnvironmentBlocked
            : WorkerFailureKind.TransientInfra;
        return new ProviderProtocolResult
        {
            Status = WorkerExecutionStatus.Blocked,
            FailureKind = failureKind,
            FailureLayer = failureKind == WorkerFailureKind.EnvironmentBlocked ? WorkerFailureLayer.Environment : WorkerFailureLayer.Transport,
            Retryable = failureKind == WorkerFailureKind.TransientInfra,
            Configured = IsConfigured,
            Model = request.ModelOverride ?? config.Model,
            Summary = exception.Message,
            FailureReason = exception.Message,
            RawResponse = exception.ToString(),
            HttpStatusCode = null,
            TransportLatencyMs = null,
            Events =
            [
                new WorkerEvent
                {
                    EventType = WorkerEventType.RawError,
                    Summary = exception.Message,
                    RawPayload = exception.ToString(),
                    Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["provider"] = ProviderId,
                        ["protocol_family"] = Metadata.ProtocolFamily,
                    },
                },
            ],
        };
    }

    private ProviderProtocolResult FailureFromResponse(HttpTransportResponse response, WorkerFailureKind failureKind)
    {
        var summary = $"Gemini API returned {response.StatusCode}.";
        return new ProviderProtocolResult
        {
            Status = failureKind is WorkerFailureKind.EnvironmentBlocked or WorkerFailureKind.PolicyDenied
                ? WorkerExecutionStatus.Blocked
                : WorkerExecutionStatus.Failed,
            FailureKind = failureKind,
            FailureLayer = WorkerFailureLayer.Provider,
            Retryable = failureKind == WorkerFailureKind.TransientInfra,
            Configured = true,
            Model = config.Model,
            Summary = summary,
            FailureReason = FirstNonEmpty(TryExtractErrorMessage(response.Body), summary),
            RawResponse = response.Body,
            HttpStatusCode = response.StatusCode,
            TransportLatencyMs = response.LatencyMs,
            Events =
            [
                new WorkerEvent
                {
                    EventType = WorkerEventType.RawError,
                    Summary = summary,
                    RawPayload = response.Body,
                    Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["provider"] = ProviderId,
                        ["protocol_family"] = Metadata.ProtocolFamily,
                        ["status_code"] = response.StatusCode.ToString(),
                    },
                },
            ],
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

    private static WorkerFailureKind ClassifyFailureKind(int statusCode, string body)
    {
        if (statusCode is 401 or 403)
        {
            return WorkerFailureKind.EnvironmentBlocked;
        }

        if (statusCode == 429 || statusCode >= 500)
        {
            return WorkerFailureKind.TransientInfra;
        }

        if (body.Contains("blocked", StringComparison.OrdinalIgnoreCase) || body.Contains("safety", StringComparison.OrdinalIgnoreCase))
        {
            return WorkerFailureKind.PolicyDenied;
        }

        return WorkerFailureKind.TaskLogicFailed;
    }

    private static string? TryExtractErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
                {
                    return message.GetString();
                }

                return error.ToString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
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
