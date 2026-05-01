using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeTargetIgnoreDecisionRecord()
    {
        return OperatorSurfaceFormatter.RuntimeTargetIgnoreDecisionRecord(CreateRuntimeTargetIgnoreDecisionRecordService().Build());
    }

    public OperatorCommandResult ApiRuntimeTargetIgnoreDecisionRecord()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeTargetIgnoreDecisionRecordService().Build()));
    }

    public OperatorCommandResult RecordRuntimeTargetIgnoreDecision(TargetIgnoreDecisionRecordRequest request, bool json)
    {
        try
        {
            var record = CreateRuntimeTargetIgnoreDecisionRecordService().Record(request);
            if (json)
            {
                return OperatorCommandResult.Success(operatorApiService.ToJson(record));
            }

            return OperatorCommandResult.Success(
                $"Recorded target ignore decision {record.DecisionRecordId}.",
                $"Plan: {record.IgnoreDecisionPlanId}",
                $"Decision: {record.Decision}",
                $"Entries: {string.Join(", ", record.Entries)}",
                $"Record path: {record.RecordPath}",
                "Next: run carves pilot commit-plan --json and commit the decision record through target git closure.");
        }
        catch (InvalidOperationException exception)
        {
            return OperatorCommandResult.Failure(exception.Message);
        }
    }

    private RuntimeTargetIgnoreDecisionRecordService CreateRuntimeTargetIgnoreDecisionRecordService()
    {
        return new RuntimeTargetIgnoreDecisionRecordService(repoRoot);
    }
}
