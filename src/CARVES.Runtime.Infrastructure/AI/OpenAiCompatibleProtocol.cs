using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class OpenAiCompatibleProtocol : IProviderProtocol
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly AiProviderConfig config;
    private readonly string? apiKey;
    private readonly string requestFamily;
    private readonly ProviderProtocolMetadata metadata;

    public OpenAiCompatibleProtocol(AiProviderConfig config)
    {
        this.config = config;
        apiKey = Environment.GetEnvironmentVariable(config.ApiKeyEnvironmentVariable);
        requestFamily = OpenAiCompatibleRequestFamily.Resolve(config);
        metadata = new ProviderProtocolMetadata
        {
            ProtocolId = nameof(OpenAiCompatibleProtocol),
            ProtocolFamily = "openai_compatible",
            RequestFamily = requestFamily,
            SupportsStreaming = false,
            SupportsToolCalls = false,
            SupportsJsonMode = true,
            SupportsSystemPrompt = true,
            SupportsFileUpload = false,
        };
    }

    public string ProviderId => "openai";

    public ProviderProtocolMetadata Metadata => metadata;

    public bool IsConfigured => config.Enabled && !string.IsNullOrWhiteSpace(apiKey);

    public WorkerBackendHealthSummary CheckHealth()
    {
        return new WorkerBackendHealthSummary
        {
            State = IsConfigured ? WorkerBackendHealthState.Healthy : WorkerBackendHealthState.Unavailable,
            Summary = IsConfigured
                ? $"OpenAI-compatible worker protocol ({requestFamily}) is configured."
                : $"OpenAI-compatible worker protocol is unavailable because '{config.ApiKeyEnvironmentVariable}' is not set or provider is disabled.",
        };
    }

    public HttpTransportRequest BuildRequest(WorkerExecutionRequest request)
    {
        if (!config.Enabled)
        {
            throw new InvalidOperationException("OpenAI-compatible provider is disabled in ai_provider.json.");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"Environment variable '{config.ApiKeyEnvironmentVariable}' is not set.");
        }

        var model = request.ModelOverride ?? config.Model;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = $"Bearer {apiKey}",
        };
        if (!string.IsNullOrWhiteSpace(config.Organization))
        {
            headers["OpenAI-Organization"] = config.Organization;
        }

        if (!string.IsNullOrWhiteSpace(config.Project))
        {
            headers["OpenAI-Project"] = config.Project;
        }

        return new HttpTransportRequest
        {
            Method = "POST",
            Url = requestFamily == OpenAiCompatibleRequestFamily.ChatCompletions
                ? $"{config.BaseUrl.TrimEnd('/')}/chat/completions"
                : $"{config.BaseUrl.TrimEnd('/')}/responses",
            Body = JsonSerializer.Serialize(BuildPayload(model, request), JsonOptions),
            ContentType = "application/json",
            TimeoutSeconds = Math.Max(5, request.TimeoutSeconds),
            Headers = headers,
        };
    }

    public ProviderProtocolResult ParseResponse(WorkerExecutionRequest request, HttpTransportResponse response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return FailureFromResponse(request, response, ClassifyFailureKind(response.StatusCode, response.Body));
        }

        var payload = requestFamily == OpenAiCompatibleRequestFamily.ChatCompletions
            ? ParseChatCompletionsResponse(request, response)
            : ParseResponsesApiResponse(request, response);
        var outputText = payload.OutputText;
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
                Summary = "OpenAI-compatible protocol returned no text output.",
                FailureReason = "OpenAI-compatible protocol returned no text output.",
                RawResponse = response.Body,
                HttpStatusCode = response.StatusCode,
                TransportLatencyMs = response.LatencyMs,
                Events =
                [
                    new WorkerEvent
                    {
                        EventType = WorkerEventType.RawError,
                        Summary = "OpenAI-compatible protocol returned no text output.",
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
            InputTokens = payload.InputTokens,
            OutputTokens = payload.OutputTokens,
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

}
