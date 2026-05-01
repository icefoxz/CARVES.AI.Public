using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };
    private static readonly Regex TaskIdPattern = new(@"\bT-[A-Z0-9][A-Z0-9\-]*\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly RuntimeServices services;
    private readonly HostAcceptedOperationStore? acceptedOperationStore;

    public LocalHostSurfaceService(RuntimeServices services, HostAcceptedOperationStore? acceptedOperationStore = null)
    {
        this.services = services;
        this.acceptedOperationStore = acceptedOperationStore;
    }

    private static JsonObject? BuildCompletionNode(TaskNode task)
    {
        if (!task.Metadata.TryGetValue("completion_provenance", out var provenance)
            || string.IsNullOrWhiteSpace(provenance))
        {
            return null;
        }

        return new JsonObject
        {
            ["mode"] = provenance,
            ["outcome_status"] = task.Metadata.GetValueOrDefault("completion_outcome_status") ?? task.Status.ToString(),
            ["recorded_at"] = task.Metadata.GetValueOrDefault("completion_recorded_at"),
            ["reason"] = task.Metadata.GetValueOrDefault("completion_reason") ?? task.PlannerReview.Reason,
            ["historical_run_id"] = task.Metadata.GetValueOrDefault("completion_historical_run_id"),
            ["historical_run_status"] = task.Metadata.GetValueOrDefault("completion_historical_run_status"),
            ["historical_worker_run_id"] = task.Metadata.GetValueOrDefault("completion_historical_worker_run_id"),
            ["historical_worker_backend"] = task.Metadata.GetValueOrDefault("completion_historical_worker_backend"),
            ["historical_worker_failure_kind"] = task.Metadata.GetValueOrDefault("completion_historical_worker_failure_kind"),
            ["historical_worker_summary"] = task.Metadata.GetValueOrDefault("completion_historical_worker_summary"),
            ["historical_worker_detail_ref"] = task.Metadata.GetValueOrDefault("completion_historical_worker_detail_ref"),
            ["historical_provider_detail_ref"] = task.Metadata.GetValueOrDefault("completion_historical_provider_detail_ref"),
            ["historical_recovery_action"] = task.Metadata.GetValueOrDefault("completion_historical_recovery_action"),
            ["historical_recovery_reason"] = task.Metadata.GetValueOrDefault("completion_historical_recovery_reason"),
            ["summary"] = task.Metadata.GetValueOrDefault("completion_reason") ?? task.PlannerReview.Reason,
        };
    }

    private static JsonObject? BuildExecutionRunNode(TaskNode task, ExecutionRun? latestRun, int runCount, JsonObject? completion)
    {
        if (latestRun is null)
        {
            return null;
        }

        if (completion is not null
            && string.Equals(completion["mode"]?.GetValue<string>(), "manual_fallback", StringComparison.Ordinal))
        {
            return new JsonObject
            {
                ["latest_run_id"] = latestRun.RunId,
                ["latest_status"] = completion["outcome_status"]?.GetValue<string>() ?? "ManualFallbackCompleted",
                ["active_run_id"] = null,
                ["run_count"] = runCount,
                ["current_step_index"] = null,
                ["current_step_title"] = "Task completed through manual fallback; delegated execution history preserved separately.",
                ["historical_latest_status"] = latestRun.Status.ToString(),
                ["historical_current_step_index"] = latestRun.CurrentStepIndex,
                ["historical_current_step_title"] = latestRun.Steps.Count == 0
                    ? "(none)"
                    : latestRun.Steps[Math.Clamp(latestRun.CurrentStepIndex, 0, latestRun.Steps.Count - 1)].Title,
            };
        }

        return new JsonObject
        {
            ["latest_run_id"] = latestRun.RunId,
            ["latest_status"] = latestRun.Status.ToString(),
            ["active_run_id"] = task.Metadata.GetValueOrDefault("execution_run_active_id"),
            ["run_count"] = runCount,
            ["current_step_index"] = latestRun.CurrentStepIndex,
            ["current_step_title"] = latestRun.Steps.Count == 0
                ? "(none)"
                : latestRun.Steps[Math.Clamp(latestRun.CurrentStepIndex, 0, latestRun.Steps.Count - 1)].Title,
        };
    }

    public JsonObject BuildDiscussionContext()
    {
        return DiscussionProjection.BuildDiscussionContext(this);
    }

    public JsonObject BuildDiscussionBriefPreview()
    {
        return DiscussionProjection.BuildDiscussionBriefPreview(this);
    }

    public JsonObject BuildAgentStatusContext()
    {
        return DiscussionProjection.BuildAgentStatusContext(this);
    }

    public JsonObject BuildDiscussionPlanner()
    {
        return DiscussionProjection.BuildDiscussionPlanner(this);
    }

    public JsonObject BuildDiscussionBlocked()
    {
        return DiscussionProjection.BuildDiscussionBlocked(this);
    }

    public JsonObject BuildDiscussionCard(string cardId)
    {
        return DiscussionProjection.BuildDiscussionCard(this, cardId);
    }

    public JsonObject BuildDiscussionTask(string taskId)
    {
        return DiscussionProjection.BuildDiscussionTask(this, taskId);
    }

    public string ToPrettyJson(JsonNode node)
    {
        return node.ToJsonString(JsonOptions);
    }

    public string ToPrettyJson<T>(T payload)
    {
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static JsonObject ToJsonObject(ProjectUnderstandingProjection projection)
    {
        return new JsonObject
        {
            ["state"] = projection.State.ToString().ToLowerInvariant(),
            ["action"] = projection.Action,
            ["summary"] = projection.Summary,
            ["rationale"] = projection.Rationale,
            ["generated_at"] = projection.GeneratedAt,
            ["module_count"] = projection.ModuleCount,
            ["file_count"] = projection.FileCount,
            ["callable_count"] = projection.CallableCount,
            ["dependency_count"] = projection.DependencyCount,
            ["module_summaries"] = ToJsonArray(projection.ModuleSummaries),
        };
    }

}
