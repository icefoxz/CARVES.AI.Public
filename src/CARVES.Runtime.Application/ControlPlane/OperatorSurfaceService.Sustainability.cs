using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectSustainability()
    {
        var catalog = CreateRuntimeArtifactCatalogService().LoadOrBuild();
        return OperatorSurfaceFormatter.RuntimeArtifactCatalog(catalog);
    }

    public OperatorCommandResult AuditSustainability()
    {
        var report = CreateSustainabilityAuditService().Audit();
        return OperatorSurfaceFormatter.SustainabilityAudit(report);
    }

    public OperatorCommandResult InspectHistoryCompaction()
    {
        var report = CreateOperationalHistoryCompactionService().TryLoadLatest();
        return report is null
            ? new OperatorCommandResult(0, ["Operational history compaction", "No compaction report has been recorded yet."])
            : OperatorSurfaceFormatter.OperationalHistoryCompaction(report);
    }

    public OperatorCommandResult InspectArchiveReadiness()
    {
        var report = CreateOperationalHistoryArchiveReadinessService().Build();
        return OperatorSurfaceFormatter.OperationalHistoryArchiveReadiness(report);
    }

    public OperatorCommandResult InspectArchiveFollowUp()
    {
        var queue = CreateOperationalHistoryArchiveFollowUpQueueService().Build();
        return OperatorSurfaceFormatter.OperationalHistoryArchiveFollowUp(queue);
    }

    public OperatorCommandResult InspectExecutionRunExceptions()
    {
        var report = CreateExecutionRunHistoricalExceptionService().Build();
        return OperatorSurfaceFormatter.ExecutionRunHistoricalExceptions(report);
    }

    private RuntimeArtifactCatalogService CreateRuntimeArtifactCatalogService()
    {
        return new RuntimeArtifactCatalogService(repoRoot, paths, systemConfig);
    }

    private SustainabilityAuditService CreateSustainabilityAuditService()
    {
        return new SustainabilityAuditService(repoRoot, paths, systemConfig, codeGraphQueryService);
    }

    private OperationalHistoryCompactionService CreateOperationalHistoryCompactionService()
    {
        return new OperationalHistoryCompactionService(repoRoot, paths, systemConfig);
    }

    private OperationalHistoryArchiveReadinessService CreateOperationalHistoryArchiveReadinessService()
    {
        return new OperationalHistoryArchiveReadinessService(repoRoot, paths, systemConfig);
    }

    private OperationalHistoryArchiveFollowUpQueueService CreateOperationalHistoryArchiveFollowUpQueueService()
    {
        return new OperationalHistoryArchiveFollowUpQueueService(repoRoot, paths, systemConfig);
    }

    private ExecutionRunHistoricalExceptionService CreateExecutionRunHistoricalExceptionService()
    {
        return new ExecutionRunHistoricalExceptionService(repoRoot, paths, taskGraphService, executionRunService, artifactRepository);
    }
}
