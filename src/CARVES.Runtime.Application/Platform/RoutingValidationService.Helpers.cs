using System.Text.Json;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RoutingValidationService
{
    private static TaskNode BuildTaskNode(RoutingValidationTaskDefinition validationTask)
    {
        return new TaskNode
        {
            TaskId = validationTask.TaskId,
            Title = validationTask.TaskType,
            Description = validationTask.Summary ?? validationTask.TaskType,
            TaskType = TaskType.Execution,
            Scope = string.IsNullOrWhiteSpace(validationTask.ModuleId) ? [] : [validationTask.ModuleId],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["routing_intent"] = validationTask.RoutingIntent,
                ["module_id"] = validationTask.ModuleId ?? string.Empty,
                ["validation_task_type"] = validationTask.TaskType,
            },
        };
    }

    private static RoutingValidationTrace BuildSelectionFailureTrace(
        RoutingValidationTaskDefinition validationTask,
        RoutingValidationMode mode,
        string runId,
        string? activeProfileId,
        WorkerSelectionDecision? selection,
        ValidationRouteResolution routeResolution)
    {
        return new RoutingValidationTrace
        {
            RunId = runId,
            TaskId = validationTask.TaskId,
            TaskType = validationTask.TaskType,
            RoutingIntent = validationTask.RoutingIntent,
            ModuleId = validationTask.ModuleId,
            ExecutionMode = mode,
            RoutingProfileId = activeProfileId,
            RouteSource = selection?.RouteSource ?? "selection_failed",
            SelectedProvider = selection?.SelectedProviderId,
            SelectedLane = null,
            SelectedBackend = selection?.SelectedBackendId,
            SelectedModel = selection?.SelectedModelId,
            SelectedRoutingProfileId = selection?.SelectedRoutingProfileId,
            AppliedRoutingRuleId = selection?.AppliedRoutingRuleId,
            FallbackConfigured = selection?.Candidates.Any(candidate => string.Equals(candidate.RouteDisposition, "fallback", StringComparison.Ordinal)) ?? false,
            FallbackTriggered = false,
            PreferredRouteEligibility = selection?.PreferredRouteEligibility,
            PreferredIneligibilityReason = selection?.PreferredIneligibilityReason,
            SelectedBecause = selection?.SelectedBecause.ToArray() ?? [],
            LatencyMs = 0,
            RequestSucceeded = false,
            TaskSucceeded = false,
            SchemaValid = false,
            BuildOutcome = RoutingValidationExecutionOutcome.NotRun,
            TestOutcome = RoutingValidationExecutionOutcome.NotRun,
            SafetyOutcome = RoutingValidationExecutionOutcome.NotRun,
            RetryCount = 0,
            PatchAccepted = false,
            FailureCategory = selection?.ReasonCode ?? "no_eligible_route",
            ReasonCodes = string.IsNullOrWhiteSpace(selection?.ReasonCode)
                ? routeResolution.SelectedBecause.Length == 0 ? [] : routeResolution.SelectedBecause
                : [selection.ReasonCode],
            Candidates = routeResolution.Candidates,
        };
    }

    private static ModelQualificationLane? ResolveLane(ModelQualificationMatrix matrix, WorkerSelectionDecision selection)
    {
        return matrix.Lanes.FirstOrDefault(item =>
                   string.Equals(item.ProviderId, selection.SelectedProviderId, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(item.BackendId, selection.SelectedBackendId, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(item.Model, selection.SelectedModelId, StringComparison.Ordinal))
               ?? matrix.Lanes.FirstOrDefault(item =>
                   string.Equals(item.ProviderId, selection.SelectedProviderId, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(item.BackendId, selection.SelectedBackendId, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(item.RoutingProfileId, selection.SelectedRoutingProfileId, StringComparison.Ordinal));
    }

    private static bool EvaluateFormat(RoutingValidationTaskDefinition validationTask, string output)
    {
        if (validationTask.ExpectedFormat != ModelQualificationExpectedFormat.Json)
        {
            return !string.IsNullOrWhiteSpace(output);
        }

        try
        {
            using var document = JsonDocument.Parse(output);
            return validationTask.RequiredJsonFields.All(field => document.RootElement.TryGetProperty(field, out _));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string[] BuildReasonCodes(WorkerSelectionDecision? selection, WorkerExecutionResult result)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(selection?.ReasonCode))
        {
            values.Add(selection.ReasonCode);
        }

        if (!result.Succeeded)
        {
            values.Add(WorkerFailureSemantics.Classify(result).ReasonCode);
        }

        return values.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static decimal? EstimateCostUsd(ModelQualificationLane lane, int? inputTokens, int? outputTokens)
    {
        if (inputTokens is null && outputTokens is null)
        {
            return null;
        }

        var inputRate = lane.LaneId switch
        {
            "groq-chat" => 0.00000059m,
            "deepseek-chat" => 0.00000027m,
            "n1n-responses" => 0.00000200m,
            "gemini-native-balanced" => 0.00000125m,
            "codex-sdk-worker" => 0.00000300m,
            "codex-cli-worker" => 0.00000300m,
            _ => 0m,
        };
        var outputRate = lane.LaneId switch
        {
            "groq-chat" => 0.00000079m,
            "deepseek-chat" => 0.00000110m,
            "n1n-responses" => 0.00000800m,
            "gemini-native-balanced" => 0.00001000m,
            "codex-sdk-worker" => 0.00001500m,
            "codex-cli-worker" => 0.00001500m,
            _ => 0m,
        };
        return ((inputTokens ?? 0) * inputRate) + ((outputTokens ?? 0) * outputRate);
    }
}
