using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeFrozenDistTargetReadbackProofService
{
    public const string PhaseDocumentPath = "docs/runtime/carves-product-closure-phase-23-frozen-dist-target-readback-proof.md";
    public const string PreviousPhaseDocumentPath = RuntimeLocalDistFreshnessSmokeService.PhaseDocumentPath;
    public const string GuideDocumentPath = "docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeFrozenDistTargetReadbackProofService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeFrozenDistTargetReadbackProofSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 23 frozen dist target readback proof document", errors);
        ValidateRuntimeDocument(PreviousPhaseDocumentPath, "Product closure Phase 22 local dist freshness smoke document", errors);
        ValidateRuntimeDocument(GuideDocumentPath, "Frozen dist target readback proof guide document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistFreshnessSmokeService.GuideDocumentPath, "Local dist freshness smoke guide document", errors);
        ValidateRuntimeDocument(RuntimeTargetDistBindingPlanService.PhaseDocumentPath, "Product closure Phase 21 target dist binding plan document", errors);
        ValidateRuntimeDocument(RuntimeTargetDistBindingPlanService.GuideDocumentPath, "Target dist binding plan guide document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistHandoffService.PhaseDocumentPath, "Product closure Phase 16 local dist handoff document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistHandoffService.LocalDistGuideDocumentPath, "Runtime local dist guide document", errors);
        ValidateRuntimeDocument(RuntimeCliInvocationContractService.PhaseDocumentPath, "Product closure Phase 19 CLI invocation contract document", errors);
        ValidateRuntimeDocument(RuntimeCliInvocationContractService.InvocationGuideDocumentPath, "CLI invocation contract guide document", errors);
        ValidateRuntimeDocument(RuntimeCliActivationPlanService.PhaseDocumentPath, "Product closure Phase 20 CLI activation plan document", errors);
        ValidateRuntimeDocument(RuntimeCliActivationPlanService.ActivationGuideDocumentPath, "CLI activation plan guide document", errors);
        ValidateRuntimeDocument(RuntimeTargetAgentBootstrapPackService.GuideDocumentPath, "Target agent bootstrap pack guide document", errors);

        var cliInvocation = new RuntimeCliInvocationContractService(repoRoot).Build();
        var cliActivation = new RuntimeCliActivationPlanService(repoRoot).Build();
        var targetAgentBootstrap = new RuntimeTargetAgentBootstrapPackService(repoRoot).Build(writeRequested: false);
        var localDistFreshnessSmoke = new RuntimeLocalDistFreshnessSmokeService(repoRoot).Build();
        var targetDistBindingPlan = new RuntimeTargetDistBindingPlanService(repoRoot).Build();
        var localDistHandoff = new RuntimeLocalDistHandoffService(repoRoot).Build();
        var targetCommitClosure = new RuntimeTargetCommitClosureService(repoRoot).Build();
        errors.AddRange(cliInvocation.Errors.Select(static error => $"runtime-cli-invocation-contract: {error}"));
        errors.AddRange(cliActivation.Errors.Select(static error => $"runtime-cli-activation-plan: {error}"));
        errors.AddRange(targetAgentBootstrap.Errors.Select(static error => $"runtime-target-agent-bootstrap-pack: {error}"));
        errors.AddRange(localDistFreshnessSmoke.Errors.Select(static error => $"runtime-local-dist-freshness-smoke: {error}"));
        errors.AddRange(targetDistBindingPlan.Errors.Select(static error => $"runtime-target-dist-binding-plan: {error}"));
        errors.AddRange(localDistHandoff.Errors.Select(static error => $"runtime-local-dist-handoff: {error}"));
        errors.AddRange(targetCommitClosure.Errors.Select(static error => $"runtime-target-commit-closure: {error}"));

        var targetAgentBootstrapReady = targetAgentBootstrap.RuntimeInitialized
                                        && targetAgentBootstrap.MissingFiles.Count == 0;
        var proofComplete = errors.Count == 0
                            && cliInvocation.InvocationContractComplete
                            && cliActivation.ActivationPlanComplete
                            && targetAgentBootstrapReady
                            && localDistFreshnessSmoke.LocalDistFreshnessSmokeReady
                            && targetDistBindingPlan.DistBindingPlanComplete
                            && targetDistBindingPlan.TargetBoundToLocalDist
                            && localDistHandoff.StableExternalConsumptionReady
                            && targetCommitClosure.RuntimeInitialized
                            && targetCommitClosure.GitRepositoryDetected;

        var gaps = BuildGaps(
                cliInvocation,
                cliActivation,
                targetAgentBootstrap,
                localDistFreshnessSmoke,
                targetDistBindingPlan,
                localDistHandoff,
                targetCommitClosure,
                targetAgentBootstrapReady,
                proofComplete)
            .ToArray();

        return new RuntimeFrozenDistTargetReadbackProofSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            PreviousPhaseDocumentPath = PreviousPhaseDocumentPath,
            GuideDocumentPath = GuideDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolvePosture(
                errors.Count,
                proofComplete,
                cliInvocation,
                cliActivation,
                targetAgentBootstrapReady,
                localDistFreshnessSmoke,
                targetDistBindingPlan,
                localDistHandoff,
                targetCommitClosure),
            FrozenDistTargetReadbackProofComplete = proofComplete,
            CliInvocationPosture = cliInvocation.OverallPosture,
            CliInvocationContractComplete = cliInvocation.InvocationContractComplete,
            CliActivationPosture = cliActivation.OverallPosture,
            CliActivationPlanComplete = cliActivation.ActivationPlanComplete,
            TargetAgentBootstrapPosture = targetAgentBootstrap.OverallPosture,
            TargetAgentBootstrapReady = targetAgentBootstrapReady,
            LocalDistFreshnessSmokePosture = localDistFreshnessSmoke.OverallPosture,
            LocalDistFreshnessSmokeReady = localDistFreshnessSmoke.LocalDistFreshnessSmokeReady,
            LocalDistFreshnessSmokeSourceCommit = localDistFreshnessSmoke.SourceGitHead,
            TargetDistBindingPlanPosture = targetDistBindingPlan.OverallPosture,
            TargetDistBindingPlanComplete = targetDistBindingPlan.DistBindingPlanComplete,
            TargetBoundToLocalDist = targetDistBindingPlan.TargetBoundToLocalDist,
            TargetDistRecommendedBindingMode = targetDistBindingPlan.RecommendedBindingMode,
            LocalDistHandoffPosture = localDistHandoff.OverallPosture,
            StableExternalConsumptionReady = localDistHandoff.StableExternalConsumptionReady,
            RuntimeRootKind = localDistHandoff.RuntimeRootKind,
            RuntimeDistManifestVersion = localDistHandoff.ManifestVersion,
            RuntimeDistManifestSourceCommit = localDistHandoff.ManifestSourceCommit,
            RuntimeInitialized = targetCommitClosure.RuntimeInitialized,
            GitRepositoryDetected = targetCommitClosure.GitRepositoryDetected,
            TargetGitWorktreeClean = targetCommitClosure.TargetGitWorktreeClean,
            RequiredSourceReadbackCommands =
            [
                "carves pilot dist-smoke --json",
                "carves pilot dist-binding --json",
                "carves pilot target-proof --json",
            ],
            RequiredTargetReadbackCommands =
            [
                "carves pilot invocation --json",
                "carves pilot activation --json",
                "carves pilot dist-smoke --json",
                "carves pilot dist-binding --json",
                "carves pilot dist --json",
                "carves pilot target-proof --json",
                "carves pilot proof --json",
            ],
            BoundaryRules =
            [
                "The proof is read-only; it does not initialize, retarget, repair, stage, commit, pack, copy, or publish anything.",
                "A target proof is complete only when the current target repo is attached to a frozen local Runtime dist, not the live Runtime source tree.",
                "The target must keep Runtime-owned docs in the Runtime dist root; target repos should not copy Runtime doctrine as local truth.",
                "This proof is a stable external-consumption gate, not a claim that target product work or commit closure is complete.",
                "If this proof is incomplete, follow the specific dist-smoke, dist-binding, local-dist, or bootstrap gap before asking an agent to plan or edit.",
            ],
            Gaps = gaps,
            Summary = BuildSummary(
                errors.Count,
                proofComplete,
                cliInvocation,
                cliActivation,
                targetAgentBootstrapReady,
                localDistFreshnessSmoke,
                targetDistBindingPlan,
                localDistHandoff,
                targetCommitClosure),
            RecommendedNextAction = BuildRecommendedNextAction(
                errors.Count,
                proofComplete,
                cliInvocation,
                cliActivation,
                targetAgentBootstrapReady,
                localDistFreshnessSmoke,
                targetDistBindingPlan,
                localDistHandoff,
                targetCommitClosure),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not run the target attach command; the operator must invoke the frozen dist wrapper explicitly.",
                "This surface does not prove target task execution, workspace writeback, product commit closure, remote push, tags, releases, or public package distribution.",
                "This surface does not claim OS sandboxing, signed installers, automatic updates, full ACP, full MCP, or remote worker orchestration.",
                "This surface does not grant agents authority to edit `.ai/runtime.json`, attach-handshake files, shell profiles, PATH, or global aliases.",
            ],
        };
    }

    private static IEnumerable<string> BuildGaps(
        RuntimeCliInvocationContractSurface cliInvocation,
        RuntimeCliActivationPlanSurface cliActivation,
        RuntimeTargetAgentBootstrapPackSurface targetAgentBootstrap,
        RuntimeLocalDistFreshnessSmokeSurface localDistFreshnessSmoke,
        RuntimeTargetDistBindingPlanSurface targetDistBindingPlan,
        RuntimeLocalDistHandoffSurface localDistHandoff,
        RuntimeTargetCommitClosureSurface targetCommitClosure,
        bool targetAgentBootstrapReady,
        bool proofComplete)
    {
        if (proofComplete)
        {
            yield break;
        }

        if (!cliInvocation.InvocationContractComplete)
        {
            yield return "cli_invocation_contract_not_ready";
        }

        if (!cliActivation.ActivationPlanComplete)
        {
            yield return "cli_activation_plan_not_ready";
        }

        if (!targetAgentBootstrap.RuntimeInitialized)
        {
            yield return "runtime_not_initialized";
        }

        if (!targetAgentBootstrapReady)
        {
            yield return "target_agent_bootstrap_not_ready";
        }

        if (!localDistFreshnessSmoke.LocalDistFreshnessSmokeReady)
        {
            yield return "local_dist_freshness_smoke_not_ready";
        }

        if (!targetDistBindingPlan.DistBindingPlanComplete)
        {
            yield return "target_dist_binding_plan_not_ready";
        }

        if (!targetDistBindingPlan.TargetBoundToLocalDist)
        {
            yield return "target_not_bound_to_frozen_local_dist";
        }

        if (!localDistHandoff.StableExternalConsumptionReady)
        {
            yield return "stable_external_consumption_not_ready";
        }

        if (!targetCommitClosure.GitRepositoryDetected)
        {
            yield return "target_git_repository_not_detected";
        }

        foreach (var gap in cliInvocation.Gaps)
        {
            yield return $"cli_invocation:{gap}";
        }

        foreach (var gap in cliActivation.Gaps)
        {
            yield return $"cli_activation:{gap}";
        }

        foreach (var gap in targetAgentBootstrap.MissingFiles)
        {
            yield return $"target_agent_bootstrap_missing:{gap}";
        }

        foreach (var gap in localDistFreshnessSmoke.Gaps)
        {
            yield return $"local_dist_freshness_smoke:{gap}";
        }

        foreach (var gap in targetDistBindingPlan.Gaps)
        {
            yield return $"target_dist_binding_plan:{gap}";
        }

        foreach (var gap in localDistHandoff.Gaps)
        {
            yield return $"local_dist_handoff:{gap}";
        }
    }

    private static string ResolvePosture(
        int errorCount,
        bool proofComplete,
        RuntimeCliInvocationContractSurface cliInvocation,
        RuntimeCliActivationPlanSurface cliActivation,
        bool targetAgentBootstrapReady,
        RuntimeLocalDistFreshnessSmokeSurface localDistFreshnessSmoke,
        RuntimeTargetDistBindingPlanSurface targetDistBindingPlan,
        RuntimeLocalDistHandoffSurface localDistHandoff,
        RuntimeTargetCommitClosureSurface targetCommitClosure)
    {
        if (errorCount > 0)
        {
            return "frozen_dist_target_readback_proof_blocked_by_surface_gaps";
        }

        if (proofComplete)
        {
            return "frozen_dist_target_readback_proof_complete";
        }

        if (!cliInvocation.InvocationContractComplete)
        {
            return "frozen_dist_target_readback_proof_waiting_for_invocation_contract";
        }

        if (!cliActivation.ActivationPlanComplete)
        {
            return "frozen_dist_target_readback_proof_waiting_for_activation_plan";
        }

        if (!targetCommitClosure.RuntimeInitialized)
        {
            return "frozen_dist_target_readback_proof_blocked_by_runtime_init";
        }

        if (!targetAgentBootstrapReady)
        {
            return "frozen_dist_target_readback_proof_waiting_for_target_agent_bootstrap";
        }

        if (!localDistFreshnessSmoke.LocalDistFreshnessSmokeReady)
        {
            return "frozen_dist_target_readback_proof_waiting_for_local_dist_freshness_smoke";
        }

        if (!targetDistBindingPlan.DistBindingPlanComplete)
        {
            return "frozen_dist_target_readback_proof_waiting_for_dist_binding_resources";
        }

        if (!targetDistBindingPlan.TargetBoundToLocalDist || !localDistHandoff.StableExternalConsumptionReady)
        {
            return "frozen_dist_target_readback_proof_waiting_for_frozen_dist_target_binding";
        }

        if (!targetCommitClosure.GitRepositoryDetected)
        {
            return "frozen_dist_target_readback_proof_blocked_by_git_repo_gap";
        }

        return "frozen_dist_target_readback_proof_blocked";
    }

    private static string BuildSummary(
        int errorCount,
        bool proofComplete,
        RuntimeCliInvocationContractSurface cliInvocation,
        RuntimeCliActivationPlanSurface cliActivation,
        bool targetAgentBootstrapReady,
        RuntimeLocalDistFreshnessSmokeSurface localDistFreshnessSmoke,
        RuntimeTargetDistBindingPlanSurface targetDistBindingPlan,
        RuntimeLocalDistHandoffSurface localDistHandoff,
        RuntimeTargetCommitClosureSurface targetCommitClosure)
    {
        if (errorCount > 0)
        {
            return "Frozen dist target readback proof cannot be trusted until required Phase 23 resources and dependent surfaces are valid.";
        }

        if (proofComplete)
        {
            return "The current external target repo is initialized, bootstrapped, and bound to a fresh frozen local Runtime dist.";
        }

        if (!cliInvocation.InvocationContractComplete || !cliActivation.ActivationPlanComplete)
        {
            return "Invocation and activation readbacks must be valid before the target proof can be trusted.";
        }

        if (!targetCommitClosure.RuntimeInitialized)
        {
            return "The current repo is not initialized as a CARVES target yet.";
        }

        if (!targetAgentBootstrapReady)
        {
            return "The current target repo is missing agent bootstrap files.";
        }

        if (!localDistFreshnessSmoke.LocalDistFreshnessSmokeReady)
        {
            return localDistFreshnessSmoke.Summary;
        }

        if (!targetDistBindingPlan.TargetBoundToLocalDist || !localDistHandoff.StableExternalConsumptionReady)
        {
            return "The current target repo is not yet bound to the fresh frozen local Runtime dist.";
        }

        if (!targetCommitClosure.GitRepositoryDetected)
        {
            return "The current target proof must run from a git repository.";
        }

        return localDistHandoff.Summary;
    }

    private static string BuildRecommendedNextAction(
        int errorCount,
        bool proofComplete,
        RuntimeCliInvocationContractSurface cliInvocation,
        RuntimeCliActivationPlanSurface cliActivation,
        bool targetAgentBootstrapReady,
        RuntimeLocalDistFreshnessSmokeSurface localDistFreshnessSmoke,
        RuntimeTargetDistBindingPlanSurface targetDistBindingPlan,
        RuntimeLocalDistHandoffSurface localDistHandoff,
        RuntimeTargetCommitClosureSurface targetCommitClosure)
    {
        if (errorCount > 0)
        {
            return "Restore Phase 23 proof resources and dependent surface docs, then rerun carves pilot target-proof --json.";
        }

        if (proofComplete)
        {
            return "frozen_dist_target_readback_proof_complete";
        }

        if (!cliInvocation.InvocationContractComplete)
        {
            return cliInvocation.RecommendedNextAction;
        }

        if (!cliActivation.ActivationPlanComplete)
        {
            return cliActivation.RecommendedNextAction;
        }

        if (!targetCommitClosure.RuntimeInitialized)
        {
            return "From the external target repo, run the frozen dist wrapper init command reported by carves pilot dist-binding --json.";
        }

        if (!targetAgentBootstrapReady)
        {
            return "carves agent bootstrap --write";
        }

        if (!localDistFreshnessSmoke.LocalDistFreshnessSmokeReady)
        {
            return localDistFreshnessSmoke.RecommendedNextAction;
        }

        if (!targetDistBindingPlan.TargetBoundToLocalDist || !localDistHandoff.StableExternalConsumptionReady)
        {
            return targetDistBindingPlan.RecommendedNextAction;
        }

        if (!targetCommitClosure.GitRepositoryDetected)
        {
            return "Run this command from the attached external target git repository.";
        }

        return localDistHandoff.RecommendedNextAction;
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
