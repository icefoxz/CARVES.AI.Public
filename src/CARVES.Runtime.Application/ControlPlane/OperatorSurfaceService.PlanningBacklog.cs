using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult RunNext(bool dryRun)
    {
        var result = devLoopService.RunOnce(dryRun);
        return OperatorSurfaceFormatter.RunNext(result);
    }

    public OperatorCommandResult SyncState()
    {
        var graph = taskGraphService.Load();
        var projectionHealthService = new MarkdownProjectionHealthService(paths);
        var reviewPendingBefore = devLoopService.GetSession()?.ReviewPendingTaskIds.Count ?? 0;
        var reconciledSession = devLoopService.ReconcileReviewBoundary();
        var reconciledTaskIds = ReconcileExecutionRunTruth(graph);
        if (reconciledTaskIds.Count > 0)
        {
            graph = taskGraphService.Load();
        }

        var ghostBlockerTaskIds = new HistoricalGhostBlockedTaskReconciliationService(taskGraphService, artifactRepository).Reconcile();
        if (ghostBlockerTaskIds.Count > 0)
        {
            graph = taskGraphService.Load();
        }

        markdownSyncService.Sync(graph, session: devLoopService.GetSession());
        var projectionHealth = projectionHealthService.Load();
        var lines = new List<string>
        {
            $"Synchronized task graph and markdown views for {graph.Tasks.Count} tasks.",
        };
        if (!string.Equals(projectionHealth.State, "healthy", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"Projection writeback: {projectionHealth.State} ({projectionHealth.Summary})");
        }

        if (reviewPendingBefore > 0 && reconciledSession is not null && reconciledSession.ReviewPendingTaskIds.Count == 0)
        {
            lines.Add($"Reconciled review boundary state for session {reconciledSession.SessionId}.");
        }

        if (reconciledTaskIds.Count > 0)
        {
            lines.Add($"Reconciled execution run truth for {reconciledTaskIds.Count} task(s): {string.Join(", ", reconciledTaskIds)}");
        }

        if (ghostBlockerTaskIds.Count > 0)
        {
            lines.Add($"Superseded {ghostBlockerTaskIds.Count} historical ghost blocker task(s): {string.Join(", ", ghostBlockerTaskIds)}");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }

    public OperatorCommandResult ScanCode()
    {
        var result = codeGraphBuilder.Build();
        return OperatorSurfaceFormatter.ScanCode(result);
    }

    public OperatorCommandResult SafetyCheck()
    {
        var carvesCodeStandard = configRepository.LoadCarvesCodeStandard();
        var rules = configRepository.LoadSafetyRules();
        var violations = safetyService.DescribeBaseline(rules);
        return OperatorSurfaceFormatter.SafetyCheck(carvesCodeStandard, rules, violations);
    }

    public OperatorCommandResult DetectRefactors()
    {
        var backlog = refactoringService.DetectAndStore();
        return OperatorSurfaceFormatter.DetectRefactors(backlog);
    }

    public OperatorCommandResult MaterializeRefactors()
    {
        var result = refactoringService.MaterializeSuggestedTasks();
        return OperatorSurfaceFormatter.MaterializeRefactors(result);
    }

    public OperatorCommandResult DetectOpportunities()
    {
        var result = opportunityDetectorService.DetectAndStore();
        return OperatorSurfaceFormatter.DetectOpportunities(result);
    }

    public OperatorCommandResult ShowOpportunities()
    {
        return OperatorTaskFormatter.ShowOpportunities(opportunityDetectorService.LoadSnapshot());
    }

    public OperatorCommandResult ShowGraph()
    {
        var graph = taskGraphService.Load();
        return OperatorTaskFormatter.ShowGraph(
            graph,
            codeGraphQueryService.LoadManifest(),
            codeGraphQueryService.LoadModuleSummaries());
    }

    public OperatorCommandResult ShowBacklog()
    {
        var backlog = refactoringService.LoadBacklog();
        return OperatorTaskFormatter.ShowBacklog(backlog);
    }

    public OperatorCommandResult ExplainTask(string taskId)
    {
        var task = taskGraphService.GetTask(taskId);
        var scopeAnalysis = codeGraphQueryService.AnalyzeScope(task.Scope);
        var impactAnalysis = codeGraphQueryService.AnalyzeImpact(task.Scope);
        var workerExecutionArtifact = artifactRepository.TryLoadWorkerExecutionArtifact(taskId);
        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(taskId);
        var reviewEvidenceProjection = new ReviewEvidenceProjectionService(reviewEvidenceGateService, reviewWritebackService)
            .Build(task, reviewArtifact, workerExecutionArtifact);
        return OperatorTaskFormatter.ExplainTask(
            task,
            runtimePolicyBundleService.LoadRoleGovernancePolicy(),
            scopeAnalysis,
            impactAnalysis,
            workerExecutionArtifact,
            artifactRepository.TryLoadWorkerPermissionArtifact(taskId),
            artifactRepository.TryLoadProviderArtifact(taskId),
            reviewArtifact,
            reviewEvidenceProjection);
    }

    public OperatorCommandResult Help()
    {
        return new OperatorCommandResult(0, OperatorTaskFormatter.HelpLines());
    }
}
