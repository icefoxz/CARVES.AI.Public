using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeCommitHygieneServiceTests
{
    [Theory]
    [InlineData("task_truth", RuntimeArtifactClass.CanonicalTruth, RuntimeCommitClass.TruthCheckpoint)]
    [InlineData("memory_truth", RuntimeArtifactClass.CanonicalTruth, RuntimeCommitClass.FeatureCommit)]
    [InlineData("context_pack_projection", RuntimeArtifactClass.DerivedTruth, RuntimeCommitClass.LocalResidue)]
    [InlineData("planning_runtime_history", RuntimeArtifactClass.OperationalHistory, RuntimeCommitClass.LocalResidue)]
    [InlineData("planning_draft_residue", RuntimeArtifactClass.EphemeralResidue, RuntimeCommitClass.LocalResidue)]
    [InlineData("runtime_live_state", RuntimeArtifactClass.LiveState, RuntimeCommitClass.LocalResidue)]
    public void Resolve_MapsRepresentativeFamiliesToExpectedCommitClasses(
        string familyId,
        RuntimeArtifactClass artifactClass,
        RuntimeCommitClass expectedCommitClass)
    {
        var decision = RuntimeCommitHygieneService.Resolve(new RuntimeArtifactFamilyPolicy
        {
            FamilyId = familyId,
            ArtifactClass = artifactClass,
        });

        Assert.Equal(expectedCommitClass, decision.CommitClass);
    }

    [Fact]
    public void Resolve_ExplainsCleanupAndCompactionForLocalResidue()
    {
        var cleanupDecision = RuntimeCommitHygieneService.Resolve(new RuntimeArtifactFamilyPolicy
        {
            FamilyId = "ephemeral_runtime_residue",
            ArtifactClass = RuntimeArtifactClass.EphemeralResidue,
            CleanupEligible = true,
        });

        var compactDecision = RuntimeCommitHygieneService.Resolve(new RuntimeArtifactFamilyPolicy
        {
            FamilyId = "execution_surface_history",
            ArtifactClass = RuntimeArtifactClass.OperationalHistory,
            CompactEligible = true,
        });

        Assert.Equal(RuntimeCommitClass.LocalResidue, cleanupDecision.CommitClass);
        Assert.Contains("cleanup", cleanupDecision.MaintenanceSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RuntimeCommitClass.LocalResidue, compactDecision.CommitClass);
        Assert.Contains("compact-history", compactDecision.MaintenanceSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_UsesActiveRecommendedActionForProjectionGuidance()
    {
        var projection = new RuntimeArtifactBudgetProjection
        {
            FamilyId = "planning_runtime_history",
            ArtifactClass = RuntimeArtifactClass.OperationalHistory,
            RecommendedAction = RuntimeMaintenanceActionKind.CompactHistory,
        };

        var decision = RuntimeCommitHygieneService.Resolve(projection);

        Assert.Equal(RuntimeCommitClass.LocalResidue, decision.CommitClass);
        Assert.Contains("compact-history", decision.MaintenanceSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisciplineDescriptions_ProjectCleanupAndArchiveReadinessWithoutGuessing()
    {
        var cleanupOnly = new RuntimeArtifactFamilyPolicy
        {
            FamilyId = "ephemeral_runtime_residue",
            ArtifactClass = RuntimeArtifactClass.EphemeralResidue,
            RetentionMode = RuntimeArtifactRetentionMode.AutoExpire,
            CleanupEligible = true,
        };

        var compactableHistory = new RuntimeArtifactFamilyPolicy
        {
            FamilyId = "worker_execution_artifact_history",
            ArtifactClass = RuntimeArtifactClass.OperationalHistory,
            RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
            CompactEligible = true,
            Budget = new RuntimeArtifactBudgetPolicy { HotWindowCount = 20, MaxAgeDays = 14 },
        };

        Assert.Equal("auto_expire", RuntimeCommitHygieneService.DescribeRetentionDiscipline(cleanupOnly));
        Assert.Equal("cleanup_only", RuntimeCommitHygieneService.DescribeClosureDiscipline(cleanupOnly));
        Assert.Equal("cleanup_only_not_archive", RuntimeCommitHygieneService.DescribeArchiveReadinessState(cleanupOnly));

        Assert.Equal("rolling_window_hot_and_age_bound", RuntimeCommitHygieneService.DescribeRetentionDiscipline(compactableHistory));
        Assert.Equal("compact_history", RuntimeCommitHygieneService.DescribeClosureDiscipline(compactableHistory));
        Assert.Equal("archive_ready_after_hot_window_with_followup", RuntimeCommitHygieneService.DescribeArchiveReadinessState(compactableHistory));
    }
}
