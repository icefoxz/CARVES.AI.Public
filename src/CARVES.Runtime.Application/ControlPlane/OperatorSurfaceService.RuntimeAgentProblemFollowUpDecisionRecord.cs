using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentProblemFollowUpDecisionRecord()
    {
        return OperatorSurfaceFormatter.RuntimeAgentProblemFollowUpDecisionRecord(CreateRuntimeAgentProblemFollowUpDecisionRecordService().Build());
    }

    public OperatorCommandResult ApiRuntimeAgentProblemFollowUpDecisionRecord()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeAgentProblemFollowUpDecisionRecordService().Build()));
    }

    public OperatorCommandResult RecordRuntimeAgentProblemFollowUpDecision(AgentProblemFollowUpDecisionRecordRequest request, bool json)
    {
        try
        {
            var record = CreateRuntimeAgentProblemFollowUpDecisionRecordService().Record(request);
            if (json)
            {
                return OperatorCommandResult.Success(operatorApiService.ToJson(record));
            }

            return OperatorCommandResult.Success(
                $"Recorded agent problem follow-up decision {record.DecisionRecordId}.",
                $"Plan: {record.DecisionPlanId}",
                $"Decision: {record.Decision}",
                $"Candidates: {string.Join(", ", record.CandidateIds)}",
                $"Record path: {record.RecordPath}",
                "Next: run carves pilot commit-plan --json and commit the decision record through target git closure.");
        }
        catch (InvalidOperationException exception)
        {
            return OperatorCommandResult.Failure(exception.Message);
        }
    }

    private RuntimeAgentProblemFollowUpDecisionRecordService CreateRuntimeAgentProblemFollowUpDecisionRecordService()
    {
        return new RuntimeAgentProblemFollowUpDecisionRecordService(
            repoRoot,
            () => CreatePilotRuntimeService().ListPilotProblemIntake());
    }
}
