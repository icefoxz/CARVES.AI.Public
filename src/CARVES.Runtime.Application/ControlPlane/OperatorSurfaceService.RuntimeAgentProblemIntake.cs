using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentProblemIntake()
    {
        return OperatorSurfaceFormatter.RuntimeAgentProblemIntake(CreateRuntimeAgentProblemIntakeService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentProblemIntake()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentProblemIntakeService().Build()));
    }

    public OperatorCommandResult ReportPilotProblem(string jsonPath, bool asJson)
    {
        var service = CreatePilotRuntimeService();
        var record = service.RecordPilotProblemIntake(jsonPath);
        if (asJson)
        {
            return OperatorCommandResult.Success(operatorApiService.ToJson(record));
        }

        return OperatorCommandResult.Success(
            $"Recorded pilot problem {record.ProblemId}.",
            $"Evidence: {record.EvidenceId}",
            $"Kind: {record.ProblemKind}",
            $"Severity: {record.Severity}",
            $"Summary: {record.Summary}");
    }

    public OperatorCommandResult ListPilotProblems(string? repoId = null)
    {
        var service = CreatePilotRuntimeService();
        var records = service.ListPilotProblemIntake(repoId);
        var lines = new List<string> { $"Pilot problem intake records{(string.IsNullOrWhiteSpace(repoId) ? string.Empty : $" for {repoId}")}:" };
        if (records.Count == 0)
        {
            lines.Add("- (none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var record in records.Take(20))
        {
            lines.Add($"- {record.ProblemId}: [{record.ProblemKind}/{record.Severity}] {record.Summary}");
            lines.Add($"  evidence={record.EvidenceId}; stage={record.CurrentStageId}; command={record.BlockedCommand}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult InspectPilotProblem(string problemId)
    {
        var service = CreatePilotRuntimeService();
        var record = service.TryGetPilotProblemIntake(problemId);
        if (record is null)
        {
            return OperatorCommandResult.Failure($"Pilot problem intake record '{problemId}' was not found.");
        }

        var lines = new List<string>
        {
            $"Pilot problem intake: {record.ProblemId}",
            $"Evidence: {record.EvidenceId}",
            $"Repo: {record.RepoId ?? "(none)"}",
            $"Task: {record.TaskId ?? "(none)"}",
            $"Card: {record.CardId ?? "(none)"}",
            $"Stage: {record.CurrentStageId}",
            $"Next governed command: {record.NextGovernedCommand}",
            $"Kind: {record.ProblemKind}",
            $"Severity: {record.Severity}",
            $"Summary: {record.Summary}",
            $"Blocked command: {record.BlockedCommand}",
            $"Command exit code: {(record.CommandExitCode is null ? "(none)" : record.CommandExitCode.Value.ToString())}",
            $"Stop trigger: {record.StopTrigger}",
            $"Recommended follow-up: {record.RecommendedFollowUp}",
            "Affected paths:",
        };
        lines.AddRange(record.AffectedPaths.Count == 0 ? ["- (none)"] : record.AffectedPaths.Select(path => $"- {path}"));
        lines.Add("Observations:");
        lines.AddRange(record.Observations.Count == 0 ? ["- (none)"] : record.Observations.Select(item => $"- {item}"));
        if (!string.IsNullOrWhiteSpace(record.CommandOutput))
        {
            lines.Add("Command output:");
            lines.Add(record.CommandOutput);
        }

        return new OperatorCommandResult(0, lines);
    }

    private RuntimeAgentProblemIntakeService CreateRuntimeAgentProblemIntakeService()
    {
        return new RuntimeAgentProblemIntakeService(
            repoRoot,
            () => CreateRuntimeExternalTargetPilotStartService().BuildStart(),
            () => CreateRuntimeExternalTargetPilotStartService().BuildNext(),
            () => CreatePilotRuntimeService().ListPilotProblemIntake());
    }
}
