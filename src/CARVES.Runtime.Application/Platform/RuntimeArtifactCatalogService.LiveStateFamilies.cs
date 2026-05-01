using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeArtifactCatalogService
{
    private RuntimeArtifactFamilyPolicy[] BuildLiveStateAndArchiveFamilies(string worktreeRoot)
    {
        return
        [
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "platform_provider_definition_truth",
                DisplayName = "Platform provider definition truth",
                ArtifactClass = RuntimeArtifactClass.CanonicalTruth,
                RetentionMode = RuntimeArtifactRetentionMode.Permanent,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                Roots =
                [
                    ToRepoRelative(paths.PlatformProviderRegistryFile),
                    ToRepoRelative(paths.PlatformProvidersRoot),
                ],
                AllowedContents = ["provider definitions", "provider registry snapshots", "stable backend routing definitions"],
                ForbiddenContents = ["embedded health probes", "quota drift", "machine-local timestamps"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 100, MaxOnlineBytes = 4 * 1024 * 1024 },
                Summary = "Provider definitions and registry truth stay versioned while volatile provider health and quota drift move onto live-state surfaces.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "platform_provider_live_state",
                DisplayName = "Platform provider live state",
                ArtifactClass = RuntimeArtifactClass.LiveState,
                RetentionMode = RuntimeArtifactRetentionMode.SingleVersion,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                Roots = [ToRepoRelative(paths.PlatformProviderLiveStateRoot)],
                AllowedContents = ["provider health snapshots", "provider quota usage"],
                ForbiddenContents = ["stable provider definition truth", "archived history"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 10, MaxOnlineBytes = 1024 * 1024 },
                Summary = "Provider health and quota drift stay inspectable as live state without rewriting the versioned provider definition surface.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "runtime_live_state",
                DisplayName = "Runtime live state",
                ArtifactClass = RuntimeArtifactClass.LiveState,
                RetentionMode = RuntimeArtifactRetentionMode.SingleVersion,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                Roots =
                [
                    ToRepoRelative(paths.RuntimeSessionFile),
                    ToRepoRelative(paths.RuntimeWorktreeStateFile),
                    ToRepoRelative(paths.RuntimeManagedWorkspaceLeaseStateFile),
                    ToRepoRelative(paths.RuntimeFailureFile),
                ],
                AllowedContents = ["current session posture", "active worktree state", "managed workspace lease state", "latest failure pointer"],
                ForbiddenContents = ["release truth", "archived history", "long-lived config"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 4, MaxOnlineBytes = 1024 * 1024 },
                Summary = "Runtime live state now lives under an explicit live-state root so session posture, worktree state, managed workspace lease state, and latest failure pointers stop masquerading as generic runtime files.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "platform_live_state",
                DisplayName = "Platform live state",
                ArtifactClass = RuntimeArtifactClass.LiveState,
                RetentionMode = RuntimeArtifactRetentionMode.SingleVersion,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                Roots =
                [
                    ToRepoRelative(paths.PlatformFleetLiveStateRoot),
                    ToRepoRelative(paths.PlatformSessionLiveStateRoot),
                    ToRepoRelative(paths.PlatformWorkerLiveStateRoot),
                    ToRepoRelative(paths.PlatformHostStateRoot),
                    ToRepoRelative(paths.PlatformDelegationLiveStateRoot),
                    ToRepoRelative(paths.PlatformWorkerRoutingOverridesFile),
                    ToRepoRelative(Path.Combine(paths.PlatformRuntimeStateRoot, "control-plane-locks")),
                ],
                AllowedContents = ["fleet discovery", "actor sessions", "worker live state", "host snapshots", "delegated run live state", "runtime overrides", "control-plane locks"],
                ForbiddenContents = ["stable policy truth", "manual release docs", "archived evidence"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 50, MaxOnlineBytes = 4 * 1024 * 1024 },
                Summary = "Platform live state now saves behind explicit runtime-local roots so machine-local drift stays inspectable without dirtying tracked config surfaces.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "incident_audit_archive",
                DisplayName = "Incident and audit archive",
                ArtifactClass = RuntimeArtifactClass.AuditArchive,
                RetentionMode = RuntimeArtifactRetentionMode.ArchiveSummary,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.ArchiveOnly,
                Roots =
                [
                    ToRepoRelative(paths.PlatformEventRuntimeRoot),
                    ToRepoRelative(Path.Combine(paths.PlatformEventRuntimeRoot, "details")),
                ],
                AllowedContents = ["incident summaries", "audit events"],
                ForbiddenContents = ["default planner context", "derived codegraph shards"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 2000, MaxOnlineBytes = 16 * 1024 * 1024 },
                Summary = "Platform incident and audit event history stays available for review without polluting the default online read path.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "sustainability_archive",
                DisplayName = "Sustainability archive",
                ArtifactClass = RuntimeArtifactClass.AuditArchive,
                RetentionMode = RuntimeArtifactRetentionMode.ArchiveSummary,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.ArchiveOnly,
                Roots = [ToRepoRelative(Path.Combine(RuntimeArtifactCatalogService.GetSustainabilityRoot(paths), "archive"))],
                AllowedContents = ["compacted operational history references", "archive index"],
                ForbiddenContents = ["canonical task truth", "default planner context", "live state"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 4000, MaxOnlineBytes = 32 * 1024 * 1024 },
                Summary = "Compacted operational-history detail stays behind archive references instead of returning to the default online read path.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "ephemeral_runtime_residue",
                DisplayName = "Ephemeral runtime residue",
                ArtifactClass = RuntimeArtifactClass.EphemeralResidue,
                RetentionMode = RuntimeArtifactRetentionMode.AutoExpire,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Hidden,
                CleanupEligible = true,
                Roots =
                [
                    ToRepoRelative(Path.Combine(paths.AiRoot, "codegraph", "tmp")),
                    ToRepoRelative(Path.Combine(paths.RuntimeRoot, "tmp")),
                    ToRepoRelative(Path.Combine(paths.RuntimeRoot, "staging")),
                    ToRepoRelative(Path.Combine(paths.ArtifactsRoot, "tmp")),
                    ToRepoRelative(paths.PlatformRuntimeStateRoot),
                    ToRepoRelative(Path.Combine(repoRoot, ".carves-temp")),
                    ToRepoRelative(worktreeRoot),
                ],
                AllowedContents = ["temporary scans", "staging files", "worktree residue"],
                ForbiddenContents = ["canonical truth", "durable summaries", "archived history"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 0, MaxOnlineBytes = 0, MaxAgeDays = 1 },
                Summary = "Ephemeral residue should be pruned, not archived or read by default, including runtime-state atomic write temp spill.",
            },
        ];
    }
}
