using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class OpenAiCompatibleProtocol
{
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

    private ProviderProtocolResult FailureFromResponse(WorkerExecutionRequest request, HttpTransportResponse response, WorkerFailureKind failureKind)
    {
        var summary = requestFamily == OpenAiCompatibleRequestFamily.ChatCompletions
            ? $"OpenAI Chat Completions API returned {response.StatusCode}."
            : $"OpenAI Responses API returned {response.StatusCode}.";
        return new ProviderProtocolResult
        {
            Status = failureKind is WorkerFailureKind.EnvironmentBlocked or WorkerFailureKind.PolicyDenied
                ? WorkerExecutionStatus.Blocked
                : WorkerExecutionStatus.Failed,
            FailureKind = failureKind,
            FailureLayer = WorkerFailureLayer.Provider,
            Retryable = failureKind == WorkerFailureKind.TransientInfra,
            Configured = true,
            Model = request.ModelOverride ?? config.Model,
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

        if (body.Contains("policy", StringComparison.OrdinalIgnoreCase) || body.Contains("blocked", StringComparison.OrdinalIgnoreCase))
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
}
