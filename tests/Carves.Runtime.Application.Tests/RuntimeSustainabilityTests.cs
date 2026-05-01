using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeSustainabilityTests
{
    [Fact]
    public void RuntimeArtifactCatalog_EncodesRetentionAndReadVisibilityContracts()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        var service = new RuntimeArtifactCatalogService(workspace.RootPath, workspace.Paths, config);

        var catalog = service.LoadOrBuild();

        var taskTruth = Assert.Single(catalog.Families, family => family.FamilyId == "task_truth");
        Assert.Equal(RuntimeArtifactClass.CanonicalTruth, taskTruth.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.Permanent, taskTruth.RetentionMode);
        Assert.Equal(RuntimeArtifactReadVisibility.Summary, taskTruth.DefaultReadVisibility);
        Assert.False(taskTruth.CompactEligible);

        var executionMemoryTruth = Assert.Single(catalog.Families, family => family.FamilyId == "execution_memory_truth");
        Assert.Equal(RuntimeArtifactClass.CanonicalTruth, executionMemoryTruth.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.Permanent, executionMemoryTruth.RetentionMode);
        Assert.Equal(RuntimeArtifactReadVisibility.OnDemandDetail, executionMemoryTruth.DefaultReadVisibility);

        var governedMirror = Assert.Single(catalog.Families, family => family.FamilyId == "governed_markdown_mirror");
        Assert.Equal(RuntimeArtifactClass.GovernedMirror, governedMirror.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.SingleVersion, governedMirror.RetentionMode);
        Assert.Equal(RuntimeArtifactReadVisibility.Summary, governedMirror.DefaultReadVisibility);

        var runtimePackAdmission = Assert.Single(catalog.Families, family => family.FamilyId == "runtime_pack_admission_evidence");
        Assert.Equal(RuntimeArtifactClass.GovernedMirror, runtimePackAdmission.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.SingleVersion, runtimePackAdmission.RetentionMode);
        Assert.Equal(RuntimeArtifactReadVisibility.Summary, runtimePackAdmission.DefaultReadVisibility);

        var runtimePackSelection = Assert.Single(catalog.Families, family => family.FamilyId == "runtime_pack_selection_evidence");
        Assert.Equal(RuntimeArtifactClass.GovernedMirror, runtimePackSelection.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.RollingWindow, runtimePackSelection.RetentionMode);
        Assert.Equal(RuntimeArtifactReadVisibility.Summary, runtimePackSelection.DefaultReadVisibility);
        Assert.True(runtimePackSelection.CompactEligible);
        Assert.Equal(20, runtimePackSelection.Budget.HotWindowCount);

        var runtimePackSelectionAudit = Assert.Single(catalog.Families, family => family.FamilyId == "runtime_pack_selection_audit_evidence");
        Assert.Equal(RuntimeArtifactClass.OperationalHistory, runtimePackSelectionAudit.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.RollingWindow, runtimePackSelectionAudit.RetentionMode);
        Assert.Equal(RuntimeArtifactReadVisibility.Summary, runtimePackSelectionAudit.DefaultReadVisibility);
        Assert.True(runtimePackSelectionAudit.CompactEligible);
        Assert.Equal(20, runtimePackSelectionAudit.Budget.HotWindowCount);

        var validationTraceHistory = Assert.Single(catalog.Families, family => family.FamilyId == "validation_trace_history");
        Assert.Equal(RuntimeArtifactClass.OperationalHistory, validationTraceHistory.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.RollingWindow, validationTraceHistory.RetentionMode);
        Assert.Equal(RuntimeArtifactReadVisibility.OnDemandDetail, validationTraceHistory.DefaultReadVisibility);
        Assert.True(validationTraceHistory.CompactEligible);

        var workerArtifactHistory = Assert.Single(catalog.Families, family => family.FamilyId == "worker_execution_artifact_history");
        Assert.Equal(RuntimeArtifactClass.OperationalHistory, workerArtifactHistory.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.RollingWindow, workerArtifactHistory.RetentionMode);
        Assert.Equal(RuntimeArtifactReadVisibility.OnDemandDetail, workerArtifactHistory.DefaultReadVisibility);
        Assert.True(workerArtifactHistory.CompactEligible);
        Assert.Equal(20, workerArtifactHistory.Budget.HotWindowCount);

        var codeGraphSummary = Assert.Single(catalog.Families, family => family.FamilyId == "codegraph_derived");
        Assert.Equal(RuntimeArtifactReadVisibility.Summary, codeGraphSummary.DefaultReadVisibility);
        Assert.DoesNotContain(codeGraphSummary.Roots, root => root.EndsWith("/graph.json", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(catalog.Families, family => family.FamilyId == "codegraph_detail_derived");

        var executionSurfaceHistory = Assert.Single(catalog.Families, family => family.FamilyId == "execution_surface_history");
        Assert.Equal(RuntimeArtifactClass.OperationalHistory, executionSurfaceHistory.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.RollingWindow, executionSurfaceHistory.RetentionMode);
        Assert.Equal(RuntimeArtifactReadVisibility.OnDemandDetail, executionSurfaceHistory.DefaultReadVisibility);
        Assert.True(executionSurfaceHistory.CompactEligible);
        Assert.Equal(60, executionSurfaceHistory.Budget.HotWindowCount);

        var planningRuntimeHistory = Assert.Single(catalog.Families, family => family.FamilyId == "planning_runtime_history");
        Assert.Equal(RuntimeArtifactClass.OperationalHistory, planningRuntimeHistory.ArtifactClass);
        Assert.True(planningRuntimeHistory.CompactEligible);
        Assert.Equal(80, planningRuntimeHistory.Budget.HotWindowCount);

        var planningDraftResidue = Assert.Single(catalog.Families, family => family.FamilyId == "planning_draft_residue");
        Assert.Equal(RuntimeArtifactClass.EphemeralResidue, planningDraftResidue.ArtifactClass);
        Assert.True(planningDraftResidue.CleanupEligible);
        Assert.Equal(RuntimeArtifactReadVisibility.Hidden, planningDraftResidue.DefaultReadVisibility);

        var executionRunReportHistory = Assert.Single(catalog.Families, family => family.FamilyId == "execution_run_report_history");
        Assert.Equal(RuntimeArtifactClass.OperationalHistory, executionRunReportHistory.ArtifactClass);
        Assert.Equal(RuntimeArtifactReadVisibility.Summary, executionRunReportHistory.DefaultReadVisibility);
        Assert.True(executionRunReportHistory.CompactEligible);
        Assert.Equal(30, executionRunReportHistory.Budget.HotWindowCount);

        var runtimeFailureHistory = Assert.Single(catalog.Families, family => family.FamilyId == "runtime_failure_detail_history");
        Assert.Equal(RuntimeArtifactClass.OperationalHistory, runtimeFailureHistory.ArtifactClass);
        Assert.True(runtimeFailureHistory.CompactEligible);
        Assert.Equal(40, runtimeFailureHistory.Budget.HotWindowCount);

        var contextPackProjection = Assert.Single(catalog.Families, family => family.FamilyId == "context_pack_projection");
        Assert.Equal(RuntimeArtifactClass.DerivedTruth, contextPackProjection.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.RollingWindow, contextPackProjection.RetentionMode);
        Assert.True(contextPackProjection.CompactEligible);
        Assert.Equal(20, contextPackProjection.Budget.HotWindowCount);

        var executionPacketMirror = Assert.Single(catalog.Families, family => family.FamilyId == "execution_packet_mirror");
        Assert.Equal(RuntimeArtifactClass.GovernedMirror, executionPacketMirror.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.RollingWindow, executionPacketMirror.RetentionMode);
        Assert.True(executionPacketMirror.CompactEligible);
        Assert.Equal(12, executionPacketMirror.Budget.HotWindowCount);

        var runtimeLiveState = Assert.Single(catalog.Families, family => family.FamilyId == "runtime_live_state");
        Assert.Equal(RuntimeArtifactClass.LiveState, runtimeLiveState.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.SingleVersion, runtimeLiveState.RetentionMode);
        Assert.Equal(RuntimeArtifactReadVisibility.OnDemandDetail, runtimeLiveState.DefaultReadVisibility);

        var platformProviderDefinition = Assert.Single(catalog.Families, family => family.FamilyId == "platform_provider_definition_truth");
        Assert.Equal(RuntimeArtifactClass.CanonicalTruth, platformProviderDefinition.ArtifactClass);
        Assert.Equal(RuntimeArtifactReadVisibility.Summary, platformProviderDefinition.DefaultReadVisibility);

        var platformProviderLiveState = Assert.Single(catalog.Families, family => family.FamilyId == "platform_provider_live_state");
        Assert.Equal(RuntimeArtifactClass.LiveState, platformProviderLiveState.ArtifactClass);
        Assert.Equal(RuntimeArtifactReadVisibility.OnDemandDetail, platformProviderLiveState.DefaultReadVisibility);

        var ephemeralResidue = Assert.Single(catalog.Families, family => family.FamilyId == "ephemeral_runtime_residue");
        Assert.Equal(RuntimeArtifactClass.EphemeralResidue, ephemeralResidue.ArtifactClass);
        Assert.Equal(RuntimeArtifactRetentionMode.AutoExpire, ephemeralResidue.RetentionMode);
        Assert.Equal(RuntimeArtifactReadVisibility.Hidden, ephemeralResidue.DefaultReadVisibility);
        Assert.True(ephemeralResidue.CleanupEligible);
        Assert.Contains(ephemeralResidue.Roots, root => root.StartsWith(".carves-platform/runtime-state", StringComparison.OrdinalIgnoreCase));

        Assert.True(File.Exists(RuntimeArtifactCatalogService.GetCatalogPath(workspace.Paths)));
    }

    [Fact]
    public void SustainabilityAudit_SplitsTopLevelPlanningDraftResidueFromTrackedPlanningHistory()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        var cardDraftResidue = Path.Combine(workspace.Paths.PlanningCardDraftsRoot, "CARD-351.json");
        var taskgraphDraftResidue = Path.Combine(workspace.Paths.PlanningTaskGraphDraftsRoot, "TG-CARD-351-001.json");
        var trackedPlanningHistory = Path.Combine(workspace.Paths.PlanningTaskGraphDraftsRoot, "compact-history", "TG-000.json");

        Directory.CreateDirectory(Path.GetDirectoryName(cardDraftResidue)!);
        Directory.CreateDirectory(Path.GetDirectoryName(taskgraphDraftResidue)!);
        Directory.CreateDirectory(Path.GetDirectoryName(trackedPlanningHistory)!);
        File.WriteAllText(cardDraftResidue, """{"card_id":"CARD-351"}""");
        File.WriteAllText(taskgraphDraftResidue, """{"draft_id":"TG-CARD-351-001"}""");
        File.WriteAllText(trackedPlanningHistory, """{"draft_id":"TG-000"}""");

        var auditService = new SustainabilityAuditService(workspace.RootPath, workspace.Paths, config, new StubCodeGraphQueryService());
        var report = auditService.Audit();

        var planningHistory = Assert.Single(report.Families, family => family.FamilyId == "planning_runtime_history");
        var planningResidue = Assert.Single(report.Families, family => family.FamilyId == "planning_draft_residue");

        Assert.Equal(1, planningHistory.FileCount);
        Assert.Equal(2, planningResidue.FileCount);
        Assert.Equal(RuntimeMaintenanceActionKind.PruneEphemeral, planningResidue.RecommendedAction);
    }

    [Fact]
    public void SustainabilityAudit_FlagsCanonicalPollutionAndOperationalHistoryPressure()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        Directory.CreateDirectory(Path.Combine(workspace.Paths.TasksRoot, "nodes"));
        Directory.CreateDirectory(Path.Combine(workspace.Paths.AiRoot, "validation", "traces"));
        Directory.CreateDirectory(Path.Combine(workspace.Paths.AiRoot, "codegraph", "obj"));

        File.WriteAllText(Path.Combine(workspace.Paths.TasksRoot, "raw.log"), "polluted canonical truth");
        File.WriteAllText(Path.Combine(workspace.Paths.AiRoot, "codegraph", "obj", "project.assets.json"), "{}");

        for (var index = 0; index < 31; index++)
        {
            var tracePath = Path.Combine(workspace.Paths.AiRoot, "validation", "traces", $"trace-{index:00}.json");
            File.WriteAllText(tracePath, $$"""{"trace_id":"trace-{{index:00}}"}""");
            File.SetLastWriteTimeUtc(tracePath, DateTime.UtcNow.AddDays(-(index + 1)));
        }

        var auditService = new SustainabilityAuditService(workspace.RootPath, workspace.Paths, config, new StubCodeGraphQueryService());
        var report = auditService.Audit();

        Assert.False(report.StrictPassed);
        Assert.Contains(report.Findings, finding => finding.Category == "canonical_truth_pollution" && finding.FamilyId == "task_truth");
        Assert.Contains(report.Findings, finding => finding.Category == "derived_truth_pollution" && finding.FamilyId == "codegraph_derived");
        Assert.Contains(report.Findings, finding => finding.Category == "retention_drift" && finding.FamilyId == "validation_trace_history");

        var traceProjection = Assert.Single(report.Families, family => family.FamilyId == "validation_trace_history");
        Assert.Equal(RuntimeMaintenanceActionKind.CompactHistory, traceProjection.RecommendedAction);
        Assert.False(traceProjection.WithinBudget);

        var taskFinding = Assert.Single(report.Findings, finding => finding.FamilyId == "task_truth" && finding.Category == "canonical_truth_pollution");
        Assert.Equal(RuntimeMaintenanceActionKind.None, taskFinding.RecommendedAction);

        var auditPath = SustainabilityAuditService.GetAuditPath(workspace.Paths);
        Assert.True(File.Exists(auditPath));
        var persisted = JsonNode.Parse(File.ReadAllText(auditPath))!.AsObject();
        Assert.False(persisted["strict_passed"]!.GetValue<bool>());
    }

    [Fact]
    public void SustainabilityAudit_ProjectsGovernedMirrorAndLiveStateWithoutTreatingThemAsCompactionFamilies()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        Directory.CreateDirectory(workspace.Paths.AiRoot);
        Directory.CreateDirectory(workspace.Paths.RuntimeLiveStateRoot);
        Directory.CreateDirectory(workspace.Paths.PlatformProvidersRoot);
        Directory.CreateDirectory(workspace.Paths.PlatformProviderLiveStateRoot);
        Directory.CreateDirectory(Path.Combine(workspace.Paths.PlatformRuntimeStateRoot, "control-plane-locks"));

        File.WriteAllText(Path.Combine(workspace.Paths.AiRoot, "STATE.md"), "# STATE");
        File.WriteAllText(workspace.Paths.RuntimeSessionFile, """{"status":"idle"}""");
        File.WriteAllText(Path.Combine(workspace.Paths.PlatformProvidersRoot, "codex.json"), """{"provider_id":"codex","worker_backends":[{"backend_id":"codex_cli"}]}""");
        File.WriteAllText(workspace.Paths.PlatformProviderHealthFile, """{"entries":[{"backend_id":"codex_cli","state":"healthy","summary":"ok"}]}""");
        File.WriteAllText(Path.Combine(workspace.Paths.PlatformRuntimeStateRoot, "control-plane-locks", "authoritative-truth-writer.json"), """{"owner":"host"}""");

        var auditService = new SustainabilityAuditService(workspace.RootPath, workspace.Paths, config, new StubCodeGraphQueryService());
        var report = auditService.Audit();

        var mirrorProjection = Assert.Single(report.Families, family => family.FamilyId == "governed_markdown_mirror");
        Assert.Equal(RuntimeArtifactClass.GovernedMirror, mirrorProjection.ArtifactClass);
        Assert.Equal(RuntimeMaintenanceActionKind.None, mirrorProjection.RecommendedAction);

        var runtimeLiveProjection = Assert.Single(report.Families, family => family.FamilyId == "runtime_live_state");
        Assert.Equal(RuntimeArtifactClass.LiveState, runtimeLiveProjection.ArtifactClass);
        Assert.Equal(RuntimeMaintenanceActionKind.None, runtimeLiveProjection.RecommendedAction);
        Assert.Equal(0, runtimeLiveProjection.ReadPathPressureCount);

        var providerDefinitionProjection = Assert.Single(report.Families, family => family.FamilyId == "platform_provider_definition_truth");
        Assert.Equal(RuntimeArtifactClass.CanonicalTruth, providerDefinitionProjection.ArtifactClass);
        Assert.Equal(RuntimeMaintenanceActionKind.None, providerDefinitionProjection.RecommendedAction);

        var providerLiveStateProjection = Assert.Single(report.Families, family => family.FamilyId == "platform_provider_live_state");
        Assert.Equal(RuntimeArtifactClass.LiveState, providerLiveStateProjection.ArtifactClass);
        Assert.Equal(RuntimeMaintenanceActionKind.None, providerLiveStateProjection.RecommendedAction);

        var platformLiveProjection = Assert.Single(report.Families, family => family.FamilyId == "platform_live_state");
        Assert.Equal(RuntimeArtifactClass.LiveState, platformLiveProjection.ArtifactClass);
        Assert.Equal(RuntimeMaintenanceActionKind.None, platformLiveProjection.RecommendedAction);
        Assert.DoesNotContain(report.Findings, finding => finding.FamilyId == "runtime_live_state" && finding.Category == "read_path_pressure");
    }

    [Fact]
    public void SustainabilityAudit_TreatsPlatformRuntimeStateTmpFilesAsEphemeralResidue()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        var runtimeInstancesPath = workspace.Paths.PlatformRuntimeInstancesLiveStateFile;
        var tempSpillPath = $"{runtimeInstancesPath}.{Guid.NewGuid():N}.tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(runtimeInstancesPath)!);
        File.WriteAllText(runtimeInstancesPath, """{"instances":[]}""");
        File.WriteAllText(tempSpillPath, """{"instances":[{"id":"tmp"}]}""");

        var auditService = new SustainabilityAuditService(workspace.RootPath, workspace.Paths, config, new StubCodeGraphQueryService());
        var report = auditService.Audit();

        var platformLiveProjection = Assert.Single(report.Families, family => family.FamilyId == "platform_live_state");
        var ephemeralProjection = Assert.Single(report.Families, family => family.FamilyId == "ephemeral_runtime_residue");

        Assert.Equal(1, platformLiveProjection.FileCount);
        Assert.Equal(1, ephemeralProjection.FileCount);
        Assert.Equal(RuntimeMaintenanceActionKind.PruneEphemeral, ephemeralProjection.RecommendedAction);
        Assert.DoesNotContain(report.Findings, finding => finding.FamilyId == "platform_live_state" && finding.Category == "growth_budget_exceeded");
    }

    [Fact]
    public void SustainabilityAudit_UsesCompactHistoryForCompactEligibleDerivedAndMirrorFamilies()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        var contextPackRoot = Path.Combine(workspace.Paths.RuntimeRoot, "context-packs", "tasks");
        var executionPacketRoot = Path.Combine(workspace.Paths.RuntimeRoot, "execution-packets");
        Directory.CreateDirectory(contextPackRoot);
        Directory.CreateDirectory(executionPacketRoot);

        for (var index = 0; index < 121; index++)
        {
            var contextPackPath = Path.Combine(contextPackRoot, $"T-CTX-{index:000}.json");
            var packetPath = Path.Combine(executionPacketRoot, $"T-PACKET-{index:000}.json");
            File.WriteAllText(contextPackPath, $$"""{"task_id":"T-CTX-{{index:000}}"}""");
            File.WriteAllText(packetPath, $$"""{"task_id":"T-PACKET-{{index:000}}"}""");
            var timestamp = DateTime.UtcNow.AddMinutes(-index);
            File.SetLastWriteTimeUtc(contextPackPath, timestamp);
            File.SetLastWriteTimeUtc(packetPath, timestamp);
        }

        var auditService = new SustainabilityAuditService(workspace.RootPath, workspace.Paths, config, new StubCodeGraphQueryService());
        var report = auditService.Audit();

        var contextPackProjection = Assert.Single(report.Families, family => family.FamilyId == "context_pack_projection");
        Assert.Equal(RuntimeArtifactRetentionMode.RollingWindow, contextPackProjection.RetentionMode);
        Assert.Equal("rolling_window_hot_and_age_bound", contextPackProjection.RetentionDiscipline);
        Assert.Equal("compact_history", contextPackProjection.ClosureDiscipline);
        Assert.Equal("archive_ready_after_hot_window_or_age_window", contextPackProjection.ArchiveReadinessState);
        Assert.Equal(RuntimeMaintenanceActionKind.CompactHistory, contextPackProjection.RecommendedAction);
        Assert.False(contextPackProjection.WithinBudget);

        var executionPacketMirror = Assert.Single(report.Families, family => family.FamilyId == "execution_packet_mirror");
        Assert.Equal("compact_history", executionPacketMirror.ClosureDiscipline);
        Assert.Equal("archive_ready_after_hot_window_or_age_window", executionPacketMirror.ArchiveReadinessState);
        Assert.Equal(RuntimeMaintenanceActionKind.CompactHistory, executionPacketMirror.RecommendedAction);
        Assert.False(executionPacketMirror.WithinBudget);
    }

    [Fact]
    public void OperationalHistoryCompaction_ArchivesCurrentHighGrowthFamilies()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        var validationTraceRoot = Path.Combine(workspace.Paths.AiRoot, "validation", "traces");
        var runRoot = Path.Combine(workspace.Paths.RuntimeRoot, "runs", "T-CARD-290-TEST");
        var runReportRoot = Path.Combine(workspace.Paths.RuntimeRoot, "run-reports", "T-CARD-317-TEST");
        var planningTaskgraphRoot = Path.Combine(workspace.Paths.PlanningTaskGraphDraftsRoot, "archive-test");
        var executionRoot = Path.Combine(workspace.Paths.AiRoot, "execution", "T-CARD-317-TEST");
        var contextPackRoot = Path.Combine(workspace.Paths.RuntimeRoot, "context-packs", "tasks");
        var executionPacketRoot = Path.Combine(workspace.Paths.RuntimeRoot, "execution-packets");
        var failureRoot = workspace.Paths.FailuresRoot;
        var runtimeFailureArtifactRoot = workspace.Paths.RuntimeFailureArtifactsRoot;
        var taskTruthPath = Path.Combine(workspace.Paths.TasksRoot, "graph.json");

        Directory.CreateDirectory(validationTraceRoot);
        Directory.CreateDirectory(runRoot);
        Directory.CreateDirectory(runReportRoot);
        Directory.CreateDirectory(planningTaskgraphRoot);
        Directory.CreateDirectory(executionRoot);
        Directory.CreateDirectory(contextPackRoot);
        Directory.CreateDirectory(executionPacketRoot);
        Directory.CreateDirectory(failureRoot);
        Directory.CreateDirectory(runtimeFailureArtifactRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(taskTruthPath)!);
        File.WriteAllText(taskTruthPath, """{"schema_version":1}""");

        for (var index = 0; index < 31; index++)
        {
            var tracePath = Path.Combine(validationTraceRoot, $"trace-{index:00}.json");
            File.WriteAllText(tracePath, $$"""{"trace_id":"trace-{{index:00}}"}""");
            File.SetLastWriteTimeUtc(tracePath, DateTime.UtcNow.AddMinutes(-index));
        }

        for (var index = 0; index < 7; index++)
        {
            var runPath = Path.Combine(runRoot, $"run-{index:00}.json");
            File.WriteAllText(runPath, $$"""{"run_id":"run-{{index:00}}"}""");
            File.SetLastWriteTimeUtc(runPath, DateTime.UtcNow.AddMinutes(-(index + 100)));
        }

        for (var index = 0; index < 91; index++)
        {
            var reportPath = Path.Combine(runReportRoot, $"run-report-{index:00}.json");
            File.WriteAllText(reportPath, $$"""{"run_id":"report-{{index:00}}"}""");
            File.SetLastWriteTimeUtc(reportPath, DateTime.UtcNow.AddMinutes(-(index + 200)));
        }

        for (var index = 0; index < 301; index++)
        {
            var taskgraphPath = Path.Combine(planningTaskgraphRoot, $"TG-{index:000}.json");
            var executionPath = Path.Combine(executionRoot, $"execution-{index:000}.json");
            File.WriteAllText(taskgraphPath, $$"""{"draft_id":"TG-{{index:000}}"}""");
            File.WriteAllText(executionPath, $$"""{"task_id":"T-CARD-317-TEST","entry_id":"{{index:000}}"}""");
            var timestamp = DateTime.UtcNow.AddMinutes(-(index + 300));
            File.SetLastWriteTimeUtc(taskgraphPath, timestamp);
            File.SetLastWriteTimeUtc(executionPath, timestamp);
        }

        for (var index = 0; index < 121; index++)
        {
            var contextPackPath = Path.Combine(contextPackRoot, $"T-CTX-{index:000}.json");
            var executionPacketPath = Path.Combine(executionPacketRoot, $"T-PACKET-{index:000}.json");
            var failurePath = Path.Combine(failureRoot, $"FAIL-{index:000}.json");
            var runtimeFailurePath = Path.Combine(runtimeFailureArtifactRoot, $"FAIL-{index:000}.json");
            File.WriteAllText(contextPackPath, $$"""{"task_id":"T-CTX-{{index:000}}"}""");
            File.WriteAllText(executionPacketPath, $$"""{"task_id":"T-PACKET-{{index:000}}"}""");
            File.WriteAllText(failurePath, $$"""{"failure_id":"FAIL-{{index:000}}"}""");
            File.WriteAllText(runtimeFailurePath, $$"""{"failure_id":"FAIL-{{index:000}}","kind":"runtime"}""");
            var timestamp = DateTime.UtcNow.AddMinutes(-(index + 700));
            File.SetLastWriteTimeUtc(contextPackPath, timestamp);
            File.SetLastWriteTimeUtc(executionPacketPath, timestamp);
            File.SetLastWriteTimeUtc(failurePath, timestamp);
            File.SetLastWriteTimeUtc(runtimeFailurePath, timestamp);
        }

        var service = new OperationalHistoryCompactionService(workspace.RootPath, workspace.Paths, config);
        var report = service.Compact();

        Assert.Contains(report.Families, family => family.FamilyId == "validation_trace_history");
        Assert.Contains(report.Families, family => family.FamilyId == "execution_run_detail_history");
        Assert.Contains(report.Families, family => family.FamilyId == "execution_run_report_history");
        Assert.Contains(report.Families, family => family.FamilyId == "planning_runtime_history");
        Assert.Contains(report.Families, family => family.FamilyId == "execution_surface_history");
        Assert.Contains(report.Families, family => family.FamilyId == "context_pack_projection");
        Assert.Contains(report.Families, family => family.FamilyId == "execution_packet_mirror");
        Assert.Contains(report.Families, family => family.FamilyId == "runtime_failure_detail_history");
        Assert.Contains(report.Families, family => family.FamilyId == "worker_execution_artifact_history");
        Assert.True(File.Exists(taskTruthPath));
        Assert.Equal(15, Directory.EnumerateFiles(validationTraceRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(3, Directory.EnumerateFiles(runRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(30, Directory.EnumerateFiles(runReportRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(80, Directory.EnumerateFiles(planningTaskgraphRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(60, Directory.EnumerateFiles(executionRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(20, Directory.EnumerateFiles(contextPackRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(12, Directory.EnumerateFiles(executionPacketRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(20, Directory.EnumerateFiles(failureRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(20, Directory.EnumerateFiles(runtimeFailureArtifactRoot, "*.json", SearchOption.TopDirectoryOnly).Count());

        var archiveIndexPath = OperationalHistoryCompactionService.GetArchiveIndexPath(workspace.Paths);
        Assert.True(File.Exists(archiveIndexPath));
        var archiveIndex = JsonSerializer.Deserialize<OperationalHistoryArchiveIndex>(File.ReadAllText(archiveIndexPath), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        });
        Assert.NotNull(archiveIndex);
        Assert.True(archiveIndex!.Entries.Length >= 130);
        Assert.All(archiveIndex.Entries, entry => Assert.False(entry.OriginalPath.StartsWith(".ai/tasks/", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(archiveIndex.Entries, entry => entry.FamilyId == "context_pack_projection" && entry.OriginalPath.Contains("/context-packs/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(archiveIndex.Entries, entry => entry.FamilyId == "execution_packet_mirror" && entry.OriginalPath.Contains("/execution-packets/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(archiveIndex.Entries, entry => entry.FamilyId == "runtime_failure_detail_history" && entry.OriginalPath.Contains("/failures/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OperationalHistoryCompaction_UsesAgeWindowForValidationTraceHistory()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        var validationTraceRoot = Path.Combine(workspace.Paths.AiRoot, "validation", "traces");
        Directory.CreateDirectory(validationTraceRoot);

        for (var index = 0; index < 15; index++)
        {
            var tracePath = Path.Combine(validationTraceRoot, $"trace-{index:00}.json");
            File.WriteAllText(tracePath, $$"""{"trace_id":"trace-{{index:00}}"}""");
            var ageDays = index < 3 ? index : 25 + index;
            File.SetLastWriteTimeUtc(tracePath, DateTime.UtcNow.AddDays(-ageDays));
        }

        var service = new OperationalHistoryCompactionService(workspace.RootPath, workspace.Paths, config);
        var report = service.Compact();

        var validationFamily = Assert.Single(report.Families, family => family.FamilyId == "validation_trace_history");
        Assert.Equal(3, validationFamily.PreservedHotFileCount);
        Assert.Equal(12, validationFamily.ArchivedFileCount);
        Assert.Equal(3, Directory.EnumerateFiles(validationTraceRoot, "*.json", SearchOption.TopDirectoryOnly).Count());

        var archiveIndexPath = OperationalHistoryCompactionService.GetArchiveIndexPath(workspace.Paths);
        var archiveIndex = JsonSerializer.Deserialize<OperationalHistoryArchiveIndex>(File.ReadAllText(archiveIndexPath), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        });
        Assert.NotNull(archiveIndex);
        Assert.Equal(12, archiveIndex!.Entries.Count(entry => entry.FamilyId == "validation_trace_history"));
    }

    [Fact]
    public void OperationalHistoryCompaction_ArchivesOlderWorkerExecutionArtifactsOutsideHotWindow()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        Directory.CreateDirectory(workspace.Paths.WorkerArtifactsRoot);
        Directory.CreateDirectory(workspace.Paths.WorkerExecutionArtifactsRoot);
        Directory.CreateDirectory(workspace.Paths.ProviderArtifactsRoot);
        Directory.CreateDirectory(workspace.Paths.ReviewArtifactsRoot);

        var oldWorker = Path.Combine(workspace.Paths.WorkerArtifactsRoot, "T-OLD.json");
        var oldExecution = Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, "T-OLD.json");
        var oldProvider = Path.Combine(workspace.Paths.ProviderArtifactsRoot, "T-OLD.json");
        var oldReview = Path.Combine(workspace.Paths.ReviewArtifactsRoot, "T-OLD.json");

        File.WriteAllBytes(oldWorker, new byte[4 * 1024 * 1024]);
        File.WriteAllBytes(oldExecution, new byte[3 * 1024 * 1024]);
        File.WriteAllBytes(oldProvider, new byte[2 * 1024 * 1024]);
        File.WriteAllBytes(oldReview, new byte[1024]);
        File.SetLastWriteTimeUtc(oldWorker, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldExecution, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldProvider, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldReview, DateTime.UtcNow.AddDays(-10));

        for (var index = 0; index < 40; index++)
        {
            var worker = Path.Combine(workspace.Paths.WorkerArtifactsRoot, $"T-NEW-{index:00}.json");
            var execution = Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, $"T-NEW-{index:00}.json");
            var provider = Path.Combine(workspace.Paths.ProviderArtifactsRoot, $"T-NEW-{index:00}.json");
            File.WriteAllText(worker, $$"""{"task_id":"T-NEW-{{index:00}}","kind":"worker"}""");
            File.WriteAllText(execution, $$"""{"task_id":"T-NEW-{{index:00}}","kind":"execution"}""");
            File.WriteAllText(provider, $$"""{"task_id":"T-NEW-{{index:00}}","kind":"provider"}""");
            var timestamp = DateTime.UtcNow.AddMinutes(-index);
            File.SetLastWriteTimeUtc(worker, timestamp);
            File.SetLastWriteTimeUtc(execution, timestamp);
            File.SetLastWriteTimeUtc(provider, timestamp);
        }

        var service = new OperationalHistoryCompactionService(workspace.RootPath, workspace.Paths, config);
        var report = service.Compact();

        var workerFamily = Assert.Single(report.Families, family => family.FamilyId == "worker_execution_artifact_history");
        Assert.True(workerFamily.ArchivedFileCount >= 4);
        Assert.False(File.Exists(oldWorker));
        Assert.False(File.Exists(oldExecution));
        Assert.False(File.Exists(oldProvider));
        Assert.False(File.Exists(oldReview));

        var archiveIndexPath = OperationalHistoryCompactionService.GetArchiveIndexPath(workspace.Paths);
        var archiveIndex = JsonSerializer.Deserialize<OperationalHistoryArchiveIndex>(File.ReadAllText(archiveIndexPath), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        });
        Assert.NotNull(archiveIndex);
        Assert.Contains(archiveIndex!.Entries, entry => entry.FamilyId == "worker_execution_artifact_history" && entry.OriginalPath.EndsWith("/T-OLD.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ArchiveReadiness_ProjectsArchiveReasonHotWindowAndPromotionRelevantEntries()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        Directory.CreateDirectory(workspace.Paths.WorkerArtifactsRoot);
        Directory.CreateDirectory(workspace.Paths.WorkerExecutionArtifactsRoot);
        Directory.CreateDirectory(workspace.Paths.ProviderArtifactsRoot);
        Directory.CreateDirectory(workspace.Paths.ReviewArtifactsRoot);

        var oldWorker = Path.Combine(workspace.Paths.WorkerArtifactsRoot, "T-OLD.json");
        var oldExecution = Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, "T-OLD.json");
        var oldProvider = Path.Combine(workspace.Paths.ProviderArtifactsRoot, "T-OLD.json");
        var oldReview = Path.Combine(workspace.Paths.ReviewArtifactsRoot, "T-OLD.json");

        File.WriteAllBytes(oldWorker, new byte[4 * 1024 * 1024]);
        File.WriteAllBytes(oldExecution, new byte[3 * 1024 * 1024]);
        File.WriteAllBytes(oldProvider, new byte[2 * 1024 * 1024]);
        File.WriteAllText(oldReview, """{"task_id":"T-OLD"}""");
        File.SetLastWriteTimeUtc(oldWorker, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldExecution, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldProvider, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldReview, DateTime.UtcNow.AddDays(-10));

        for (var index = 0; index < 40; index++)
        {
            var worker = Path.Combine(workspace.Paths.WorkerArtifactsRoot, $"T-NEW-{index:00}.json");
            var execution = Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, $"T-NEW-{index:00}.json");
            var provider = Path.Combine(workspace.Paths.ProviderArtifactsRoot, $"T-NEW-{index:00}.json");
            File.WriteAllText(worker, $$"""{"task_id":"T-NEW-{{index:00}}","kind":"worker"}""");
            File.WriteAllText(execution, $$"""{"task_id":"T-NEW-{{index:00}}","kind":"execution"}""");
            File.WriteAllText(provider, $$"""{"task_id":"T-NEW-{{index:00}}","kind":"provider"}""");
            var timestamp = DateTime.UtcNow.AddMinutes(-index);
            File.SetLastWriteTimeUtc(worker, timestamp);
            File.SetLastWriteTimeUtc(execution, timestamp);
            File.SetLastWriteTimeUtc(provider, timestamp);
        }

        var compaction = new OperationalHistoryCompactionService(workspace.RootPath, workspace.Paths, config);
        compaction.Compact();

        var readiness = new OperationalHistoryArchiveReadinessService(workspace.RootPath, workspace.Paths, config).Build();

        var workerFamily = Assert.Single(readiness.Families, family => family.FamilyId == "worker_execution_artifact_history");
        Assert.True(workerFamily.ArchivedFileCount >= 4);
        Assert.Equal(RuntimeArtifactRetentionMode.RollingWindow, workerFamily.RetentionMode);
        Assert.Equal(20, workerFamily.HotWindowCount);
        Assert.Equal(14, workerFamily.MaxAgeDays);
        Assert.Equal("rolling_window_hot_and_age_bound", workerFamily.RetentionDiscipline);
        Assert.Equal("compact_history", workerFamily.ClosureDiscipline);
        Assert.Equal("archive_ready_after_hot_window_with_followup", workerFamily.ArchiveReadinessState);
        Assert.Equal(20, workerFamily.PreservedHotFileCount);
        Assert.Contains("fell outside the configured hot window", workerFamily.ArchiveReason, StringComparison.Ordinal);
        Assert.True(workerFamily.PromotionRelevantCount >= 2);
        Assert.Contains(readiness.PromotionRelevantEntries, entry => entry.OriginalPath.EndsWith("/provider/T-OLD.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(readiness.PromotionRelevantEntries, entry => entry.OriginalPath.EndsWith("/reviews/T-OLD.json", StringComparison.OrdinalIgnoreCase));
        Assert.All(readiness.PromotionRelevantEntries, entry => Assert.True(entry.PromotionRelevant));
        Assert.All(readiness.PromotionRelevantEntries, entry => Assert.Equal("archive_ready_after_hot_window_with_followup", entry.ArchiveReadinessState));
        Assert.True(File.Exists(OperationalHistoryArchiveReadinessService.GetLatestReportPath(workspace.Paths)));
    }

    [Fact]
    public void ArchiveFollowUpQueue_GroupsPromotionRelevantEntriesIntoProviderAndReviewBuckets()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        Directory.CreateDirectory(workspace.Paths.WorkerArtifactsRoot);
        Directory.CreateDirectory(workspace.Paths.WorkerExecutionArtifactsRoot);
        Directory.CreateDirectory(workspace.Paths.ProviderArtifactsRoot);
        Directory.CreateDirectory(workspace.Paths.ReviewArtifactsRoot);

        var oldWorker = Path.Combine(workspace.Paths.WorkerArtifactsRoot, "T-OLD.json");
        var oldExecution = Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, "T-OLD.json");
        var oldProvider = Path.Combine(workspace.Paths.ProviderArtifactsRoot, "T-OLD.json");
        var oldReview = Path.Combine(workspace.Paths.ReviewArtifactsRoot, "T-OLD.json");

        File.WriteAllBytes(oldWorker, new byte[4 * 1024 * 1024]);
        File.WriteAllBytes(oldExecution, new byte[3 * 1024 * 1024]);
        File.WriteAllBytes(oldProvider, new byte[2 * 1024 * 1024]);
        File.WriteAllText(oldReview, """{"task_id":"T-OLD"}""");
        File.SetLastWriteTimeUtc(oldWorker, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldExecution, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldProvider, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldReview, DateTime.UtcNow.AddDays(-10));

        for (var index = 0; index < 40; index++)
        {
            var worker = Path.Combine(workspace.Paths.WorkerArtifactsRoot, $"T-NEW-{index:00}.json");
            var provider = Path.Combine(workspace.Paths.ProviderArtifactsRoot, $"T-NEW-{index:00}.json");
            File.WriteAllText(worker, $$"""{"task_id":"T-NEW-{{index:00}}","kind":"worker"}""");
            File.WriteAllText(provider, $$"""{"task_id":"T-NEW-{{index:00}}","kind":"provider"}""");
            var timestamp = DateTime.UtcNow.AddMinutes(-index);
            File.SetLastWriteTimeUtc(worker, timestamp);
            File.SetLastWriteTimeUtc(provider, timestamp);
        }

        var compaction = new OperationalHistoryCompactionService(workspace.RootPath, workspace.Paths, config);
        compaction.Compact();

        var queue = new OperationalHistoryArchiveFollowUpQueueService(workspace.RootPath, workspace.Paths, config).Build();

        Assert.Equal(2, queue.Groups.Length);
        Assert.Contains(queue.Groups, group => group.GroupId == "provider_evidence" && group.ItemCount >= 1);
        Assert.Contains(queue.Groups, group => group.GroupId == "review_evidence" && group.ItemCount >= 1);
        Assert.Contains(queue.Entries, entry => entry.GroupId == "provider_evidence" && entry.TaskId == "T-OLD");
        Assert.Contains(queue.Entries, entry => entry.GroupId == "review_evidence" && entry.TaskId == "T-OLD");
        Assert.All(queue.Entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.RecommendedAction)));
        Assert.True(File.Exists(OperationalHistoryArchiveFollowUpQueueService.GetLatestReportPath(workspace.Paths)));
    }

    [Fact]
    public void SustainabilityAudit_DoesNotTreatOnDemandDetailArtifactsAsReadPathPressure()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        var workerRoot = workspace.Paths.WorkerArtifactsRoot;
        Directory.CreateDirectory(workerRoot);

        File.WriteAllBytes(Path.Combine(workerRoot, "large-artifact.bin"), new byte[9 * 1024 * 1024]);

        var auditService = new SustainabilityAuditService(workspace.RootPath, workspace.Paths, config, new StubCodeGraphQueryService());
        var report = auditService.Audit();

        var workerProjection = Assert.Single(report.Families, family => family.FamilyId == "worker_execution_artifact_history");
        Assert.True(workerProjection.OverByteBudget);
        Assert.Equal("compact_history", workerProjection.ClosureDiscipline);
        Assert.Equal(0, workerProjection.ReadPathPressureCount);
        Assert.DoesNotContain(report.Findings, finding => finding.FamilyId == "worker_execution_artifact_history" && finding.Category == "read_path_pressure");
        Assert.Contains(report.Findings, finding => finding.FamilyId == "worker_execution_artifact_history" && finding.Category == "size_budget_exceeded");
    }

    [Fact]
    public void SustainabilityAudit_ExcludesQuarantineWorktreesFromEphemeralResidue()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        var worktreeRoot = Path.GetFullPath(Path.Combine(workspace.RootPath, config.WorktreeRoot));
        var quarantineFile = Path.Combine(worktreeRoot, "_quarantine", "T-CARD-282-001", "marker.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(quarantineFile)!);
        File.WriteAllText(quarantineFile, "quarantined worktree");

        var auditService = new SustainabilityAuditService(workspace.RootPath, workspace.Paths, config, new StubCodeGraphQueryService());
        var report = auditService.Audit();

        var ephemeralProjection = Assert.Single(report.Families, family => family.FamilyId == "ephemeral_runtime_residue");
        Assert.Equal(0, ephemeralProjection.FileCount);
        Assert.Equal("auto_expire", ephemeralProjection.RetentionDiscipline);
        Assert.Equal("cleanup_only", ephemeralProjection.ClosureDiscipline);
        Assert.Equal("cleanup_only_not_archive", ephemeralProjection.ArchiveReadinessState);
        Assert.DoesNotContain(report.Findings, finding => finding.FamilyId == "ephemeral_runtime_residue");
    }
}
