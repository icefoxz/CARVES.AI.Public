using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public enum RuntimeCommitClass
{
    FeatureCommit,
    TruthCheckpoint,
    LocalResidue,
}

public sealed record RuntimeCommitHygieneDecision(
    RuntimeCommitClass CommitClass,
    string CommitSummary,
    string MaintenanceSummary);

public static class RuntimeCommitHygieneService
{
    public static string DescribeRetentionDiscipline(RuntimeArtifactFamilyPolicy family)
    {
        return family.RetentionMode switch
        {
            RuntimeArtifactRetentionMode.Permanent => "permanent",
            RuntimeArtifactRetentionMode.SingleVersion => "single_version",
            RuntimeArtifactRetentionMode.RollingWindow when family.Budget.HotWindowCount is not null && family.Budget.MaxAgeDays is not null
                => "rolling_window_hot_and_age_bound",
            RuntimeArtifactRetentionMode.RollingWindow when family.Budget.HotWindowCount is not null
                => "rolling_window_hot_bound",
            RuntimeArtifactRetentionMode.RollingWindow when family.Budget.MaxAgeDays is not null
                => "rolling_window_age_bound",
            RuntimeArtifactRetentionMode.RollingWindow => "rolling_window",
            RuntimeArtifactRetentionMode.AutoExpire => "auto_expire",
            RuntimeArtifactRetentionMode.ArchiveSummary => "archive_summary",
            _ => "unspecified",
        };
    }

    public static string DescribeArchiveReadinessState(RuntimeArtifactFamilyPolicy family)
    {
        if (family.CleanupEligible || family.ArtifactClass == RuntimeArtifactClass.EphemeralResidue)
        {
            return "cleanup_only_not_archive";
        }

        if (family.RetentionMode == RuntimeArtifactRetentionMode.ArchiveSummary
            || family.ArtifactClass == RuntimeArtifactClass.AuditArchive)
        {
            return "archive_only";
        }

        if (!family.CompactEligible)
        {
            return "not_applicable";
        }

        if (string.Equals(family.FamilyId, "worker_execution_artifact_history", StringComparison.Ordinal))
        {
            return "archive_ready_after_hot_window_with_followup";
        }

        if (family.Budget.HotWindowCount is not null && family.Budget.MaxAgeDays is not null)
        {
            return "archive_ready_after_hot_window_or_age_window";
        }

        if (family.Budget.HotWindowCount is not null)
        {
            return "archive_ready_after_hot_window";
        }

        if (family.Budget.MaxAgeDays is not null)
        {
            return "archive_ready_after_age_window";
        }

        return "archive_ready_when_compaction_runs";
    }

    public static string DescribeClosureDiscipline(RuntimeArtifactFamilyPolicy family)
    {
        if (family.CleanupEligible || family.ArtifactClass == RuntimeArtifactClass.EphemeralResidue)
        {
            return "cleanup_only";
        }

        if (family.CompactEligible)
        {
            return "compact_history";
        }

        if (family.RebuildEligible && family.ArtifactClass == RuntimeArtifactClass.DerivedTruth)
        {
            return "rebuild_then_checkpoint";
        }

        return ResolveCommitClass(family.FamilyId, family.ArtifactClass) switch
        {
            RuntimeCommitClass.FeatureCommit => "feature_commit",
            RuntimeCommitClass.TruthCheckpoint => "truth_checkpoint",
            _ => "keep_local",
        };
    }

    public static string DescribeClosureDiscipline(RuntimeArtifactBudgetProjection projection)
    {
        return projection.RecommendedAction switch
        {
            RuntimeMaintenanceActionKind.PruneEphemeral => "cleanup_only",
            RuntimeMaintenanceActionKind.CompactHistory => "compact_history",
            RuntimeMaintenanceActionKind.RebuildDerived => "rebuild_then_checkpoint",
            _ => ResolveCommitClass(projection.FamilyId, projection.ArtifactClass) switch
            {
                RuntimeCommitClass.FeatureCommit => "feature_commit",
                RuntimeCommitClass.TruthCheckpoint => "truth_checkpoint",
                _ => "keep_local",
            },
        };
    }

    public static RuntimeCommitHygieneDecision Resolve(RuntimeArtifactFamilyPolicy family)
    {
        return ResolveInternal(
            family.FamilyId,
            family.ArtifactClass,
            RuntimeMaintenanceActionKind.None,
            family.CleanupEligible,
            family.CompactEligible,
            family.RebuildEligible);
    }

    public static RuntimeCommitHygieneDecision Resolve(RuntimeArtifactBudgetProjection projection)
    {
        return ResolveInternal(
            projection.FamilyId,
            projection.ArtifactClass,
            projection.RecommendedAction,
            cleanupEligible: false,
            compactEligible: projection.RecommendedAction == RuntimeMaintenanceActionKind.CompactHistory,
            rebuildEligible: projection.RecommendedAction == RuntimeMaintenanceActionKind.RebuildDerived);
    }

    public static IReadOnlyList<string> DescribeCommitClasses()
    {
        return
        [
            "Feature commit: source, tests, docs, and intentionally changed stable definition truth move together in the scoped feature change.",
            "Truth-only checkpoint: task/taskgraph truth, governed mirrors, and bounded sustainability checkpoints can be committed without bundling product code.",
            "Local residue: live state, operational history, audit archives, and ephemeral spill stay local and should be reduced with maintenance rather than committed."
        ];
    }

    public static IReadOnlyList<string> DescribeMaintenanceMapping()
    {
        return
        [
            "cleanup: prune EphemeralResidue and stale worktree spill; do not treat cleanup as a license to delete canonical truth or historical evidence.",
            "compact-history: reduce OperationalHistory and explicitly compactable bounded mirrors/projections behind the hot window; compacted history still stays local.",
            "rebuild: regenerate DerivedTruth when the projection itself drifted; rebuild does not reclassify live state or history as commit targets."
        ];
    }

    public static string FormatCommitClass(RuntimeCommitClass commitClass)
    {
        return commitClass switch
        {
            RuntimeCommitClass.FeatureCommit => "feature_commit",
            RuntimeCommitClass.TruthCheckpoint => "truth_checkpoint",
            _ => "local_residue",
        };
    }

    private static RuntimeCommitHygieneDecision ResolveInternal(
        string familyId,
        RuntimeArtifactClass artifactClass,
        RuntimeMaintenanceActionKind recommendedAction,
        bool cleanupEligible,
        bool compactEligible,
        bool rebuildEligible)
    {
        var commitClass = ResolveCommitClass(familyId, artifactClass);
        var commitSummary = commitClass switch
        {
            RuntimeCommitClass.FeatureCommit => "Commit with the intentional feature or governance change when this family is updated on purpose.",
            RuntimeCommitClass.TruthCheckpoint => "Commit only as a bounded truth/checkpoint writeback; do not bundle it with arbitrary runtime residue.",
            _ => "Keep local; this family is not a normal commit target.",
        };

        var maintenanceSummary = recommendedAction switch
        {
            RuntimeMaintenanceActionKind.PruneEphemeral => "Run `cleanup` before commit; this family is cleanup-only local residue.",
            RuntimeMaintenanceActionKind.CompactHistory => "Run `compact-history` before commit review; this family stays local even after compaction.",
            RuntimeMaintenanceActionKind.RebuildDerived => "Run `rebuild` before checkpointing this derived surface.",
            _ when commitClass == RuntimeCommitClass.LocalResidue && cleanupEligible => "Keep local; use `cleanup` when this residue appears.",
            _ when commitClass == RuntimeCommitClass.LocalResidue && compactEligible => "Keep local; use `compact-history` if the hot window or size budget drifts.",
            _ when commitClass == RuntimeCommitClass.LocalResidue && artifactClass == RuntimeArtifactClass.LiveState => "Keep local; inspectable live state should not enter feature or checkpoint commits.",
            _ when commitClass == RuntimeCommitClass.LocalResidue && artifactClass == RuntimeArtifactClass.AuditArchive => "Keep local; archive detail remains traceability evidence outside normal commit flow.",
            _ when commitClass == RuntimeCommitClass.TruthCheckpoint && rebuildEligible => "Checkpoint only when intentionally regenerated; otherwise rebuild locally and keep it out of the commit.",
            _ when commitClass == RuntimeCommitClass.TruthCheckpoint => "Commit only as a truth-only checkpoint when the task or maintenance flow explicitly produced it.",
            _ => "Commit only when the change is intentional and within the current feature scope.",
        };

        return new RuntimeCommitHygieneDecision(commitClass, commitSummary, maintenanceSummary);
    }

    private static RuntimeCommitClass ResolveCommitClass(string familyId, RuntimeArtifactClass artifactClass)
    {
        return familyId switch
        {
            "task_truth" or "execution_memory_truth" or "governed_markdown_mirror" or "sustainability_projection" or "codegraph_derived"
                => RuntimeCommitClass.TruthCheckpoint,
            "memory_truth" or "routing_truth" or "platform_definition_truth" or "platform_provider_definition_truth" or "validation_suite_truth"
                => RuntimeCommitClass.FeatureCommit,
            "context_pack_projection" or "execution_packet_mirror"
                => RuntimeCommitClass.LocalResidue,
            _ when artifactClass is RuntimeArtifactClass.OperationalHistory
                or RuntimeArtifactClass.LiveState
                or RuntimeArtifactClass.EphemeralResidue
                or RuntimeArtifactClass.AuditArchive
                => RuntimeCommitClass.LocalResidue,
            _ when artifactClass is RuntimeArtifactClass.DerivedTruth or RuntimeArtifactClass.GovernedMirror
                => RuntimeCommitClass.TruthCheckpoint,
            _ => RuntimeCommitClass.FeatureCommit,
        };
    }
}
