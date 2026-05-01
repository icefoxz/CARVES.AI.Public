using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeProductPilotProofService
{
    public const string PhaseDocumentPath = RuntimeProductClosureMetadata.CurrentDocumentPath;
    public const string PilotGuideDocumentPath = "docs/guides/CARVES_PRODUCTIZED_PILOT_GUIDE.md";
    public const string PilotStatusDocumentPath = "docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeProductPilotProofService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeProductPilotProofSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-17-product-pilot-proof.md", "Product closure Phase 17 product pilot proof document", errors);
        ValidateRuntimeDocument(PhaseDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidateRuntimeDocument(RuntimeLocalDistHandoffService.PhaseDocumentPath, "Product closure Phase 16 local dist handoff document", errors);
        ValidateRuntimeDocument(RuntimeTargetCommitClosureService.PhaseDocumentPath, "Product closure Phase 15 target commit closure document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistFreshnessSmokeService.PhaseDocumentPath, "Product closure Phase 22 local dist freshness smoke document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistFreshnessSmokeService.GuideDocumentPath, "Local dist freshness smoke guide document", errors);
        ValidateRuntimeDocument(RuntimeFrozenDistTargetReadbackProofService.PhaseDocumentPath, "Product closure Phase 23 frozen dist target readback proof document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md", "Product closure Phase 24 wrapper runtime root binding document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md", "Product closure Phase 25 external target product proof closure document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-26a-product-closure-projection-cleanup.md", "Product closure Phase 26A projection cleanup document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md", "Product closure Phase 26 real external repo pilot document", errors);
        ValidateRuntimeDocument(RuntimeTargetResiduePolicyService.PhaseDocumentPath, "Product closure Phase 27 external target residue policy document", errors);
        ValidateRuntimeDocument(RuntimeTargetIgnoreDecisionPlanService.PhaseDocumentPath, "Product closure Phase 28 target ignore decision plan document", errors);
        ValidateRuntimeDocument(RuntimeTargetIgnoreDecisionRecordService.DecisionRecordPhaseDocumentPath, "Product closure Phase 29 target ignore decision record document", errors);
        ValidateRuntimeDocument(RuntimeTargetIgnoreDecisionRecordService.AuditPhaseDocumentPath, "Product closure Phase 30 target ignore decision record audit document", errors);
        ValidateRuntimeDocument(RuntimeTargetIgnoreDecisionRecordService.CommitReadbackPhaseDocumentPath, "Product closure Phase 31 target ignore decision record commit readback document", errors);
        ValidateRuntimeDocument(RuntimeFrozenDistTargetReadbackProofService.GuideDocumentPath, "Frozen dist target readback proof guide document", errors);
        ValidateRuntimeDocument(RuntimeTargetDistBindingPlanService.PhaseDocumentPath, "Product closure Phase 21 target dist binding plan document", errors);
        ValidateRuntimeDocument(RuntimeTargetDistBindingPlanService.GuideDocumentPath, "Target dist binding plan guide document", errors);
        ValidateRuntimeDocument(PilotGuideDocumentPath, "Productized pilot guide document", errors);
        ValidateRuntimeDocument(PilotStatusDocumentPath, "Productized pilot status document", errors);

        var localDistFreshnessSmoke = new RuntimeLocalDistFreshnessSmokeService(repoRoot).Build();
        var targetDistBindingPlan = new RuntimeTargetDistBindingPlanService(repoRoot).Build();
        var localDistHandoff = new RuntimeLocalDistHandoffService(repoRoot).Build();
        var frozenDistTargetReadbackProof = new RuntimeFrozenDistTargetReadbackProofService(repoRoot).Build();
        var targetCommitClosure = new RuntimeTargetCommitClosureService(repoRoot).Build();
        var targetResiduePolicy = new RuntimeTargetResiduePolicyService(repoRoot).Build();
        var targetIgnoreDecisionPlan = new RuntimeTargetIgnoreDecisionPlanService(repoRoot).Build();
        var targetIgnoreDecisionRecord = new RuntimeTargetIgnoreDecisionRecordService(repoRoot).Build();
        errors.AddRange(localDistFreshnessSmoke.Errors.Select(static error => $"runtime-local-dist-freshness-smoke: {error}"));
        errors.AddRange(targetDistBindingPlan.Errors.Select(static error => $"runtime-target-dist-binding-plan: {error}"));
        errors.AddRange(localDistHandoff.Errors.Select(static error => $"runtime-local-dist-handoff: {error}"));
        errors.AddRange(frozenDistTargetReadbackProof.Errors.Select(static error => $"runtime-frozen-dist-target-readback-proof: {error}"));
        errors.AddRange(targetCommitClosure.Errors.Select(static error => $"runtime-target-commit-closure: {error}"));
        errors.AddRange(targetResiduePolicy.Errors.Select(static error => $"runtime-target-residue-policy: {error}"));
        errors.AddRange(targetIgnoreDecisionPlan.Errors.Select(static error => $"runtime-target-ignore-decision-plan: {error}"));
        errors.AddRange(targetIgnoreDecisionRecord.Errors.Select(static error => $"runtime-target-ignore-decision-record: {error}"));

        var proofComplete = errors.Count == 0
                            && localDistFreshnessSmoke.LocalDistFreshnessSmokeReady
                            && frozenDistTargetReadbackProof.FrozenDistTargetReadbackProofComplete
                            && localDistHandoff.StableExternalConsumptionReady
                            && targetCommitClosure.CommitClosureComplete
                            && targetCommitClosure.OperatorReviewRequiredPathCount == 0
                            && targetResiduePolicy.ProductProofCanRemainComplete
                            && targetIgnoreDecisionPlan.IgnoreDecisionPlanReady
                            && targetIgnoreDecisionRecord.DecisionRecordReady;

        var gaps = BuildGaps(localDistFreshnessSmoke, frozenDistTargetReadbackProof, localDistHandoff, targetCommitClosure, targetResiduePolicy, targetIgnoreDecisionPlan, targetIgnoreDecisionRecord, proofComplete).ToArray();

        return new RuntimeProductPilotProofSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            PreviousPhaseDocumentPath = RuntimeProductClosureMetadata.PreviousDocumentPath,
            PilotGuideDocumentPath = PilotGuideDocumentPath,
            PilotStatusDocumentPath = PilotStatusDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolvePosture(errors.Count, localDistFreshnessSmoke, frozenDistTargetReadbackProof, localDistHandoff, targetCommitClosure, targetResiduePolicy, targetIgnoreDecisionPlan, targetIgnoreDecisionRecord, proofComplete),
            ProductPilotProofComplete = proofComplete,
            LocalDistFreshnessSmokePosture = localDistFreshnessSmoke.OverallPosture,
            LocalDistFreshnessSmokeReady = localDistFreshnessSmoke.LocalDistFreshnessSmokeReady,
            LocalDistFreshnessSmokeSourceCommit = localDistFreshnessSmoke.SourceGitHead,
            LocalDistHandoffPosture = localDistHandoff.OverallPosture,
            StableExternalConsumptionReady = localDistHandoff.StableExternalConsumptionReady,
            RuntimeRootKind = localDistHandoff.RuntimeRootKind,
            RuntimeDistManifestVersion = localDistHandoff.ManifestVersion,
            RuntimeDistManifestSourceCommit = localDistHandoff.ManifestSourceCommit,
            FrozenDistTargetReadbackProofPosture = frozenDistTargetReadbackProof.OverallPosture,
            FrozenDistTargetReadbackProofComplete = frozenDistTargetReadbackProof.FrozenDistTargetReadbackProofComplete,
            TargetCommitClosurePosture = targetCommitClosure.OverallPosture,
            TargetCommitPlanPosture = targetCommitClosure.CommitPlanPosture,
            TargetResiduePolicyPosture = targetResiduePolicy.OverallPosture,
            TargetIgnoreDecisionPlanPosture = targetIgnoreDecisionPlan.OverallPosture,
            TargetIgnoreDecisionRecordPosture = targetIgnoreDecisionRecord.OverallPosture,
            CommitPlanId = targetCommitClosure.CommitPlanId,
            RuntimeInitialized = targetCommitClosure.RuntimeInitialized,
            GitRepositoryDetected = targetCommitClosure.GitRepositoryDetected,
            TargetGitWorktreeClean = targetCommitClosure.TargetGitWorktreeClean,
            TargetCommitClosureComplete = targetCommitClosure.CommitClosureComplete,
            TargetResiduePolicyReady = targetResiduePolicy.ResiduePolicyReady,
            ProductProofCanRemainCompleteWithResidue = targetResiduePolicy.ProductProofCanRemainComplete,
            TargetIgnoreDecisionPlanReady = targetIgnoreDecisionPlan.IgnoreDecisionPlanReady,
            TargetIgnoreDecisionRecordReady = targetIgnoreDecisionRecord.DecisionRecordReady,
            TargetIgnoreDecisionRecordAuditReady = targetIgnoreDecisionRecord.RecordAuditReady,
            TargetIgnoreDecisionRecordCommitReady = targetIgnoreDecisionRecord.DecisionRecordCommitReady,
            IgnoreDecisionRequired = targetIgnoreDecisionPlan.IgnoreDecisionRequired,
            CanApplyIgnoreAfterReview = targetIgnoreDecisionPlan.CanApplyIgnoreAfterReview,
            CanStage = targetCommitClosure.CanStage,
            StagePathCount = targetCommitClosure.StagePathCount,
            ExcludedPathCount = targetCommitClosure.ExcludedPathCount,
            OperatorReviewRequiredPathCount = targetCommitClosure.OperatorReviewRequiredPathCount,
            SuggestedIgnoreEntryCount = targetResiduePolicy.SuggestedIgnoreEntryCount,
            MissingIgnoreEntryCount = targetIgnoreDecisionPlan.MissingIgnoreEntryCount,
            RequiredIgnoreDecisionEntryCount = targetIgnoreDecisionRecord.RequiredDecisionEntryCount,
            RecordedIgnoreDecisionEntryCount = targetIgnoreDecisionRecord.RecordedDecisionEntryCount,
            MissingIgnoreDecisionEntryCount = targetIgnoreDecisionRecord.MissingDecisionEntryCount,
            InvalidIgnoreDecisionRecordCount = targetIgnoreDecisionRecord.InvalidRecordCount,
            MalformedIgnoreDecisionRecordCount = targetIgnoreDecisionRecord.MalformedRecordCount,
            ConflictingIgnoreDecisionEntryCount = targetIgnoreDecisionRecord.ConflictingDecisionEntryCount,
            UncommittedIgnoreDecisionRecordCount = targetIgnoreDecisionRecord.UncommittedDecisionRecordCount,
            StagePaths = targetCommitClosure.StagePaths,
            ExcludedPaths = targetCommitClosure.ExcludedPaths,
            OperatorReviewRequiredPaths = targetCommitClosure.OperatorReviewRequiredPaths,
            SuggestedIgnoreEntries = targetResiduePolicy.SuggestedIgnoreEntries,
            MissingIgnoreEntries = targetIgnoreDecisionPlan.MissingIgnoreEntries,
            MissingIgnoreDecisionEntries = targetIgnoreDecisionRecord.MissingDecisionEntries,
            IgnoreDecisionRecordIds = targetIgnoreDecisionRecord.DecisionRecordIds,
            InvalidIgnoreDecisionRecordPaths = targetIgnoreDecisionRecord.InvalidDecisionRecordPaths,
            MalformedIgnoreDecisionRecordPaths = targetIgnoreDecisionRecord.MalformedDecisionRecordPaths,
            ConflictingIgnoreDecisionEntries = targetIgnoreDecisionRecord.ConflictingDecisionEntries,
            UncommittedIgnoreDecisionRecordPaths = targetIgnoreDecisionRecord.UncommittedDecisionRecordPaths,
            RequiredReadbackCommands =
            [
                "carves pilot dist-smoke --json",
                "carves pilot dist-binding --json",
                "carves pilot dist --json",
                "carves pilot target-proof --json",
                "carves pilot commit-plan --json",
                "carves pilot closure --json",
                "carves pilot residue --json",
                "carves pilot ignore-plan --json",
                "carves pilot ignore-record --json",
                "carves pilot proof --json",
            ],
            Gaps = gaps,
            Summary = BuildSummary(errors.Count, localDistFreshnessSmoke, frozenDistTargetReadbackProof, localDistHandoff, targetCommitClosure, targetResiduePolicy, targetIgnoreDecisionPlan, targetIgnoreDecisionRecord, proofComplete),
            RecommendedNextAction = BuildRecommendedNextAction(errors.Count, localDistFreshnessSmoke, frozenDistTargetReadbackProof, localDistHandoff, targetCommitClosure, targetResiduePolicy, targetIgnoreDecisionPlan, targetIgnoreDecisionRecord, proofComplete),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not initialize, plan, review, write back, stage, commit, push, tag, release, pack, copy, repair, or retarget anything.",
                "This surface aggregates local dist freshness, frozen dist target readback, local dist handoff, and target commit closure into one external-project pilot proof posture.",
                "This surface does not claim public package distribution, signed release manifests, automatic update channels, OS sandboxing, full ACP, full MCP, or remote worker orchestration.",
                "This surface does not replace operator review of target commit contents or downstream release policy.",
            ],
        };
    }

    private static IEnumerable<string> BuildGaps(
        RuntimeLocalDistFreshnessSmokeSurface localDistFreshnessSmoke,
        RuntimeFrozenDistTargetReadbackProofSurface frozenDistTargetReadbackProof,
        RuntimeLocalDistHandoffSurface localDistHandoff,
        RuntimeTargetCommitClosureSurface targetCommitClosure,
        RuntimeTargetResiduePolicySurface targetResiduePolicy,
        RuntimeTargetIgnoreDecisionPlanSurface targetIgnoreDecisionPlan,
        RuntimeTargetIgnoreDecisionRecordSurface targetIgnoreDecisionRecord,
        bool proofComplete)
    {
        if (proofComplete)
        {
            yield break;
        }

        if (!localDistFreshnessSmoke.LocalDistFreshnessSmokeReady)
        {
            yield return "local_dist_freshness_smoke_not_ready";
        }

        if (!frozenDistTargetReadbackProof.FrozenDistTargetReadbackProofComplete)
        {
            yield return "frozen_dist_target_readback_proof_not_complete";
        }

        if (!localDistHandoff.StableExternalConsumptionReady)
        {
            yield return "stable_external_consumption_not_ready";
        }

        if (!targetCommitClosure.RuntimeInitialized)
        {
            yield return "runtime_not_initialized";
        }

        if (!targetCommitClosure.GitRepositoryDetected)
        {
            yield return "target_git_repository_not_detected";
        }

        if (!targetCommitClosure.CommitClosureComplete)
        {
            yield return "target_commit_closure_not_complete";
        }

        if (targetCommitClosure.CanStage)
        {
            yield return "target_commit_plan_stage_paths_pending";
        }

        if (targetCommitClosure.OperatorReviewRequiredPathCount > 0)
        {
            yield return "operator_review_required_paths_remain";
        }

        if (targetCommitClosure.ExcludedPathCount > 0 && !targetCommitClosure.CommitClosureComplete)
        {
            yield return "excluded_paths_remain";
        }

        if (!targetResiduePolicy.ProductProofCanRemainComplete)
        {
            yield return "target_residue_policy_not_ready";
        }

        if (!targetIgnoreDecisionPlan.IgnoreDecisionPlanReady)
        {
            yield return "target_ignore_decision_plan_not_ready";
        }

        if (!targetIgnoreDecisionRecord.DecisionRecordReady)
        {
            yield return "target_ignore_decision_record_not_ready";
        }

        foreach (var gap in localDistFreshnessSmoke.Gaps)
        {
            yield return $"local_dist_freshness_smoke:{gap}";
        }

        foreach (var gap in frozenDistTargetReadbackProof.Gaps)
        {
            yield return $"frozen_dist_target_readback_proof:{gap}";
        }

        foreach (var gap in localDistHandoff.Gaps)
        {
            yield return $"local_dist_handoff:{gap}";
        }

        foreach (var gap in targetCommitClosure.Gaps)
        {
            yield return $"target_commit_closure:{gap}";
        }

        foreach (var gap in targetResiduePolicy.Gaps)
        {
            yield return $"target_residue_policy:{gap}";
        }

        foreach (var gap in targetIgnoreDecisionPlan.Gaps)
        {
            yield return $"target_ignore_decision_plan:{gap}";
        }

        foreach (var gap in targetIgnoreDecisionRecord.Gaps)
        {
            yield return $"target_ignore_decision_record:{gap}";
        }
    }

    private static string ResolvePosture(
        int errorCount,
        RuntimeLocalDistFreshnessSmokeSurface localDistFreshnessSmoke,
        RuntimeFrozenDistTargetReadbackProofSurface frozenDistTargetReadbackProof,
        RuntimeLocalDistHandoffSurface localDistHandoff,
        RuntimeTargetCommitClosureSurface targetCommitClosure,
        RuntimeTargetResiduePolicySurface targetResiduePolicy,
        RuntimeTargetIgnoreDecisionPlanSurface targetIgnoreDecisionPlan,
        RuntimeTargetIgnoreDecisionRecordSurface targetIgnoreDecisionRecord,
        bool proofComplete)
    {
        if (errorCount > 0)
        {
            return "product_pilot_proof_blocked_by_surface_gaps";
        }

        if (proofComplete)
        {
            return targetCommitClosure.ExcludedPathCount > 0
                ? "product_pilot_proof_complete_with_local_residue"
                : "product_pilot_proof_complete";
        }

        if (!localDistFreshnessSmoke.LocalDistFreshnessSmokeReady)
        {
            return "product_pilot_proof_waiting_for_local_dist_freshness_smoke";
        }

        if (!frozenDistTargetReadbackProof.FrozenDistTargetReadbackProofComplete)
        {
            return "product_pilot_proof_waiting_for_frozen_dist_target_readback_proof";
        }

        if (!localDistHandoff.StableExternalConsumptionReady)
        {
            return "product_pilot_proof_waiting_for_local_dist_handoff";
        }

        if (!targetCommitClosure.GitRepositoryDetected)
        {
            return "product_pilot_proof_blocked_by_git_repo_gap";
        }

        if (!targetCommitClosure.RuntimeInitialized)
        {
            return "product_pilot_proof_blocked_by_runtime_init";
        }

        if (targetCommitClosure.OperatorReviewRequiredPathCount > 0)
        {
            return "product_pilot_proof_blocked_by_operator_review_required";
        }

        if (targetCommitClosure.CanStage)
        {
            return "product_pilot_proof_waiting_for_target_commit";
        }

        if (targetCommitClosure.ExcludedPathCount > 0)
        {
            if (!targetResiduePolicy.ProductProofCanRemainComplete)
            {
                return "product_pilot_proof_waiting_for_operator_residue_policy";
            }

            if (!targetIgnoreDecisionPlan.IgnoreDecisionPlanReady)
            {
                return "product_pilot_proof_waiting_for_operator_ignore_decision_plan";
            }

            return targetIgnoreDecisionRecord.DecisionRecordReady
                ? "product_pilot_proof_complete_with_local_residue"
                : "product_pilot_proof_waiting_for_operator_ignore_decision_record";
        }

        return "product_pilot_proof_blocked_by_commit_closure";
    }

    private static string BuildSummary(
        int errorCount,
        RuntimeLocalDistFreshnessSmokeSurface localDistFreshnessSmoke,
        RuntimeFrozenDistTargetReadbackProofSurface frozenDistTargetReadbackProof,
        RuntimeLocalDistHandoffSurface localDistHandoff,
        RuntimeTargetCommitClosureSurface targetCommitClosure,
        RuntimeTargetResiduePolicySurface targetResiduePolicy,
        RuntimeTargetIgnoreDecisionPlanSurface targetIgnoreDecisionPlan,
        RuntimeTargetIgnoreDecisionRecordSurface targetIgnoreDecisionRecord,
        bool proofComplete)
    {
        if (errorCount > 0)
        {
            return "Product pilot proof cannot be trusted until required phase documents and dependent surfaces are valid.";
        }

        if (proofComplete)
        {
            return targetResiduePolicy.ResiduePathCount > 0
                ? targetIgnoreDecisionPlan.IgnoreDecisionRequired
                    ? "The target repo is bound to a frozen local Runtime dist, target commit closure is complete, only excluded local/tooling residue remains, and operator ignore decisions are durably recorded."
                    : "The target repo is bound to a frozen local Runtime dist, target commit closure is complete, and only excluded local/tooling residue remains."
                : "The target repo is bound to a frozen local Runtime dist and target commit closure is complete.";
        }

        if (!localDistFreshnessSmoke.LocalDistFreshnessSmokeReady)
        {
            return "Product pilot proof is waiting for the local Runtime dist freshness smoke to prove the frozen dist matches the clean Runtime source HEAD.";
        }

        if (!frozenDistTargetReadbackProof.FrozenDistTargetReadbackProofComplete)
        {
            return "Product pilot proof is waiting for the external target repo to prove it is initialized, bootstrapped, and bound to the fresh frozen Runtime dist.";
        }

        if (!localDistHandoff.StableExternalConsumptionReady)
        {
            return "Product pilot proof is waiting for the target repo to consume Runtime from a frozen local dist root.";
        }

        if (targetCommitClosure.CanStage)
        {
            return "Product pilot proof is waiting for the operator-reviewed target product proof commit.";
        }

        if (targetCommitClosure.OperatorReviewRequiredPathCount > 0)
        {
            return "Product pilot proof is blocked because operator-review paths remain before commit closure.";
        }

        if (!targetIgnoreDecisionRecord.DecisionRecordReady)
        {
            return targetIgnoreDecisionRecord.Summary;
        }

        return targetCommitClosure.Summary;
    }

    private static string BuildRecommendedNextAction(
        int errorCount,
        RuntimeLocalDistFreshnessSmokeSurface localDistFreshnessSmoke,
        RuntimeFrozenDistTargetReadbackProofSurface frozenDistTargetReadbackProof,
        RuntimeLocalDistHandoffSurface localDistHandoff,
        RuntimeTargetCommitClosureSurface targetCommitClosure,
        RuntimeTargetResiduePolicySurface targetResiduePolicy,
        RuntimeTargetIgnoreDecisionPlanSurface targetIgnoreDecisionPlan,
        RuntimeTargetIgnoreDecisionRecordSurface targetIgnoreDecisionRecord,
        bool proofComplete)
    {
        if (errorCount > 0)
        {
            return "Restore current product pilot proof docs and dependent surfaces, then rerun carves pilot proof --json.";
        }

        if (proofComplete)
        {
            return "product_pilot_proof_readback_complete";
        }

        if (!localDistFreshnessSmoke.LocalDistFreshnessSmokeReady)
        {
            return "Run carves pilot dist-smoke --json and follow its recommended next action before treating the local Runtime dist as current.";
        }

        if (!frozenDistTargetReadbackProof.FrozenDistTargetReadbackProofComplete)
        {
            return "Run carves pilot target-proof --json and follow its recommended next action before treating the target as a stable frozen-dist external consumer.";
        }

        if (!localDistHandoff.StableExternalConsumptionReady)
        {
            return "Run carves pilot dist --json and follow its recommended next action before treating the target as stable external consumption.";
        }

        if (targetCommitClosure.CanStage)
        {
            return "Run carves pilot commit-plan --json, stage only stage_paths, commit with an operator-reviewed message, rerun carves pilot closure --json, then rerun carves pilot proof --json.";
        }

        if (targetCommitClosure.OperatorReviewRequiredPathCount > 0)
        {
            return "Review operator_review_required_paths before any git add or commit, then rerun carves pilot proof --json.";
        }

        if (!targetResiduePolicy.ProductProofCanRemainComplete)
        {
            return "Run carves pilot residue --json and resolve its gaps before declaring product pilot proof complete.";
        }

        if (!targetIgnoreDecisionPlan.IgnoreDecisionPlanReady)
        {
            return "Run carves pilot ignore-plan --json and resolve its gaps before declaring product pilot proof complete.";
        }

        if (!targetIgnoreDecisionRecord.DecisionRecordReady)
        {
            return "Run carves pilot ignore-record --json, then record an operator decision with carves pilot record-ignore-decision keep_local --all --reason <reason> if local residue is accepted.";
        }

        return targetCommitClosure.RecommendedNextAction;
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
