using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeArtifactCatalogService
{
    private RuntimeArtifactFamilyPolicy[] BuildPackProjectionFamilies()
    {
        return
        [
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "runtime_pack_admission_evidence",
                DisplayName = "Runtime pack admission evidence",
                ArtifactClass = RuntimeArtifactClass.GovernedMirror,
                RetentionMode = RuntimeArtifactRetentionMode.SingleVersion,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                Roots = [ToRepoRelative(Path.Combine(paths.ArtifactsRoot, "runtime-pack-admission"))],
                AllowedContents = ["current admitted runtime pack attribution evidence", "local admission summary"],
                ForbiddenContents = ["registry rollout history", "publication tooling state", "automatic activation drift"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 4, MaxOnlineBytes = 128 * 1024 },
                Summary = "Runtime-local pack admission remains a bounded governed mirror over validated pack and attribution contracts.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "runtime_pack_selection_evidence",
                DisplayName = "Runtime pack selection evidence",
                ArtifactClass = RuntimeArtifactClass.GovernedMirror,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                CompactEligible = true,
                Roots =
                [
                    ToRepoRelative(Path.Combine(paths.ArtifactsRoot, "runtime-pack-selection")),
                    ToRepoRelative(Path.Combine(paths.ArtifactsRoot, "runtime-pack-selection", "history")),
                ],
                AllowedContents = ["current local runtime pack selection", "append-only local selection history", "selection summary over admitted evidence"],
                ForbiddenContents = ["registry rollout history", "multi-pack inventory drift", "publication tooling state"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 20, MaxOnlineBytes = 256 * 1024, HotWindowCount = 20, MaxAgeDays = 30 },
                Summary = "Runtime-local pack selection remains a bounded governed mirror over currently admitted pack evidence and append-only local switch history.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "runtime_pack_switch_policy_evidence",
                DisplayName = "Runtime pack switch policy evidence",
                ArtifactClass = RuntimeArtifactClass.GovernedMirror,
                RetentionMode = RuntimeArtifactRetentionMode.SingleVersion,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                Roots = [ToRepoRelative(Path.Combine(paths.ArtifactsRoot, "runtime-pack-switch-policy"))],
                AllowedContents = ["current local pack switch policy", "current local pack pin state", "switch-policy summary over current selection truth"],
                ForbiddenContents = ["registry rollout policy", "remote assignment state", "multi-pack orchestration"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 4, MaxOnlineBytes = 128 * 1024 },
                Summary = "Runtime-local pack switch policy remains a bounded governed mirror over the current local selection and explicit local pin state.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "runtime_pack_admission_policy_evidence",
                DisplayName = "Runtime pack admission policy evidence",
                ArtifactClass = RuntimeArtifactClass.GovernedMirror,
                RetentionMode = RuntimeArtifactRetentionMode.SingleVersion,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                Roots = [ToRepoRelative(Path.Combine(paths.ArtifactsRoot, "runtime-pack-admission-policy"))],
                AllowedContents = ["current local pack admission policy", "local admission-policy summary"],
                ForbiddenContents = ["registry rollout policy", "remote policy bundles", "automatic activation drift"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 4, MaxOnlineBytes = 128 * 1024 },
                Summary = "Runtime-local pack admission policy remains a bounded governed mirror over current explicit local policy truth.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "runtime_pack_selection_audit_evidence",
                DisplayName = "Runtime pack selection audit evidence",
                ArtifactClass = RuntimeArtifactClass.OperationalHistory,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                CompactEligible = true,
                Roots = [ToRepoRelative(Path.Combine(paths.ArtifactsRoot, "runtime-pack-selection-audit"))],
                AllowedContents = ["append-only local pack switch audit entries", "bounded rollback decision evidence", "selection audit summaries"],
                ForbiddenContents = ["registry rollout ledgers", "publication tooling state", "multi-pack orchestration state"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 40, MaxOnlineBytes = 256 * 1024, HotWindowCount = 20, MaxAgeDays = 45 },
                Summary = "Runtime-local pack switch and rollback audit evidence stays online as a compact summary-first history, separate from current selection truth.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "runtime_pack_policy_audit_evidence",
                DisplayName = "Runtime pack policy audit evidence",
                ArtifactClass = RuntimeArtifactClass.OperationalHistory,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                CompactEligible = true,
                Roots = [ToRepoRelative(Path.Combine(paths.ArtifactsRoot, "runtime-pack-policy-audit"))],
                AllowedContents = ["append-only local pack policy audit entries", "export and import audit summaries", "pin and clear policy audit entries"],
                ForbiddenContents = ["registry rollout ledgers", "remote policy bundles", "automatic remediation state"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 40, MaxOnlineBytes = 256 * 1024, HotWindowCount = 20, MaxAgeDays = 45 },
                Summary = "Runtime-local pack policy audit evidence stays online as compact summary-first history over export, import, pin, and clear operations.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "runtime_pack_policy_preview_evidence",
                DisplayName = "Runtime pack policy preview evidence",
                ArtifactClass = RuntimeArtifactClass.GovernedMirror,
                RetentionMode = RuntimeArtifactRetentionMode.SingleVersion,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                Roots = [ToRepoRelative(Path.Combine(paths.ArtifactsRoot, "runtime-pack-policy-preview"))],
                AllowedContents = ["current local pack policy preview", "bounded diff between incoming package and current local truth"],
                ForbiddenContents = ["automatic apply state", "registry rollout planning", "remote sync state"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 4, MaxOnlineBytes = 128 * 1024 },
                Summary = "Runtime-local pack policy preview stays a bounded governed mirror over the latest incoming package diff without mutating current truth.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "context_pack_projection",
                DisplayName = "Context pack projection",
                ArtifactClass = RuntimeArtifactClass.DerivedTruth,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                RebuildEligible = true,
                CompactEligible = true,
                Roots = [ToRepoRelative(Path.Combine(paths.RuntimeRoot, "context-packs"))],
                AllowedContents = ["task context packs", "planner context packs", "projection summaries"],
                ForbiddenContents = ["full memory dumps", "raw build logs", "archived trace detail"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 160, MaxOnlineBytes = 1024 * 1024, HotWindowCount = 20, MaxAgeDays = 14 },
                Summary = "Context packs are rebuildable projections and now keep only a narrow online hot window before older task and planner packs archive.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "execution_packet_mirror",
                DisplayName = "Execution packet mirrors",
                ArtifactClass = RuntimeArtifactClass.GovernedMirror,
                RetentionMode = RuntimeArtifactRetentionMode.RollingWindow,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                CompactEligible = true,
                Roots = [ToRepoRelative(Path.Combine(paths.RuntimeRoot, "execution-packets"))],
                AllowedContents = ["compiled execution packets", "repo mirror of authoritative packet truth"],
                ForbiddenContents = ["manual edits as sole truth", "raw worker logs"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 120, MaxOnlineBytes = 1024 * 1024, HotWindowCount = 12, MaxAgeDays = 14 },
                Summary = "Execution packets remain inspectable governed mirrors over authoritative truth, but only a narrow online packet window stays local before archive.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "sustainability_projection",
                DisplayName = "Sustainability projections",
                ArtifactClass = RuntimeArtifactClass.DerivedTruth,
                RetentionMode = RuntimeArtifactRetentionMode.SingleVersion,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                RebuildEligible = true,
                Roots = [ToRepoRelative(RuntimeArtifactCatalogService.GetSustainabilityRoot(paths))],
                AllowedContents = ["artifact catalog", "audit projection", "compaction and archive summaries"],
                ForbiddenContents = ["archived operational history detail", "tmp residue", "manual edits as sole truth"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 20, MaxOnlineBytes = 2 * 1024 * 1024 },
                Summary = "Sustainability reports are derived projections that should stay small and summary-readable.",
            },
        ];
    }
}
