using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeGitNativeCodingLoopService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;

    public RuntimeGitNativeCodingLoopService(string repoRoot, ControlPlanePaths paths)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
    }

    public RuntimeGitNativeCodingLoopSurface Build()
    {
        var policy = LoadOrCreatePolicy();
        return new RuntimeGitNativeCodingLoopSurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformGitNativeCodingLoopFile),
            Policy = policy,
        };
    }

    public GitNativeCodingLoopPolicy LoadPolicy()
    {
        return LoadOrCreatePolicy();
    }

    private GitNativeCodingLoopPolicy LoadOrCreatePolicy()
    {
        if (File.Exists(paths.PlatformGitNativeCodingLoopFile))
        {
            var persisted = JsonSerializer.Deserialize<GitNativeCodingLoopPolicy>(
                                File.ReadAllText(paths.PlatformGitNativeCodingLoopFile),
                                JsonOptions)
                            ?? BuildDefaultPolicy();
            if (NeedsRefresh(persisted))
            {
                var refreshed = BuildDefaultPolicy();
                File.WriteAllText(paths.PlatformGitNativeCodingLoopFile, JsonSerializer.Serialize(refreshed, JsonOptions));
                return refreshed;
            }

            return persisted;
        }

        var policy = BuildDefaultPolicy();
        Directory.CreateDirectory(paths.PlatformPoliciesRoot);
        File.WriteAllText(paths.PlatformGitNativeCodingLoopFile, JsonSerializer.Serialize(policy, JsonOptions));
        return policy;
    }

    private GitNativeCodingLoopPolicy BuildDefaultPolicy()
    {
        return new GitNativeCodingLoopPolicy
        {
            Summary = "Machine-readable git-native coding loop boundary derived from Aider: repo-map projection, patch-first interaction, and edit evidence loops with git, lint, and test signals, while keeping Host governance and CARVES truth writeback stronger than the coding loop.",
            ExtractionBoundary = new GitNativeCodingLoopExtractionBoundary
            {
                DirectAbsorptions =
                [
                    "repo map as a codebase context compressor",
                    "patch-first and diff-first interaction style",
                    "git-native commit loop with commit-message projection",
                    "lint and test evidence after each edit round",
                ],
                TranslatedIntoCarves =
                [
                    "runtime-git-native-coding-loop policy truth under .carves-platform/policies/",
                    "repo-map projection attached to runtime-code-understanding-engine rather than replacing .ai/codegraph/",
                    "patch-first interaction routed through task overlays, review-task, and merge-candidate artifacts",
                    "git, lint, and test evidence attached as governed review evidence instead of lifecycle truth",
                ],
                RejectedAnchors =
                [
                    "replacing Host review and truth writeback with a direct git loop",
                    "treating repo map as a replacement for .ai/codegraph truth",
                    "equating lint or test pass with lifecycle completion",
                ],
            },
            RepoMapProjection = new GitNativeRepoMapProjectionBoundary
            {
                Summary = "Repo map remains a projection layer that compresses codebase context for coding loops while staying downstream from codegraph-first truth.",
                SourceCapabilities =
                [
                    "repository-scale context summary",
                    "ranked or narrowed file visibility",
                    "map refresh after code movement",
                ],
                CarvesTranslations =
                [
                    "runtime-code-understanding-engine as stronger structural substrate",
                    "task overlays and execution packets as bounded context entry points",
                    "repo map as an operator-readable projection rather than canonical symbol truth",
                ],
                StrongerTruthDependencies =
                [
                    RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformCodeUnderstandingEngineFile),
                    ".ai/codegraph/",
                ],
                NonGoals =
                [
                    "semantic ownership of definitions, references, or implementations",
                    "replacement of codegraph-first truth with a summary view",
                ],
            },
            PatchFirstInteraction = new GitNativePatchFirstInteractionBoundary
            {
                Summary = "Patch-first interaction is allowed as a bounded worker and review style, but it remains subordinate to task inspection, verification, review, and truth writeback.",
                InteractionSteps =
                [
                    "inspect scope and current task truth",
                    "propose patch or diff candidate",
                    "apply bounded edits",
                    "run targeted verification",
                    "surface evidence for review and writeback",
                ],
                RequiredGates =
                [
                    "task inspect <task-id>",
                    "task run <task-id>",
                    "review-task <task-id> <verdict> <reason...>",
                    "approve-review <task-id> <reason...>",
                    "sync-state",
                ],
                ReviewExpectations =
                [
                    "patch is reviewable by path and evidence",
                    "scope remains bounded to overlay and task truth",
                    "verification precedes truth writeback",
                ],
                RejectedDrift =
                [
                    "direct lifecycle completion from a patch artifact",
                    "silent broad rewrite without task-bound scope",
                    "diff-first interaction that bypasses review-task",
                ],
            },
            EvidenceLoop = new GitNativeEvidenceLoopBoundary
            {
                Summary = "Git commits, lint results, and test results are supporting evidence for review and remediation loops, not the sole source of lifecycle completion or task truth.",
                EvidenceKinds =
                [
                    "git diff summary",
                    "git commit message projection",
                    "lint result",
                    "test result",
                    "repair loop follow-up evidence",
                ],
                SupportingUses =
                [
                    "attach review evidence to task and merge-candidate artifacts",
                    "explain why a patch candidate is acceptable or blocked",
                    "support remediation loops after failed verification",
                ],
                NonTruthUses =
                [
                    "git commit as final truth writeback",
                    "lint pass as boundary gate completion",
                    "test pass as task lifecycle completion",
                ],
                Notes =
                [
                    "Evidence is strongest when attached to review-task and approval surfaces.",
                    "Aider-style auto-commit is absorbed here as an evidence-loop reference, not a new lifecycle owner.",
                ],
            },
            ConcernFamilies =
            [
                Family(
                    "repo_map_projection",
                    "projection",
                    "Repo map compresses large codebase context but stays downstream from codegraph truth.",
                    ["repo map / codebase summary", "ranked file context"],
                    [RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformCodeUnderstandingEngineFile), ".ai/codegraph/"],
                    ["inspect runtime-code-understanding-engine", "inspect runtime-git-native-coding-loop"],
                    ["repo map remains projection-only"],
                    ["repo map as canonical structure truth"]),
                Family(
                    "patch_first_interaction",
                    "interaction",
                    "Patch-first interaction is a valid bounded workflow when it remains subordinate to task overlays, verification, and review writeback.",
                    ["patch-first and diff-first interaction", "small, reviewable edit rounds"],
                    ["runtime-agent-task-overlay", "review-task", ".ai/artifacts/merge-candidates/"],
                    ["task inspect <task-id>", "task run <task-id>", "review-task <task-id> <verdict> <reason...>"],
                    ["verification precedes writeback"],
                    ["direct completion from patch application"]),
                Family(
                    "git_native_commit_evidence_loop",
                    "evidence_loop",
                    "Git-native commit loops may summarize edit rounds and evidence, but remain downstream from Host-routed lifecycle truth.",
                    ["automatic commit message projection", "diff summary after each edit round"],
                    [".ai/artifacts/reviews/", ".ai/artifacts/merge-candidates/", "review-task"],
                    ["review-task <task-id> <verdict> <reason...>", "approve-review <task-id> <reason...>"],
                    ["commit artifacts support review, not truth writeback"],
                    ["git loop as lifecycle owner"]),
                Family(
                    "lint_test_evidence_loop",
                    "evidence_loop",
                    "Lint and test outputs are strong supporting evidence after each edit round, but they do not by themselves complete the boundary gate.",
                    ["per-round lint evidence", "per-round test evidence", "repair-on-failure follow-up"],
                    [".ai/artifacts/reviews/", ".ai/failures/", "execution-packet"],
                    ["task run <task-id>", "review-task <task-id> <verdict> <reason...>"],
                    ["lint/test evidence feeds review and remediation"],
                    ["lint/test pass as lifecycle completion"]),
                Family(
                    "qualification_without_host_replacement",
                    "qualification",
                    "Aider-derived git-native coding loops are absorbed only when Host governance, TaskGraph truth, and truth writeback stay stronger.",
                    ["git-native edit loop as bounded worker aid", "operator-readable evidence chain"],
                    [RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformGitNativeCodingLoopFile), RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.TaskGraphFile)],
                    ["inspect runtime-git-native-coding-loop", "review-task", "sync-state"],
                    ["host governance remains stronger than git loop convenience"],
                    ["host governance replacement"]),
            ],
            TruthRoots =
            [
                Root(
                    "git_native_coding_loop_policy_truth",
                    "canonical_truth",
                    "Machine-readable git-native coding loop policy truth is the upstream owner for repo-map projection, patch-first interaction, and git-native evidence loop boundaries.",
                    [RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformGitNativeCodingLoopFile)],
                    ["inspect runtime-git-native-coding-loop", "api runtime-git-native-coding-loop"]),
                Root(
                    "codegraph_and_repo_map_dependency",
                    "stronger_truth_dependency",
                    "Repo-map projection remains downstream from stronger code-understanding and codegraph truth.",
                    [RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformCodeUnderstandingEngineFile), ".ai/codegraph/"],
                    ["inspect runtime-code-understanding-engine"]),
                Root(
                    "task_review_writeback_truth",
                    "stronger_truth_dependency",
                    "Task graph, execution memory, review-task, and sync-state remain stronger lifecycle truth than git commits or patch artifacts.",
                    [RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.TaskGraphFile), ".ai/memory/execution/", ".ai/tasks/"],
                    ["task run <task-id>", "review-task", "sync-state"]),
                Root(
                    "git_and_verification_evidence",
                    "operational_history",
                    "Git commit metadata, lint/test outputs, and merge-candidate artifacts remain supporting evidence for review loops.",
                    [".ai/artifacts/reviews/", ".ai/artifacts/merge-candidates/", ".ai/failures/"],
                    ["approve-review", "InspectReviewArtifact"]),
            ],
            BoundaryRules =
            [
                Rule(
                    "repo_map_remains_projection_not_codegraph_truth",
                    "Repo map is a projection and must not replace codegraph-first truth ownership.",
                    [RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformCodeUnderstandingEngineFile), ".ai/codegraph/", "repo_map_projection"],
                    ["repo_map_as_codegraph_truth"]),
                Rule(
                    "patch_first_interaction_stays_reviewable_and_bounded",
                    "Patch-first interaction remains valid only when edits stay reviewable, bounded, and subordinate to review-task and writeback gates.",
                    ["task inspect <task-id>", "task run <task-id>", "review-task"],
                    ["patch_first_bypasses_review_gate", "patch_first_expands_scope_silently"]),
                Rule(
                    "git_commit_loop_does_not_replace_host_writeback",
                    "Git-native commit loops may summarize edit rounds but never replace Host-routed truth writeback.",
                    ["git diff summary", "git commit message projection", "sync-state"],
                    ["git_commit_as_truth_writeback", "direct_lifecycle_completion_from_git_commit"]),
                Rule(
                    "lint_test_evidence_supports_but_does_not_complete",
                    "Lint and test outputs support review and remediation, but do not by themselves satisfy the boundary gate or completion truth.",
                    ["lint result", "test result", "review-task"],
                    ["lint_test_pass_as_completion_truth"]),
                Rule(
                    "host_governance_replacement_is_rejected",
                    "The git-native coding loop remains subordinate to Host governance, TaskGraph truth, and approval/writeback surfaces.",
                    ["task run <task-id>", "review-task", "approve-review", "sync-state"],
                    ["host_governance_replacement"]),
            ],
            GovernedReadPaths =
            [
                ReadPath(
                    "git_native_coding_loop_surface",
                    "inspect runtime-git-native-coding-loop",
                    "Read the machine-readable git-native coding loop surface before proposing repo-map, patch-first, or git evidence loop changes.",
                    [RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformGitNativeCodingLoopFile)]),
                ReadPath(
                    "repo_map_dependency_surface",
                    "inspect runtime-code-understanding-engine",
                    "Repo-map projection proposals must start from the stronger code-understanding and codegraph-first boundary.",
                    [RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformCodeUnderstandingEngineFile), ".ai/codegraph/"]),
                ReadPath(
                    "task_overlay_and_patch_path",
                    "task inspect <task-id> | inspect runtime-agent-task-overlay <task-id>",
                    "Patch-first interaction stays task-scoped and overlay-bounded before any edit or diff proposal.",
                    [".ai/tasks/", "runtime-agent-task-overlay"]),
                ReadPath(
                    "review_and_writeback_path",
                    "review-task <task-id> <verdict> <reason...> | approve-review <task-id> <reason...> | sync-state",
                    "Git, lint, and test evidence become meaningful only when routed through review and writeback surfaces.",
                    [".ai/artifacts/reviews/", ".ai/artifacts/merge-candidates/", "sync-state"]),
            ],
            Qualification = new GitNativeCodingLoopQualificationLine
            {
                Summary = "CARVES may absorb Aider-derived git-native coding loop ideas when repo maps stay projection-only, patch-first remains reviewable and bounded, and git/lint/test outputs remain supporting evidence under Host governance.",
                SuccessCriteria =
                [
                    "Repo map is explicitly projection-only and downstream from runtime-code-understanding-engine.",
                    "Patch-first interaction is routed through task inspection, review-task, and sync-state rather than direct lifecycle completion.",
                    "Git commit, lint, and test outputs are visible as supporting evidence, not as completion truth owners.",
                ],
                RejectedDirections =
                [
                    "repo_map_as_codegraph_truth",
                    "git_commit_as_truth_writeback",
                    "host_governance_replacement",
                ],
                DeferredDirections =
                [
                    "provider-specific auto-commit behavior remains projection-specific",
                    "automatic branch publishing remains outside this slice",
                ],
                StopConditions =
                [
                    "Do not replace Host review or sync-state with a direct git loop.",
                    "Do not let repo map become the substitute for codegraph-first truth.",
                    "Do not treat lint/test pass as completion truth.",
                ],
            },
            ReadinessMap =
            [
                Ready("repo_map_projection", "ready_for_bounded_followup", "Repo-map projection is ready as a bounded context-compression surface downstream from codegraph truth.", ["inspect runtime-git-native-coding-loop", "inspect runtime-code-understanding-engine"]),
                Ready("patch_first_interaction", "ready_for_bounded_followup", "Patch-first interaction is ready as a bounded, reviewable workflow style.", ["task inspect <task-id>", "review-task"]),
                Ready("git_native_commit_evidence_loop", "ready_for_bounded_followup", "Git-native commit summaries are ready as supporting evidence inside review and merge-candidate surfaces.", ["review-task", ".ai/artifacts/merge-candidates/"]),
                Ready("lint_test_evidence_loop", "ready_for_bounded_followup", "Lint and test evidence loops are ready as bounded repair and review support signals.", [".ai/artifacts/reviews/", ".ai/failures/"]),
                Ready("repo_map_as_codegraph_truth", "rejected", "Repo map must not replace codegraph-first truth ownership.", ["runtime-code-understanding-engine"]),
                Ready("git_commit_as_truth_writeback", "rejected", "Git commit loops remain evidence-only and may not replace truth writeback.", ["sync-state", "review-task"]),
                Ready("host_governance_replacement", "rejected", "Host governance replacement is explicitly out of bounds for this slice.", ["task run <task-id>", "review-task", "sync-state"]),
            ],
            Notes =
            [
                "Aider is treated here as a git-native coding loop reference, not as a replacement control plane.",
                "The repo-map slice complements runtime-code-understanding-engine rather than displacing it.",
            ],
        };
    }

    private static bool NeedsRefresh(GitNativeCodingLoopPolicy policy)
    {
        return policy.PolicyVersion < 1
               || policy.ConcernFamilies.Length < 5
               || policy.TruthRoots.Length < 4
               || policy.BoundaryRules.Length < 5
               || policy.GovernedReadPaths.Length < 4
               || policy.ReadinessMap.Length < 7
               || !policy.ReadinessMap.Any(item => string.Equals(item.SemanticId, "host_governance_replacement", StringComparison.Ordinal) && string.Equals(item.Readiness, "rejected", StringComparison.Ordinal))
               || !policy.ReadinessMap.Any(item => string.Equals(item.SemanticId, "repo_map_as_codegraph_truth", StringComparison.Ordinal) && string.Equals(item.Readiness, "rejected", StringComparison.Ordinal))
               || !policy.ReadinessMap.Any(item => string.Equals(item.SemanticId, "git_commit_as_truth_writeback", StringComparison.Ordinal) && string.Equals(item.Readiness, "rejected", StringComparison.Ordinal));
    }

    private static GitNativeCodingLoopConcernFamily Family(
        string familyId,
        string layer,
        string summary,
        string[] directAbsorptions,
        string[] translationTargets,
        string[] governedEntryPoints,
        string[] reviewBoundaries,
        string[] outOfScope,
        string[]? notes = null)
    {
        return new GitNativeCodingLoopConcernFamily
        {
            FamilyId = familyId,
            Layer = layer,
            Summary = summary,
            CurrentStatus = "ready_for_bounded_followup",
            SourceProjects = ["Aider"],
            DirectAbsorptions = directAbsorptions,
            TranslationTargets = translationTargets,
            GovernedEntryPoints = governedEntryPoints,
            ReviewBoundaries = reviewBoundaries,
            OutOfScope = outOfScope,
            Notes = notes ?? [],
        };
    }

    private static RuntimeKernelTruthRootDescriptor Root(string rootId, string classification, string summary, string[] pathRefs, string[] truthRefs, string[]? notes = null)
    {
        return new RuntimeKernelTruthRootDescriptor
        {
            RootId = rootId,
            Classification = classification,
            Summary = summary,
            PathRefs = pathRefs,
            TruthRefs = truthRefs,
            Notes = notes ?? [],
        };
    }

    private static RuntimeKernelBoundaryRule Rule(string ruleId, string summary, string[] allowedRefs, string[] forbiddenRefs, string[]? notes = null)
    {
        return new RuntimeKernelBoundaryRule
        {
            RuleId = ruleId,
            Summary = summary,
            AllowedRefs = allowedRefs,
            ForbiddenRefs = forbiddenRefs,
            Notes = notes ?? [],
        };
    }

    private static RuntimeKernelGovernedReadPath ReadPath(string pathId, string entryPoint, string summary, string[] truthRefs, string[]? notes = null)
    {
        return new RuntimeKernelGovernedReadPath
        {
            PathId = pathId,
            EntryPoint = entryPoint,
            Summary = summary,
            TruthRefs = truthRefs,
            Notes = notes ?? [],
        };
    }

    private static GitNativeCodingLoopReadinessEntry Ready(string semanticId, string readiness, string summary, string[] governedFollowUp, string[]? notes = null)
    {
        return new GitNativeCodingLoopReadinessEntry
        {
            SemanticId = semanticId,
            Readiness = readiness,
            Summary = summary,
            GovernedFollowUp = governedFollowUp,
            Notes = notes ?? [],
        };
    }
}
