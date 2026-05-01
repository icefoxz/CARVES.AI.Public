using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeControlledGovernanceProofService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;
    private readonly RoleGovernanceRuntimePolicy roleGovernancePolicy;

    public RuntimeControlledGovernanceProofService(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        RoleGovernanceRuntimePolicy roleGovernancePolicy)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.paths = paths;
        this.systemConfig = systemConfig;
        this.roleGovernancePolicy = roleGovernancePolicy;
    }

    public RuntimeControlledGovernanceProofSurface Build(string? laneId = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var boundaryDocumentPath = ToRepoRelative(Path.Combine(repoRoot, "docs", "runtime", "runtime-controlled-governance-proof-integration.md"));
        var boundaryDocumentFullPath = Path.Combine(repoRoot, boundaryDocumentPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(boundaryDocumentFullPath))
        {
            errors.Add($"Boundary document '{boundaryDocumentPath}' is missing.");
        }

        var handoff = new RuntimeValidationLabProofHandoffService(repoRoot, paths, systemConfig, roleGovernancePolicy).Build();
        errors.AddRange(handoff.Errors.Select(error => $"ValidationLab handoff: {error}"));
        warnings.AddRange(handoff.Warnings.Select(warning => $"ValidationLab handoff: {warning}"));

        var lanes = BuildLanes(handoff, errors);
        if (!string.IsNullOrWhiteSpace(laneId))
        {
            lanes = lanes
                .Where(candidate => string.Equals(candidate.LaneId, laneId, StringComparison.Ordinal))
                .ToArray();
            if (lanes.Length == 0)
            {
                throw new InvalidOperationException($"Runtime controlled governance proof lane '{laneId}' was not found.");
            }
        }

        return new RuntimeControlledGovernanceProofSurface
        {
            BoundaryDocumentPath = boundaryDocumentPath,
            HandoffBoundaryDocumentPath = handoff.BoundaryDocumentPath,
            Summary = "Controlled governance proof remains a bounded Runtime read path that keeps approval, refusal, interruption, and recovery proof queryable from existing Runtime truth and ValidationLab handoff lanes.",
            ControlledModeDefault = roleGovernancePolicy.ControlledModeDefault,
            ProducerCannotSelfApprove = roleGovernancePolicy.ProducerCannotSelfApprove,
            ReviewerCannotApproveSameTask = roleGovernancePolicy.ReviewerCannotApproveSameTask,
            ValidationLabFollowOnLanes = roleGovernancePolicy.ValidationLabFollowOnLanes,
            ControlledModeInvariants =
            [
                "controlled_mode_default stays explicit",
                "producer_cannot_self_approve stays enforced",
                "reviewer_cannot_approve_same_task stays enforced",
                "review-task and approve-review remain the only Runtime truth-affecting review and approval gates",
                "ValidationLab remains downstream read-only choreography, not a Runtime write path",
                "no second proof ledger is introduced for convenience"
            ],
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Lanes = lanes,
        };
    }

    private RuntimeControlledGovernanceProofLaneSurface[] BuildLanes(
        RuntimeValidationLabProofHandoffSurface handoff,
        List<string> errors)
    {
        var approval = ResolveLane(handoff, "approval_proof", errors);
        var recovery = ResolveLane(handoff, "recovery_proof", errors);
        if (approval is null || recovery is null)
        {
            return [];
        }

        return
        [
            BuildLane(
                "approval",
                "Approval proof integration",
                "Approval proof keeps review, approval separation, merge-candidate emission, and permission-decision evidence queryable from Runtime truth.",
                ["approval_proof"],
                "Runtime stays authoritative for task/review/approval truth and approval-separation policy defaults.",
                "Approval proof remains a bounded Runtime projection over task truth, execution memory, review evidence, and permission audit surfaces.",
                CombineCommands(
                    approval.RuntimeCommands,
                    ["review-task <task-id> <verdict> <reason...>"],
                    ["approve-review <task-id> <reason...>"],
                    ["explain-task <task-id>"],
                    ["policy inspect"]),
                approval.RuntimeTruthFamilies,
                approval.RuntimeEvidencePaths,
                [
                    "ValidationLab does not approve reviews.",
                    "Runtime does not duplicate approval truth into a proof-only ledger."
                ]),
            BuildLane(
                "refusal",
                "Refusal proof integration",
                "Refusal proof keeps denied or timed-out permission decisions and blocked review outcomes distinguishable from generic failure semantics.",
                ["approval_proof", "recovery_proof"],
                "Runtime stays authoritative for deny, timeout, and refusal-side audit evidence.",
                "Refusal proof bridges approval surfaces and recovery surfaces without collapsing refusal into an untyped failure bucket.",
                CombineCommands(
                    approval.RuntimeCommands,
                    ["worker approvals"],
                    ["worker deny <permission-request-id> [actor-identity]"],
                    ["worker timeout <permission-request-id> [actor-identity]"]),
                CombineFamilies(approval.RuntimeTruthFamilies, recovery.RuntimeTruthFamilies),
                CombineEvidencePaths(
                    approval.RuntimeEvidencePaths,
                    recovery.RuntimeEvidencePaths,
                    [ToRepoRelative(paths.PlatformPermissionAuditRuntimeFile)]),
                [
                    "Refusal proof does not create a separate refusal truth store.",
                    "ValidationLab remains downstream and does not author refusal outcomes."
                ]),
            BuildLane(
                "interruption",
                "Interruption proof integration",
                "Interruption proof keeps delegated worker timeout, blocked review, and live interruption posture queryable from existing Runtime failure and lifecycle truth.",
                ["recovery_proof"],
                "Runtime stays authoritative for interruption posture, failure detail, and delegated lifecycle evidence.",
                "Interruption proof remains a bounded projection over failure artifacts, runtime ledgers, and live-state summaries.",
                CombineCommands(
                    recovery.RuntimeCommands,
                    ["worker incidents [--task-id <task-id>] [--run-id <run-id>]"],
                    ["inspect execution-run-exceptions"]),
                recovery.RuntimeTruthFamilies,
                recovery.RuntimeEvidencePaths,
                [
                    "Interruption proof does not become a second live-state authority.",
                    "ValidationLab does not own interruption truth."
                ]),
            BuildLane(
                "recovery",
                "Recovery proof integration",
                "Recovery proof keeps retry, quarantine, manual review, and blocked recovery classification queryable from Runtime execution memory and delegated recovery ledgers.",
                ["recovery_proof"],
                "Runtime stays authoritative for recovery classification, recovery action, failure detail, and delegated recovery ledgers.",
                "Recovery proof remains attached to current Runtime truth, bounded evidence packaging, and existing recovery classification docs.",
                CombineCommands(
                    recovery.RuntimeCommands,
                    ["task inspect <task-id> --runs"],
                    ["sync-state"]),
                recovery.RuntimeTruthFamilies,
                recovery.RuntimeEvidencePaths,
                [
                    "Recovery proof does not duplicate delegated recovery truth into a convenience ledger.",
                    "ValidationLab does not become a recovery authority."
                ]),
        ];
    }

    private static RuntimeControlledGovernanceProofLaneSurface BuildLane(
        string laneId,
        string displayName,
        string summary,
        IReadOnlyList<string> sourceHandoffLaneIds,
        string runtimeAuthoritySummary,
        string integrationSummary,
        IReadOnlyList<string> governingCommands,
        IReadOnlyList<RuntimeValidationLabProofFamilySurface> runtimeTruthFamilies,
        IReadOnlyList<string> runtimeEvidencePaths,
        IReadOnlyList<string> nonClaims)
    {
        return new RuntimeControlledGovernanceProofLaneSurface
        {
            LaneId = laneId,
            DisplayName = displayName,
            Summary = summary,
            SourceHandoffLaneIds = sourceHandoffLaneIds,
            RuntimeAuthoritySummary = runtimeAuthoritySummary,
            IntegrationSummary = integrationSummary,
            GoverningCommands = governingCommands,
            RuntimeTruthFamilies = runtimeTruthFamilies,
            RuntimeEvidencePaths = runtimeEvidencePaths,
            NonClaims = nonClaims,
        };
    }

    private static RuntimeValidationLabProofLaneSurface? ResolveLane(
        RuntimeValidationLabProofHandoffSurface handoff,
        string laneId,
        List<string> errors)
    {
        var lane = handoff.Lanes.FirstOrDefault(candidate => string.Equals(candidate.LaneId, laneId, StringComparison.Ordinal));
        if (lane is null)
        {
            errors.Add($"ValidationLab handoff lane '{laneId}' is missing.");
        }

        return lane;
    }

    private static RuntimeValidationLabProofFamilySurface[] CombineFamilies(params IReadOnlyList<RuntimeValidationLabProofFamilySurface>[] families)
    {
        return families
            .SelectMany(items => items)
            .GroupBy(item => item.FamilyId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.FamilyId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] CombineEvidencePaths(params IReadOnlyList<string>[] evidencePathSets)
    {
        return evidencePathSets
            .SelectMany(set => set)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] CombineCommands(params IReadOnlyList<string>[] commandSets)
    {
        return commandSets
            .SelectMany(set => set)
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(command => command, StringComparer.Ordinal)
            .ToArray();
    }

    private string ToRepoRelative(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Path.GetRelativePath(repoRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
