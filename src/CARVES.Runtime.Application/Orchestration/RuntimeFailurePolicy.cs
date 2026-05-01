using System.Text.Json;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Orchestration;

public sealed class RuntimeFailurePolicy
{
    public RuntimeFailureRecord ClassifyException(RuntimeSessionState session, string? taskId, Exception exception)
    {
        var failureType = exception switch
        {
            TaskExecutionIsolationTimeoutException => RuntimeFailureType.SchedulerDecisionFailure,
            SchedulerDecisionException => RuntimeFailureType.SchedulerDecisionFailure,
            JsonException => RuntimeFailureType.SchemaMismatch,
            InvalidDataException => RuntimeFailureType.ControlPlaneDesync,
            IOException or UnauthorizedAccessException => RuntimeFailureType.ArtifactPersistenceFailure,
            _ => RuntimeFailureType.WorkerExecutionFailure,
        };

        return CreateRecord(session, taskId, failureType, ResolveAction(failureType), exception.Message, "runtime-exception", exception.GetType().FullName);
    }

    public RuntimeFailureRecord CreateReviewRejected(RuntimeSessionState? session, string taskId, string reason)
    {
        return CreateRecord(
            session,
            taskId,
            RuntimeFailureType.ReviewRejected,
            RuntimeFailureAction.RetryTask,
            reason,
            "review-boundary",
            exceptionType: null);
    }

    public RuntimeFailureRecord ClassifyWorkerFailure(RuntimeSessionState session, string taskId, WorkerExecutionResult result, WorkerRecoveryDecision recoveryDecision)
    {
        var reason = ResolveWorkerFailureReason(result);
        var classification = WorkerFailureSemantics.Classify(result);

        return CreateRecord(
            session,
            taskId,
            RuntimeFailureType.WorkerExecutionFailure,
            MapRecoveryAction(recoveryDecision.Action),
            $"{reason} Lane={classification.Lane}; ReasonCode={classification.ReasonCode}; Recovery: {recoveryDecision.Reason}",
            $"worker:{result.BackendId}:{classification.Lane.ToString().ToLowerInvariant()}:{classification.SubstrateCategory ?? classification.ReasonCode}",
            exceptionType: result.FailureKind.ToString());
    }

    private static string ResolveWorkerFailureReason(WorkerExecutionResult result)
    {
        var summary = result.Summary?.Trim();
        var failureReason = result.FailureReason?.Trim();
        var providerSummary = FirstNonEmpty(
            DescribeProviderFailure(result),
            DescribeProviderFailureFromEvents(result));

        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = providerSummary;
        }
        else if (!string.IsNullOrWhiteSpace(providerSummary)
                 && !summary.Contains(providerSummary, StringComparison.OrdinalIgnoreCase))
        {
            summary = $"{providerSummary} Detail={summary}";
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            return string.IsNullOrWhiteSpace(failureReason) ? "Worker execution failed." : failureReason;
        }

        if (string.IsNullOrWhiteSpace(failureReason)
            || string.Equals(summary, failureReason, StringComparison.OrdinalIgnoreCase))
        {
            return summary;
        }

        return $"{summary} Detail={failureReason}";
    }

    private static string? DescribeProviderFailure(WorkerExecutionResult result)
    {
        if (result.ProviderStatusCode is null)
        {
            return null;
        }

        return (result.ProviderId, result.RequestFamily) switch
        {
            ("openai", "responses_api") => $"OpenAI Responses API returned {result.ProviderStatusCode}.",
            ("openai", "chat_completions") => $"OpenAI Chat Completions API returned {result.ProviderStatusCode}.",
            ("gemini", _) => $"Gemini provider returned {result.ProviderStatusCode}.",
            _ => $"{result.ProviderId} provider returned {result.ProviderStatusCode}.",
        };
    }

    private static string? DescribeProviderFailureFromEvents(WorkerExecutionResult result)
    {
        foreach (var @event in result.Events)
        {
            if (!string.IsNullOrWhiteSpace(@event.Summary)
                && (@event.Summary.Contains(" API returned ", StringComparison.OrdinalIgnoreCase)
                    || @event.Summary.Contains(" provider returned ", StringComparison.OrdinalIgnoreCase)))
            {
                return @event.Summary.Trim();
            }

            if (@event.Attributes.TryGetValue("status_code", out var statusCode)
                && int.TryParse(statusCode, out var parsedStatusCode))
            {
                var synthesized = DescribeProviderFailure(result with { ProviderStatusCode = parsedStatusCode });
                if (!string.IsNullOrWhiteSpace(synthesized))
                {
                    return synthesized;
                }
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    public RuntimeFailureAction ResolveAction(RuntimeFailureType failureType)
    {
        return failureType switch
        {
            RuntimeFailureType.ReviewRejected => RuntimeFailureAction.RetryTask,
            RuntimeFailureType.WorkerExecutionFailure => RuntimeFailureAction.AbortTask,
            RuntimeFailureType.ArtifactPersistenceFailure => RuntimeFailureAction.PauseSession,
            RuntimeFailureType.SchedulerDecisionFailure => RuntimeFailureAction.PauseSession,
            RuntimeFailureType.ControlPlaneDesync => RuntimeFailureAction.PauseSession,
            RuntimeFailureType.SchemaMismatch => RuntimeFailureAction.EscalateToOperator,
            RuntimeFailureType.InvariantViolation => RuntimeFailureAction.EscalateToOperator,
            _ => RuntimeFailureAction.EscalateToOperator,
        };
    }

    private RuntimeFailureRecord CreateRecord(
        RuntimeSessionState? session,
        string? taskId,
        RuntimeFailureType failureType,
        RuntimeFailureAction action,
        string reason,
        string source,
        string? exceptionType)
    {
        var capturedAt = DateTimeOffset.UtcNow;
        var sessionId = session?.SessionId ?? "default";
        var tickCount = session?.TickCount ?? 0;
        return new RuntimeFailureRecord
        {
            FailureId = $"{sessionId}-{tickCount:D4}-{capturedAt:yyyyMMddHHmmssfff}-{failureType}".ToLowerInvariant(),
            SessionId = sessionId,
            AttachedRepoRoot = session?.AttachedRepoRoot ?? string.Empty,
            TaskId = taskId,
            FailureType = failureType,
            Action = action,
            SessionStatus = session?.Status ?? RuntimeSessionStatus.Idle,
            TickCount = tickCount,
            Reason = reason,
            Source = source,
            ExceptionType = exceptionType,
            CapturedAt = capturedAt,
        };
    }

    private static RuntimeFailureAction MapRecoveryAction(WorkerRecoveryAction action)
    {
        return action switch
        {
            WorkerRecoveryAction.Retry => RuntimeFailureAction.RetryTask,
            WorkerRecoveryAction.RebuildWorktree => RuntimeFailureAction.RebuildWorktree,
            WorkerRecoveryAction.SwitchProvider => RuntimeFailureAction.SwitchProvider,
            WorkerRecoveryAction.BlockTask => RuntimeFailureAction.BlockTask,
            WorkerRecoveryAction.EscalateToOperator => RuntimeFailureAction.EscalateToOperator,
            _ => RuntimeFailureAction.AbortTask,
        };
    }
}
