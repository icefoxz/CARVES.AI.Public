using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeArtifactCatalogService
{
    private RuntimeArtifactFamilyPolicy[] BuildOperationalHistoryFamilies()
    {
        return
        [
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "planning_draft_residue",
                DisplayName = "Planning draft residue",
                ArtifactClass = RuntimeArtifactClass.EphemeralResidue,
                RetentionMode = RuntimeArtifactRetentionMode.AutoExpire,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Hidden,
                CleanupEligible = true,
                Roots =
                [
                    ToRepoRelative(paths.PlanningCardDraftsRoot),
                    ToRepoRelative(paths.PlanningTaskGraphDraftsRoot),
                ],
                AllowedContents = ["top-level card draft spill", "top-level taskgraph draft spill"],
                ForbiddenContents = ["tracked historical draft truth", "planner signals", "replans", "canonical task truth"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 0, MaxOnlineBytes = 0, MaxAgeDays = 1 },
                Summary = "Top-level planning draft spill should stay local and be pruned instead of surfacing as default worktree residue.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "planning_runtime_history",
                DisplayName = "Planning runtime history",
                ArtifactClass = RuntimeArtifactClass.OperationalHistory,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                CompactEligible = true,
                Roots = [ToRepoRelative(paths.PlanningRoot)],
                AllowedContents = ["tracked draft history", "planner signals", "replans", "payload projections"],
                ForbiddenContents = ["canonical task truth", "manual policy files", "tmp residue"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 400, MaxOnlineBytes = 4 * 1024 * 1024, HotWindowCount = 80, MaxAgeDays = 30 },
                Summary = "Planner runtime material stays reviewable online, but top-level draft spill is kept local while tracked history, signals, and replans remain compactable operational history.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "execution_surface_history",
                DisplayName = "Execution surface history",
                ArtifactClass = RuntimeArtifactClass.OperationalHistory,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                CompactEligible = true,
                Roots = [ToRepoRelative(Path.Combine(paths.AiRoot, "execution"))],
                AllowedContents = ["task execution envelopes", "result envelopes", "execution-side status detail"],
                ForbiddenContents = ["canonical task graph writes", "tmp residue"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 400, MaxOnlineBytes = 4 * 1024 * 1024, HotWindowCount = 60, MaxAgeDays = 30 },
                Summary = "Per-task execution envelopes stay inspectable for active review, but only a tighter online slice now remains local before older envelopes archive.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "validation_trace_history",
                DisplayName = "Validation trace history",
                ArtifactClass = RuntimeArtifactClass.OperationalHistory,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                CompactEligible = true,
                Roots = [ToRepoRelative(Path.Combine(paths.AiRoot, "validation", "traces"))],
                AllowedContents = ["per-run validation traces", "selection decisions", "route evidence detail"],
                ForbiddenContents = ["canonical task truth", "default AI context"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 15, MaxOnlineBytes = 1024 * 1024, HotWindowCount = 15, MaxAgeDays = 14 },
                Summary = "Validation trace detail is operational history and now keeps only a compact online trace window.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "validation_summary_history",
                DisplayName = "Validation summary history",
                ArtifactClass = RuntimeArtifactClass.OperationalHistory,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                Roots = [ToRepoRelative(Path.Combine(paths.AiRoot, "validation", "summaries"))],
                AllowedContents = ["validation batch summaries", "latest validation summary"],
                ForbiddenContents = ["raw provider transcripts", "full build logs"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 120, MaxOnlineBytes = 2 * 1024 * 1024, MaxAgeDays = 90 },
                Summary = "Validation summaries stay online longer than detail and feed operator readiness surfaces.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "execution_run_detail_history",
                DisplayName = "Execution run detail history",
                ArtifactClass = RuntimeArtifactClass.OperationalHistory,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                CompactEligible = true,
                Roots = [ToRepoRelative(Path.Combine(paths.RuntimeRoot, "runs"))],
                AllowedContents = ["execution run steps", "run metadata", "run status detail", "selected pack attribution references"],
                ForbiddenContents = ["canonical task truth", "default summary projections"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 20, MaxOnlineBytes = 2 * 1024 * 1024, HotWindowCount = 3, MaxAgeDays = 14 },
                Summary = "Execution run detail is operational history and now keeps only the smallest active replay window before older per-run detail archives.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "execution_run_report_history",
                DisplayName = "Execution run report history",
                ArtifactClass = RuntimeArtifactClass.OperationalHistory,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                CompactEligible = true,
                Roots = [ToRepoRelative(Path.Combine(paths.RuntimeRoot, "run-reports"))],
                AllowedContents = ["execution run summaries", "modules touched", "boundary and failure summaries", "selected pack attribution references"],
                ForbiddenContents = ["raw command traces", "tmp residue"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 80, MaxOnlineBytes = 1024 * 1024, HotWindowCount = 30, MaxAgeDays = 45 },
                Summary = "Run reports remain the online summary layer for execution history, but only a tighter online report window now remains local before archive.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "runtime_failure_detail_history",
                DisplayName = "Runtime failure detail history",
                ArtifactClass = RuntimeArtifactClass.OperationalHistory,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                CompactEligible = true,
                Roots =
                [
                    ToRepoRelative(paths.FailuresRoot),
                    ToRepoRelative(paths.RuntimeFailureArtifactsRoot),
                ],
                AllowedContents = ["failure detail records", "runtime failure artifacts", "failure evidence detail"],
                ForbiddenContents = ["canonical task truth", "default AI context", "manual policy files"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 160, MaxOnlineBytes = 2 * 1024 * 1024, HotWindowCount = 40, MaxAgeDays = 45 },
                Summary = "Runtime failure detail stays available for incident review, but only a tighter online failure window remains local before older detail archives.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "worker_execution_artifact_history",
                DisplayName = "Worker execution artifact history",
                ArtifactClass = RuntimeArtifactClass.OperationalHistory,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                CompactEligible = true,
                Roots =
                [
                    ToRepoRelative(paths.WorkerArtifactsRoot),
                    ToRepoRelative(paths.WorkerExecutionArtifactsRoot),
                    ToRepoRelative(paths.WorkerPermissionArtifactsRoot),
                    ToRepoRelative(paths.ProviderArtifactsRoot),
                    ToRepoRelative(paths.ReviewArtifactsRoot),
                    ToRepoRelative(paths.MergeArtifactsRoot),
                ],
                AllowedContents = ["latest worker execution artifact", "worker permission artifact", "provider artifact", "review artifact", "merge candidate artifact"],
                ForbiddenContents = ["canonical summaries copied verbatim into truth roots"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 240, MaxOnlineBytes = 6 * 1024 * 1024, HotWindowCount = 20, MaxAgeDays = 14 },
                Summary = "Worker-facing artifacts stay inspectable on demand, but only the most recent review window remains local before older detail archives.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "platform_runtime_ledger_history",
                DisplayName = "Platform runtime ledger history",
                ArtifactClass = RuntimeArtifactClass.OperationalHistory,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                Roots =
                [
                    ToRepoRelative(paths.PlatformQualificationRunLedgerFile),
                    ToRepoRelative(paths.PlatformDelegatedRunLifecycleLiveStateFile),
                    ToRepoRelative(paths.PlatformDelegatedRunRecoveryLedgerLiveStateFile),
                ],
                AllowedContents = ["qualification ledgers", "delegated run lifecycle summaries", "recovery ledgers"],
                ForbiddenContents = ["host session state", "manual policy config", "tmp residue"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 3, MaxOnlineBytes = 2 * 1024 * 1024, MaxAgeDays = 180 },
                Summary = "Platform ledgers are reviewable history for runtime coordination and recovery, not local session state.",
            },
        ];
    }
}
