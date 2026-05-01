using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectReplan(string taskId)
    {
        taskGraphService.GetTask(taskId);
        var service = new PlannerEmergenceService(paths, taskGraphService, executionRunService);
        var entry = service.TryGetLatestReplan(taskId);
        if (entry is null)
        {
            return OperatorCommandResult.Success($"No replan entry exists for {taskId}.");
        }

        var suggestions = service.ListSuggestedTasks(taskId);
        var lines = new List<string>
        {
            $"Replan entry for {taskId}:",
            $"Entry: {entry.EntryId}",
            $"State: {entry.EntryState}",
            $"Trigger: {entry.Trigger}",
            $"Reason: {entry.Reason}",
            $"Run: {entry.RunId ?? "(none)"}",
            $"Failure: {entry.FailureId ?? "(none)"}",
            $"Incident: {entry.IncidentId ?? "(none)"}",
            $"Signal: {entry.PlanningSignalId ?? "(none)"}",
            "Suggested tasks:",
        };

        if (suggestions.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var suggestion in suggestions.Take(5))
            {
                lines.Add($"- {suggestion.SuggestionId}: {suggestion.Title} [{suggestion.Status}] guard={suggestion.GuardVerdict} target={suggestion.Target}");
            }
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult InspectSuggestedTasks(string taskId)
    {
        taskGraphService.GetTask(taskId);
        var service = new PlannerEmergenceService(paths, taskGraphService, executionRunService);
        var suggestions = service.ListSuggestedTasks(taskId);
        var lines = new List<string> { $"Suggested tasks for {taskId}:" };
        if (suggestions.Count == 0)
        {
            lines.Add("- (none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var suggestion in suggestions)
        {
            lines.Add($"- {suggestion.SuggestionId}: {suggestion.Title}");
            lines.Add($"  status={suggestion.Status}; guard={suggestion.GuardVerdict}; reason={suggestion.GuardReason}");
            lines.Add($"  proposed_task_id={suggestion.ProposedTaskId}; target={suggestion.Target}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult InspectExecutionMemory(string taskId)
    {
        taskGraphService.GetTask(taskId);
        var service = new PlannerEmergenceService(paths, taskGraphService, executionRunService);
        var records = service.ListExecutionMemory(taskId, take: 10);
        var lines = new List<string> { $"Execution memory for {taskId}:" };
        if (records.Count == 0)
        {
            lines.Add("- (none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var record in records)
        {
            lines.Add($"- {record.MemoryId}: {record.EventKind} [{record.Status}] run={record.RunId ?? "(none)"}");
            lines.Add($"  {record.Summary}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult ApproveSuggestedTask(string suggestionId, string reason)
    {
        var service = new PlannerEmergenceService(paths, taskGraphService, executionRunService);
        var result = service.ApproveSuggestedTask(suggestionId, reason);
        return result.Allowed
            ? OperatorCommandResult.Success(result.Message)
            : OperatorCommandResult.Failure(result.Message);
    }
}
