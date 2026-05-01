using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed class ClaudeProviderProtocol : IProviderProtocol
{
    private const string DefaultBaseUrl = "https://api.anthropic.com/v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly AiProviderConfig config;
    private readonly string? apiKey;

    public ClaudeProviderProtocol(AiProviderConfig config)
    {
        this.config = config;
        apiKey = Environment.GetEnvironmentVariable(config.ApiKeyEnvironmentVariable);
    }

    public string ProviderId => "claude";

    public ProviderProtocolMetadata Metadata { get; } = new()
    {
        ProtocolId = nameof(ClaudeProviderProtocol),
        ProtocolFamily = "anthropic_native",
        RequestFamily = "messages_api",
        SupportsStreaming = false,
        SupportsToolCalls = false,
        SupportsJsonMode = false,
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
                ? "Anthropic Claude worker protocol is configured."
                : $"Anthropic Claude worker protocol is unavailable because '{config.ApiKeyEnvironmentVariable}' is not set or provider is disabled.",
        };
    }

    public HttpTransportRequest BuildRequest(WorkerExecutionRequest request)
    {
        if (!config.Enabled)
        {
            throw new InvalidOperationException("Claude provider is disabled in ai_provider.json.");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"Environment variable '{config.ApiKeyEnvironmentVariable}' is not set.");
        }

        var payload = new ClaudeMessagesRequest
        {
            Model = request.ModelOverride ?? config.Model,
            MaxTokens = Math.Max(64, request.MaxOutputTokens),
            System = request.Instructions,
            Messages =
            [
                new ClaudeMessage
                {
                    Role = "user",
                    Content = request.Input,
                },
            ],
        };

        return new HttpTransportRequest
        {
            Method = "POST",
            Url = $"{ResolveBaseUrl().TrimEnd('/')}/messages",
            Body = JsonSerializer.Serialize(payload, JsonOptions),
            ContentType = "application/json",
            TimeoutSeconds = Math.Max(5, request.TimeoutSeconds),
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-api-key"] = apiKey,
                ["anthropic-version"] = "2023-06-01",
                ["anthropic-beta"] = "output-128k-2025-02-19",
            },
        };
    }

    public ProviderProtocolResult ParseResponse(WorkerExecutionRequest request, HttpTransportResponse response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return FailureFromResponse(response, ClassifyFailureKind(response.StatusCode, response.Body));
        }

        var payload = JsonSerializer.Deserialize<ClaudeResponseEnvelope>(response.Body, JsonOptions)
            ?? throw new InvalidOperationException("Claude Messages API returned an empty payload.");
        var outputText = ExtractText(payload);
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return new ProviderProtocolResult
            {
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.InvalidOutput,
                FailureLayer = WorkerFailureLayer.Protocol,
                Configured = true,
                Model = payload.Model ?? request.ModelOverride ?? config.Model,
                RequestId = payload.Id,
                Summary = "Claude worker returned no text output.",
                FailureReason = "Claude worker returned no text output.",
                RawResponse = response.Body,
                HttpStatusCode = response.StatusCode,
                TransportLatencyMs = response.LatencyMs,
                Events =
                [
                    new WorkerEvent
                    {
                        EventType = WorkerEventType.RawError,
                        Summary = "Claude worker returned no text output.",
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
            Model = payload.Model ?? request.ModelOverride ?? config.Model,
            RequestId = payload.Id,
            Summary = outputText,
            OutputText = outputText,
            RawResponse = response.Body,
            HttpStatusCode = response.StatusCode,
            TransportLatencyMs = response.LatencyMs,
            InputTokens = payload.Usage?.InputTokens,
            OutputTokens = payload.Usage?.OutputTokens,
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
        var summary = $"Claude Messages API returned {response.StatusCode}.";
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

    private static string ExtractText(ClaudeResponseEnvelope payload)
    {
        if (payload.Content is null)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            payload.Content
                .Where(item => string.Equals(item.Type, "text", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Text))
                .Select(item => item.Text));
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

        if (body.Contains("policy", StringComparison.OrdinalIgnoreCase)
            || body.Contains("blocked", StringComparison.OrdinalIgnoreCase)
            || body.Contains("safety", StringComparison.OrdinalIgnoreCase))
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

    private sealed class ClaudeMessagesRequest
    {
        public string Model { get; init; } = string.Empty;

        public int MaxTokens { get; init; }

        public string? System { get; init; }

        public IReadOnlyList<ClaudeMessage> Messages { get; init; } = Array.Empty<ClaudeMessage>();
    }

    private sealed class ClaudeMessage
    {
        public string Role { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;
    }

    private sealed class ClaudeResponseEnvelope
    {
        public string? Id { get; init; }

        public string? Model { get; init; }

        public IReadOnlyList<ClaudeResponseContent> Content { get; init; } = Array.Empty<ClaudeResponseContent>();

        public ClaudeUsage? Usage { get; init; }
    }

    private sealed class ClaudeResponseContent
    {
        public string Type { get; init; } = string.Empty;

        public string Text { get; init; } = string.Empty;
    }

    private sealed class ClaudeUsage
    {
        public int? InputTokens { get; init; }

        public int? OutputTokens { get; init; }
    }
}
