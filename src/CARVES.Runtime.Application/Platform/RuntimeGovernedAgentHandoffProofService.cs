using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeGovernedAgentHandoffProofService
{
    private const string ProofDocumentPath = "docs/runtime/runtime-governed-agent-handoff-proof.md";
    private const string SessionGatewayDocumentPath = "docs/session-gateway/governed-agent-handoff-proof.md";
    private const string ProductClosureBaselineDocumentPath = "docs/runtime/carves-product-closure-phase-0-baseline.md";
    private const string ProductClosurePhase1DocumentPath = "docs/runtime/carves-product-closure-phase-1-cli-distribution.md";
    private const string ProductClosurePhase2DocumentPath = "docs/runtime/carves-product-closure-phase-2-readiness-separation.md";
    private const string ProductClosurePhase3DocumentPath = "docs/runtime/carves-product-closure-phase-3-minimal-init-onboarding.md";
    private const string ProductClosurePhase4DocumentPath = "docs/runtime/carves-product-closure-phase-4-external-target-dogfood-proof.md";
    private const string ProductClosurePhase5DocumentPath = "docs/runtime/carves-product-closure-phase-5-real-project-pilot.md";
    private const string ProductClosurePhase6DocumentPath = "docs/runtime/carves-product-closure-phase-6-official-truth-writeback.md";
    private const string ProductClosurePhase7DocumentPath = "docs/runtime/carves-product-closure-phase-7-managed-workspace-execution.md";
    private const string ProductClosurePhase8DocumentPath = "docs/runtime/carves-product-closure-phase-8-managed-workspace-writeback.md";
    private const string ProductClosurePhase9DocumentPath = "docs/runtime/carves-product-closure-phase-9-productized-pilot-guide.md";
    private const string ProductClosurePhase10DocumentPath = "docs/runtime/carves-product-closure-phase-10-productized-pilot-status.md";
    private const string ProductClosurePhase11BDocumentPath = "docs/runtime/carves-product-closure-phase-11b-target-agent-bootstrap-pack.md";
    private const string ProductClosurePhase12DocumentPath = "docs/runtime/carves-product-closure-phase-12-existing-target-bootstrap-repair.md";
    private const string ProductClosurePhase13DocumentPath = "docs/runtime/carves-product-closure-phase-13-target-commit-hygiene.md";
    private const string ProductClosurePhase14DocumentPath = "docs/runtime/carves-product-closure-phase-14-target-commit-plan.md";
    private const string ProductClosurePhase15DocumentPath = "docs/runtime/carves-product-closure-phase-15-target-commit-closure.md";
    private const string ProductClosurePhase16DocumentPath = "docs/runtime/carves-product-closure-phase-16-local-dist-handoff.md";
    private const string ProductClosurePhase17DocumentPath = "docs/runtime/carves-product-closure-phase-17-product-pilot-proof.md";
    private const string ProductClosurePhase18DocumentPath = "docs/runtime/carves-product-closure-phase-18-external-consumer-resource-pack.md";
    private const string ProductClosurePhase19DocumentPath = RuntimeCliInvocationContractService.PhaseDocumentPath;
    private const string ProductClosurePhase20DocumentPath = RuntimeCliActivationPlanService.PhaseDocumentPath;
    private const string ProductClosurePhase21DocumentPath = RuntimeTargetDistBindingPlanService.PhaseDocumentPath;
    private const string ProductClosurePhase22DocumentPath = RuntimeLocalDistFreshnessSmokeService.PhaseDocumentPath;
    private const string ProductClosurePhase23DocumentPath = RuntimeFrozenDistTargetReadbackProofService.PhaseDocumentPath;
    private const string ProductClosurePhase24DocumentPath = "docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md";
    private const string ProductClosurePhase25DocumentPath = "docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md";
    private const string ProductClosurePhase26ADocumentPath = "docs/runtime/carves-product-closure-phase-26a-product-closure-projection-cleanup.md";
    private const string ProductClosurePhase26DocumentPath = "docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md";
    private const string ProductClosurePhase27DocumentPath = RuntimeTargetResiduePolicyService.PhaseDocumentPath;
    private const string ProductClosurePhase28DocumentPath = RuntimeTargetIgnoreDecisionPlanService.PhaseDocumentPath;
    private const string ProductClosurePhase29DocumentPath = RuntimeTargetIgnoreDecisionRecordService.DecisionRecordPhaseDocumentPath;
    private const string ProductClosurePhase30DocumentPath = RuntimeTargetIgnoreDecisionRecordService.AuditPhaseDocumentPath;
    private const string ProductClosurePhase31DocumentPath = RuntimeTargetIgnoreDecisionRecordService.CommitReadbackPhaseDocumentPath;
    private const string ProductClosurePhase32DocumentPath = "docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md";
    private const string ProductClosurePhase33DocumentPath = "docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md";
    private const string ProductClosurePhase34DocumentPath = "docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md";
    private const string ProductClosurePhase35DocumentPath = "docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md";
    private const string ProductClosurePhase36DocumentPath = "docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md";
    private const string ProductClosurePhase37DocumentPath = RuntimeAgentProblemFollowUpDecisionPlanService.Phase37DocumentPath;
    private const string ProductClosurePhase38DocumentPath = RuntimeAgentProblemFollowUpDecisionRecordService.Phase38DocumentPath;
    private const string ProductClosurePhase39DocumentPath = RuntimeAgentProblemFollowUpPlanningIntakeService.Phase39DocumentPath;
    private const string ProductClosureCurrentDocumentPath = RuntimeProductClosureMetadata.CurrentDocumentPath;

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<RuntimeAgentWorkingModesSurface> workingModesFactory;
    private readonly Func<RuntimeAdapterHandoffContractSurface> adapterContractFactory;
    private readonly Func<RuntimeProtectedTruthRootPolicySurface> protectedTruthRootPolicyFactory;

    public RuntimeGovernedAgentHandoffProofService(
        string repoRoot,
        Func<RuntimeAgentWorkingModesSurface> workingModesFactory,
        Func<RuntimeAdapterHandoffContractSurface> adapterContractFactory,
        Func<RuntimeProtectedTruthRootPolicySurface> protectedTruthRootPolicyFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.workingModesFactory = workingModesFactory;
        this.adapterContractFactory = adapterContractFactory;
        this.protectedTruthRootPolicyFactory = protectedTruthRootPolicyFactory;
    }

    public RuntimeGovernedAgentHandoffProofSurface Build()
    {
        var errors = new List<string>();
        ValidatePath(ProofDocumentPath, "Governed agent handoff proof document", errors);
        ValidatePath(SessionGatewayDocumentPath, "Session-gateway governed handoff proof document", errors);
        ValidatePath(ProductClosureBaselineDocumentPath, "Product closure Phase 0 baseline document", errors);
        ValidatePath(ProductClosurePhase1DocumentPath, "Product closure Phase 1 CLI distribution document", errors);
        ValidatePath(ProductClosurePhase2DocumentPath, "Product closure Phase 2 readiness separation document", errors);
        ValidatePath(ProductClosurePhase3DocumentPath, "Product closure Phase 3 minimal init onboarding document", errors);
        ValidatePath(ProductClosurePhase4DocumentPath, "Product closure Phase 4 external target dogfood proof document", errors);
        ValidatePath(ProductClosurePhase5DocumentPath, "Product closure Phase 5 real project pilot document", errors);
        ValidatePath(ProductClosurePhase6DocumentPath, "Product closure Phase 6 official truth writeback document", errors);
        ValidatePath(ProductClosurePhase7DocumentPath, "Product closure Phase 7 managed workspace execution document", errors);
        ValidatePath(ProductClosurePhase8DocumentPath, "Product closure Phase 8 managed workspace writeback document", errors);
        ValidatePath(ProductClosurePhase9DocumentPath, "Product closure Phase 9 productized pilot guide document", errors);
        ValidatePath(ProductClosurePhase10DocumentPath, "Product closure Phase 10 productized pilot status document", errors);
        ValidatePath(ProductClosurePhase11BDocumentPath, "Product closure Phase 11B target agent bootstrap pack document", errors);
        ValidatePath(ProductClosurePhase12DocumentPath, "Product closure Phase 12 existing target bootstrap repair document", errors);
        ValidatePath(ProductClosurePhase13DocumentPath, "Product closure Phase 13 target commit hygiene document", errors);
        ValidatePath(ProductClosurePhase14DocumentPath, "Product closure Phase 14 target commit plan document", errors);
        ValidatePath(ProductClosurePhase15DocumentPath, "Product closure Phase 15 target commit closure document", errors);
        ValidatePath(ProductClosurePhase16DocumentPath, "Product closure Phase 16 local dist handoff document", errors);
        ValidatePath(ProductClosurePhase17DocumentPath, "Product closure Phase 17 product pilot proof document", errors);
        ValidatePath(ProductClosurePhase18DocumentPath, "Product closure Phase 18 external consumer resource pack document", errors);
        ValidatePath(ProductClosurePhase19DocumentPath, "Product closure Phase 19 CLI invocation contract document", errors);
        ValidatePath(ProductClosurePhase20DocumentPath, "Product closure Phase 20 CLI activation plan document", errors);
        ValidatePath(ProductClosurePhase21DocumentPath, "Product closure Phase 21 target dist binding plan document", errors);
        ValidatePath(ProductClosurePhase22DocumentPath, "Product closure Phase 22 local dist freshness smoke document", errors);
        ValidatePath(ProductClosurePhase23DocumentPath, "Product closure Phase 23 frozen dist target readback proof document", errors);
        ValidatePath(ProductClosurePhase24DocumentPath, "Product closure Phase 24 wrapper runtime root binding document", errors);
        ValidatePath(ProductClosurePhase25DocumentPath, "Product closure Phase 25 external target product proof closure document", errors);
        ValidatePath(ProductClosurePhase26ADocumentPath, "Product closure Phase 26A projection cleanup document", errors);
        ValidatePath(ProductClosurePhase26DocumentPath, "Product closure Phase 26 real external repo pilot document", errors);
        ValidatePath(ProductClosurePhase27DocumentPath, "Product closure Phase 27 external target residue policy document", errors);
        ValidatePath(ProductClosurePhase28DocumentPath, "Product closure Phase 28 target ignore decision plan document", errors);
        ValidatePath(ProductClosurePhase29DocumentPath, "Product closure Phase 29 target ignore decision record document", errors);
        ValidatePath(ProductClosurePhase30DocumentPath, "Product closure Phase 30 target ignore decision record audit document", errors);
        ValidatePath(ProductClosurePhase31DocumentPath, "Product closure Phase 31 target ignore decision record commit readback document", errors);
        ValidatePath(ProductClosurePhase32DocumentPath, "Product closure Phase 32 alpha external-use readiness document", errors);
        ValidatePath(ProductClosurePhase33DocumentPath, "Product closure Phase 33 external target pilot start bundle document", errors);
        ValidatePath(ProductClosurePhase34DocumentPath, "Product closure Phase 34 agent problem intake document", errors);
        ValidatePath(ProductClosurePhase35DocumentPath, "Product closure Phase 35 agent problem triage ledger document", errors);
        ValidatePath(ProductClosurePhase36DocumentPath, "Product closure Phase 36 agent problem follow-up candidates document", errors);
        ValidatePath(ProductClosurePhase37DocumentPath, "Product closure Phase 37 agent problem follow-up decision plan document", errors);
        ValidatePath(ProductClosurePhase38DocumentPath, "Product closure Phase 38 agent problem follow-up decision record document", errors);
        ValidatePath(ProductClosurePhase39DocumentPath, "Product closure Phase 39 agent problem follow-up planning intake document", errors);
        ValidatePath(ProductClosureCurrentDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);

        var workingModes = workingModesFactory();
        var adapterContract = adapterContractFactory();
        var protectedPolicy = protectedTruthRootPolicyFactory();
        errors.AddRange(workingModes.Errors.Select(static error => $"runtime-agent-working-modes: {error}"));
        errors.AddRange(adapterContract.Errors.Select(static error => $"runtime-adapter-handoff-contract: {error}"));
        errors.AddRange(protectedPolicy.Errors.Select(static error => $"runtime-protected-truth-root-policy: {error}"));

        return new RuntimeGovernedAgentHandoffProofSurface
        {
            ProofDocumentPath = ProofDocumentPath,
            SessionGatewayDocumentPath = SessionGatewayDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            ProductClosureBaselineDocumentPath = ProductClosureBaselineDocumentPath,
            ProductClosureCurrentDocumentPath = ProductClosureCurrentDocumentPath,
            OverallPosture = errors.Count == 0
                ? "bounded_governed_agent_handoff_proof_ready"
                : "blocked_by_governed_agent_handoff_proof_gaps",
            AdapterContractPosture = adapterContract.OverallPosture,
            ProtectedTruthRootPosture = protectedPolicy.OverallPosture,
            WorkingModeRecommendationPosture = workingModes.ExternalAgentRecommendationPosture,
            ProofStages = BuildProofStages(),
            ConstraintClasses = BuildConstraintClasses(),
            RequiredColdReadbacks =
            [
                "init --json",
                "doctor --json",
                "agent start [--json]",
                "inspect runtime-agent-thread-start",
                "agent handoff",
                "agent handoff --json",
                "inspect runtime-agent-working-modes",
                "inspect runtime-adapter-handoff-contract",
                "inspect runtime-protected-truth-root-policy",
                "inspect runtime-workspace-mutation-audit <task-id>",
                "inspect runtime-brokered-execution <task-id>",
                "inspect runtime-governed-agent-handoff-proof",
                "pilot start [--json]",
                "inspect runtime-external-target-pilot-start",
                "pilot next [--json]",
                "inspect runtime-external-target-pilot-next",
                "pilot problem-intake [--json]",
                "inspect runtime-agent-problem-intake",
                "pilot triage [--json]",
                "inspect runtime-agent-problem-triage-ledger",
                "pilot follow-up [--json]",
                "inspect runtime-agent-problem-follow-up-candidates",
                "pilot follow-up-plan [--json]",
                "inspect runtime-agent-problem-follow-up-decision-plan",
                "pilot follow-up-record [--json]",
                "inspect runtime-agent-problem-follow-up-decision-record",
                "pilot follow-up-intake [--json]",
                "inspect runtime-agent-problem-follow-up-planning-intake",
                "pilot follow-up-gate [--json]",
                "inspect runtime-agent-problem-follow-up-planning-gate",
                "pilot readiness [--json]",
                "inspect runtime-alpha-external-use-readiness",
                "pilot invocation [--json]",
                "inspect runtime-cli-invocation-contract",
                "pilot activation [--json]",
                "inspect runtime-cli-activation-plan",
                "pilot dist-smoke [--json]",
                "inspect runtime-local-dist-freshness-smoke",
                "pilot dist-binding [--json]",
                "inspect runtime-target-dist-binding-plan",
                "pilot target-proof [--json]",
                "inspect runtime-frozen-dist-target-readback-proof",
                "pilot guide [--json]",
                "inspect runtime-product-closure-pilot-guide",
                "pilot status [--json]",
                "inspect runtime-product-closure-pilot-status",
                "pilot resources [--json]",
                "inspect runtime-external-consumer-resource-pack",
                "pilot commit-hygiene [--json]",
                "inspect runtime-target-commit-hygiene",
                "pilot commit-plan [--json]",
                "inspect runtime-target-commit-plan",
                "pilot closure [--json]",
                "inspect runtime-target-commit-closure",
                "pilot residue [--json]",
                "inspect runtime-target-residue-policy",
                "pilot ignore-plan [--json]",
                "inspect runtime-target-ignore-decision-plan",
                "pilot ignore-record [--json]",
                "inspect runtime-target-ignore-decision-record",
                "pilot dist [--json]",
                "inspect runtime-local-dist-handoff",
                "pilot proof [--json]",
                "inspect runtime-product-pilot-proof",
                "intent draft --persist",
                "plan init [candidate-card-id]",
                "plan submit-workspace <task-id> [reason...]",
            ],
            RecommendedNextAction = errors.Count == 0
                ? "Start external target work with carves agent start --json, follow next_governed_command exactly, and use pilot start/next/problem/follow-up readbacks only when the start payload reports a gap or blocker; use carves pilot report-problem instead of mutating protected truth, use carves pilot guide for the full path, verify local dist freshness with carves pilot dist-smoke, verify dist binding with carves pilot dist-binding and local dist handoff with carves pilot dist, prove target frozen-dist readback with carves pilot target-proof, then use carves init and carves doctor to establish the target boundary, read handoff proof from the Runtime document root, enter formal planning, issue a task-bound workspace or Mode E packet, submit returned material through Runtime review, approve only after packet, evidence, path, and mutation-audit blockers are clear, run carves pilot commit-plan before staging a product proof commit, rerun carves pilot closure after commit, run carves pilot residue when local/tooling residue remains, run carves pilot ignore-plan before editing .gitignore, run carves pilot ignore-record and record-ignore-decision when a durable operator decision is required, keep target repos attached to a frozen local dist for stable consumption, and use carves pilot proof as the final read-only pilot closure aggregate."
                : "Restore missing doctrine/read-model anchors before using the end-to-end handoff proof as closure evidence.",
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This proof is bounded Runtime governance evidence, not broad production sandboxing.",
                "This proof does not implement a second planner, second task graph, or second control plane.",
                "This proof does not claim full ACP or MCP transport implementation.",
                "This proof does not claim vendor-native permission files are the portable baseline.",
            ],
        };
    }

    private static RuntimeGovernedAgentHandoffProofStageSurface[] BuildProofStages()
    {
        return
        [
            BuildStage(
                1,
                "formal_planning_entry",
                "plan init [candidate-card-id] / inspect runtime-formal-planning-posture",
                "one active planning slot/card is required before durable governed work starts",
                "active_planning_slot_single_owner"),
            BuildStage(
                2,
                "acceptance_bound_task_truth",
                "inspect task <task-id> / api runtime-agent-working-modes",
                "materialized task carries acceptance_contract and working-mode recommendation",
                "acceptance_contract_present"),
            BuildStage(
                3,
                "adapter_handoff_contract",
                "inspect runtime-adapter-handoff-contract",
                "CLI-first, ACP-second, and MCP-optional lanes expose inputs, outputs, and non-authority",
                "adapter_cannot_bypass_runtime_truth"),
            BuildStage(
                4,
                "workspace_or_brokered_execution",
                "plan issue-workspace <task-id> or inspect runtime-brokered-execution <task-id>",
                "agent receives a bounded workspace lease or task-scoped packet/result return channel",
                "worker_stops_at_result_return"),
            BuildStage(
                5,
                "pre_writeback_blocker_projection",
                "plan submit-workspace <task-id> / inspect runtime-workspace-mutation-audit <task-id> / inspect runtime-brokered-execution <task-id>",
                "path, packet, acceptance-evidence, protected-root, and mutation-audit blockers are visible before approval",
                "review_preflight_fail_closed"),
            BuildStage(
                6,
                "host_owned_review_writeback",
                "review approve <task-id> <reason...> / review writeback preview",
                "official truth ingress stays planner-reviewed and host-owned",
                "host_writeback_only"),
        ];
    }

    private static RuntimeGovernedAgentHandoffConstraintClassSurface[] BuildConstraintClasses()
    {
        return
        [
            new RuntimeGovernedAgentHandoffConstraintClassSurface
            {
                ClassId = "soft_advisory",
                EnforcementLevel = "read_side_guidance_only",
                AppliesTo = "Mode A, bootstrap docs, planning guidance before hard-mode prerequisites",
                Summary = "Portable guidance and required documents shape behavior even when the agent can ignore them.",
            },
            new RuntimeGovernedAgentHandoffConstraintClassSurface
            {
                ClassId = "hard_runtime_gate",
                EnforcementLevel = "runtime_enforced_before_writeback",
                AppliesTo = "formal planning slot, acceptance contract, managed workspace path policy, Mode E packet/preflight",
                Summary = "Runtime-owned gates block or project violations before official truth ingress.",
            },
            new RuntimeGovernedAgentHandoffConstraintClassSurface
            {
                ClassId = "vendor_optional",
                EnforcementLevel = "acceleration_not_baseline",
                AppliesTo = "Codex/Claude native config, IDE permission surfaces, ACP/MCP transports",
                Summary = "Vendor-specific reinforcement may help but cannot replace the Runtime-owned contract.",
            },
            new RuntimeGovernedAgentHandoffConstraintClassSurface
            {
                ClassId = "deferred",
                EnforcementLevel = "not_claimed",
                AppliesTo = "OS sandboxing, full ACP server, full MCP server, remote pack orchestration",
                Summary = "These remain outside the bounded proof and must not be marketed as completed by this card.",
            },
        ];
    }

    private static RuntimeGovernedAgentHandoffProofStageSurface BuildStage(
        int order,
        string stageId,
        string command,
        string evidence,
        string gate)
    {
        return new RuntimeGovernedAgentHandoffProofStageSurface
        {
            Order = order,
            StageId = stageId,
            RequiredSurfaceOrCommand = command,
            EvidenceProjected = evidence,
            Gate = gate,
        };
    }

    private void ValidatePath(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
