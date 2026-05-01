namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult IngestTaskResult(string taskId)
    {
        var outcome = resultIngestionService.Ingest(taskId);
        var lines = new List<string>
        {
            $"Ingested result envelope for {outcome.TaskId}.",
            $"Result status: {outcome.ResultStatus}",
            $"Task status: {outcome.TaskStatus}",
            $"Already applied: {outcome.AlreadyApplied}",
        };
        if (!string.IsNullOrWhiteSpace(outcome.FailureId))
        {
            lines.Add($"Failure report: {outcome.FailureId}");
        }

        if (outcome.BoundaryStopped)
        {
            lines.Add($"Boundary stop: {outcome.BoundaryReason}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult Failures(string? taskId = null, bool summaryOnly = false)
    {
        var summary = failureSummaryService.Build(taskId);
        var lines = failureSummaryService.RenderLines(summary);
        return new OperatorCommandResult(0, lines);
    }
}
