using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeArtifactCatalogService
{
    private string ToRepoRelative(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Path.GetRelativePath(repoRoot, fullPath).Replace('\\', '/');
    }

    private static bool NeedsRefresh(RuntimeArtifactCatalog catalog)
    {
        if (!string.Equals(catalog.SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
        {
            return true;
        }

        return catalog.Families.Any(family => string.Equals(family.FamilyId, "codegraph_detail_derived", StringComparison.Ordinal))
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "worker_execution_artifact_history", StringComparison.Ordinal)
                                                  && family.CompactEligible
                                                  && family.RetentionMode == RuntimeArtifactRetentionMode.RollingWindow
                                                  && family.Budget.HotWindowCount is not null
                                                  && family.Roots.Contains(".ai/artifacts/worker-permissions", StringComparer.Ordinal)
                                                  && family.Roots.Contains(".ai/artifacts/merge-candidates", StringComparer.Ordinal))
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "governed_markdown_mirror", StringComparison.Ordinal)
                                                  && family.ArtifactClass == RuntimeArtifactClass.GovernedMirror)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "runtime_live_state", StringComparison.Ordinal)
                                                  && family.ArtifactClass == RuntimeArtifactClass.LiveState)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "platform_provider_definition_truth", StringComparison.Ordinal)
                                                  && family.ArtifactClass == RuntimeArtifactClass.CanonicalTruth)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "platform_provider_live_state", StringComparison.Ordinal)
                                                  && family.ArtifactClass == RuntimeArtifactClass.LiveState)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "platform_live_state", StringComparison.Ordinal)
                                                  && family.ArtifactClass == RuntimeArtifactClass.LiveState)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "execution_surface_history", StringComparison.Ordinal)
                                                  && family.CompactEligible
                                                  && family.Budget.HotWindowCount is not null)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "planning_runtime_history", StringComparison.Ordinal)
                                                  && family.CompactEligible
                                                  && family.Budget.HotWindowCount is not null)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "execution_run_report_history", StringComparison.Ordinal)
                                                  && family.CompactEligible
                                                  && family.Budget.HotWindowCount is not null)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "runtime_pack_admission_evidence", StringComparison.Ordinal)
                                                  && family.ArtifactClass == RuntimeArtifactClass.GovernedMirror
                                                  && family.RetentionMode == RuntimeArtifactRetentionMode.SingleVersion)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "runtime_pack_selection_audit_evidence", StringComparison.Ordinal)
                                                  && family.ArtifactClass == RuntimeArtifactClass.OperationalHistory
                                                  && family.CompactEligible
                                                  && family.RetentionMode == RuntimeArtifactRetentionMode.RollingWindow)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "runtime_pack_switch_policy_evidence", StringComparison.Ordinal)
                                                  && family.ArtifactClass == RuntimeArtifactClass.GovernedMirror
                                                  && family.RetentionMode == RuntimeArtifactRetentionMode.SingleVersion)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "context_pack_projection", StringComparison.Ordinal)
                                                  && family.CompactEligible
                                                  && family.RetentionMode == RuntimeArtifactRetentionMode.RollingWindow
                                                  && family.Budget.HotWindowCount is not null)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "execution_packet_mirror", StringComparison.Ordinal)
                                                  && family.CompactEligible
                                                  && family.RetentionMode == RuntimeArtifactRetentionMode.RollingWindow
                                                  && family.Budget.HotWindowCount is not null)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "runtime_failure_detail_history", StringComparison.Ordinal)
                                                  && family.CompactEligible
                                                  && family.ArtifactClass == RuntimeArtifactClass.OperationalHistory
                                                  && family.Budget.HotWindowCount is not null)
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "execution_memory_truth", StringComparison.Ordinal))
               || !catalog.Families.Any(family => string.Equals(family.FamilyId, "planning_draft_residue", StringComparison.Ordinal)
                                                  && family.ArtifactClass == RuntimeArtifactClass.EphemeralResidue
                                                  && family.CleanupEligible);
    }
}
