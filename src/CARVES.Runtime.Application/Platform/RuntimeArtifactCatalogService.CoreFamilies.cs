using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeArtifactCatalogService
{
    private RuntimeArtifactFamilyPolicy[] BuildCoreTruthAndMirrorFamilies()
    {
        return
        [
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "task_truth",
                DisplayName = "Task truth",
                ArtifactClass = RuntimeArtifactClass.CanonicalTruth,
                RetentionMode = RuntimeArtifactRetentionMode.Permanent,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                Roots = [ToRepoRelative(paths.TasksRoot)],
                AllowedContents = ["task graph", "task nodes", "card markdown"],
                ForbiddenContents = ["raw stdout", "raw stderr", "validation traces", "tmp residue"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 5000, MaxOnlineBytes = 10 * 1024 * 1024 },
                Summary = "Governed card and task truth stays small, stable, and summary-readable.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "memory_truth",
                DisplayName = "Memory truth",
                ArtifactClass = RuntimeArtifactClass.CanonicalTruth,
                RetentionMode = RuntimeArtifactRetentionMode.Permanent,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                Roots =
                [
                    ToRepoRelative(Path.Combine(paths.AiRoot, "memory", "architecture")),
                    ToRepoRelative(Path.Combine(paths.AiRoot, "memory", "modules")),
                    ToRepoRelative(Path.Combine(paths.AiRoot, "memory", "patterns")),
                    ToRepoRelative(Path.Combine(paths.AiRoot, "memory", "project")),
                ],
                AllowedContents = ["architecture memory", "module memory", "pattern memory", "project memory"],
                ForbiddenContents = ["raw execution logs", "trace ledgers", "build artifacts"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 500, MaxOnlineBytes = 5 * 1024 * 1024 },
                Summary = "Long-lived architecture, module, pattern, and project memory remain canonical truth but should not be bulk-loaded by default.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "execution_memory_truth",
                DisplayName = "Execution memory truth",
                ArtifactClass = RuntimeArtifactClass.CanonicalTruth,
                RetentionMode = RuntimeArtifactRetentionMode.Permanent,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.OnDemandDetail,
                Roots = [ToRepoRelative(Path.Combine(paths.AiRoot, "memory", "execution"))],
                AllowedContents = ["execution outcome memory", "approval memory", "patch references"],
                ForbiddenContents = ["raw stdout", "raw stderr", "tmp residue"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 1000, MaxOnlineBytes = 12 * 1024 * 1024 },
                Summary = "Execution memory is governed machine-readable truth for planner review, not ad-hoc runtime residue.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "routing_truth",
                DisplayName = "Routing and qualification truth",
                ArtifactClass = RuntimeArtifactClass.CanonicalTruth,
                RetentionMode = RuntimeArtifactRetentionMode.Permanent,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                Roots =
                [
                    ToRepoRelative(paths.PlatformActiveRoutingProfileFile),
                    ToRepoRelative(paths.PlatformCandidateRoutingProfileFile),
                    ToRepoRelative(paths.PlatformQualificationMatrixFile),
                ],
                AllowedContents = ["routing profiles", "qualification matrix", "promotion truth"],
                ForbiddenContents = ["raw worker transcripts", "raw validation logs"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 20, MaxOnlineBytes = 2 * 1024 * 1024 },
                Summary = "Active routing and qualification truth is canonical and operator-readable.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "platform_definition_truth",
                DisplayName = "Platform definition truth",
                ArtifactClass = RuntimeArtifactClass.CanonicalTruth,
                RetentionMode = RuntimeArtifactRetentionMode.Permanent,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                Roots =
                [
                    ToRepoRelative(paths.ConfigRoot),
                    ToRepoRelative(paths.PlatformPoliciesRoot),
                    ToRepoRelative(paths.PlatformReposRoot),
                    ToRepoRelative(Path.Combine(paths.PlatformRoot, "host", "descriptor.json")),
                ],
                AllowedContents = ["repo-local config", "platform policies", "stable repo registries", "host descriptor"],
                ForbiddenContents = ["session timestamps", "host health snapshots", "worker live state drift", "local temp repo churn"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 100, MaxOnlineBytes = 2 * 1024 * 1024 },
                Summary = "Stable config, policy, and registry definitions stay versioned even while adjacent live state drifts.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "validation_suite_truth",
                DisplayName = "Validation suite truth",
                ArtifactClass = RuntimeArtifactClass.CanonicalTruth,
                RetentionMode = RuntimeArtifactRetentionMode.Permanent,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                Roots = [ToRepoRelative(Path.Combine(paths.AiRoot, "validation", "tasks"))],
                AllowedContents = ["validation task definitions", "validation catalogs"],
                ForbiddenContents = ["per-run trace detail", "archived history"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 100, MaxOnlineBytes = 512 * 1024 },
                Summary = "Validation suite definitions are stable truth, not rolling run history.",
            },
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "governed_markdown_mirror",
                DisplayName = "Governed markdown mirrors",
                ArtifactClass = RuntimeArtifactClass.GovernedMirror,
                RetentionMode = RuntimeArtifactRetentionMode.SingleVersion,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                Roots =
                [
                    ToRepoRelative(Path.Combine(paths.AiRoot, "CURRENT_TASK.md")),
                    ToRepoRelative(Path.Combine(paths.AiRoot, "STATE.md")),
                    ToRepoRelative(Path.Combine(paths.AiRoot, "TASK_QUEUE.md")),
                ],
                AllowedContents = ["human-readable mirror", "summary projection", "generated collaboration view"],
                ForbiddenContents = ["authoritative-only fields", "raw execution detail", "manual edits as sole truth"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 3, MaxOnlineBytes = 2 * 1024 * 1024 },
                Summary = "Markdown surfaces are governed mirrors over JSON truth and should stay summary-first, versioned, and regenerable.",
            },
        ];
    }
}
