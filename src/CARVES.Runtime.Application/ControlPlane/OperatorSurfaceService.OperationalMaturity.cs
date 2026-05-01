using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult StatusSummary()
    {
        return OperatorSurfaceFormatter.OperationalSummary(operationalSummaryService.Build());
    }

    public OperatorCommandResult StatusFull()
    {
        return Status();
    }

    public OperatorCommandResult Cleanup(bool includeRuntimeResidue = true, bool includeEphemeralResidue = true)
    {
        var report = worktreeResourceCleanupService.Cleanup("manual_cleanup", includeRuntimeResidue, includeEphemeralResidue);
        report = new ResourceCleanupReport
        {
            Trigger = report.Trigger,
            ExecutedAt = report.ExecutedAt,
            RemovedWorktreeCount = report.RemovedWorktreeCount,
            RemovedRecordCount = report.RemovedRecordCount,
            RemovedRuntimeResidueCount = report.RemovedRuntimeResidueCount,
            RemovedEphemeralResidueCount = report.RemovedEphemeralResidueCount,
            PreservedActiveWorktreeCount = report.PreservedActiveWorktreeCount,
            Actions = report.Actions,
            Summary = report.Summary,
            SustainabilityAudit = CreateSustainabilityAuditService().Audit(),
        };
        return OperatorSurfaceFormatter.ResourceCleanup(report);
    }

    public OperatorCommandResult DelegationReport(int? hours = null)
    {
        return OperatorSurfaceFormatter.DelegationReport(delegationReportingService.BuildDelegationReport(hours));
    }

    public OperatorCommandResult ApprovalReport(int? hours = null)
    {
        return OperatorSurfaceFormatter.ApprovalReport(delegationReportingService.BuildApprovalReport(hours));
    }
}
