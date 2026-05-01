using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeExternalConsumerResourcePackService
{
    public const string PhaseDocumentPath = "docs/runtime/carves-product-closure-phase-18-external-consumer-resource-pack.md";
    public const string PreviousPhaseDocumentPath = "docs/runtime/carves-product-closure-phase-17-product-pilot-proof.md";
    public const string ResourcePackGuideDocumentPath = "docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeExternalConsumerResourcePackService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeExternalConsumerResourcePackSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 18 external consumer resource pack document", errors);
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
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md", "Product closure Phase 32 alpha external-use readiness document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md", "Product closure Phase 33 external target pilot start bundle document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md", "Product closure Phase 34 agent problem intake document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md", "Product closure Phase 35 agent problem triage ledger document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md", "Product closure Phase 36 agent problem follow-up candidates document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpDecisionPlanService.Phase37DocumentPath, "Product closure Phase 37 agent problem follow-up decision plan document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpDecisionRecordService.Phase38DocumentPath, "Product closure Phase 38 agent problem follow-up decision record document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpPlanningIntakeService.Phase39DocumentPath, "Product closure Phase 39 agent problem follow-up planning intake document", errors);
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.CurrentDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidateRuntimeDocument(RuntimeFrozenDistTargetReadbackProofService.GuideDocumentPath, "Frozen dist target readback proof guide document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistFreshnessSmokeService.PhaseDocumentPath, "Product closure Phase 22 local dist freshness smoke document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistFreshnessSmokeService.GuideDocumentPath, "Local dist freshness smoke guide document", errors);
        ValidateRuntimeDocument(RuntimeTargetDistBindingPlanService.PhaseDocumentPath, "Product closure Phase 21 target dist binding plan document", errors);
        ValidateRuntimeDocument(RuntimeCliActivationPlanService.PhaseDocumentPath, "Product closure Phase 20 CLI activation plan document", errors);
        ValidateRuntimeDocument(RuntimeCliInvocationContractService.PhaseDocumentPath, "Product closure Phase 19 CLI invocation contract document", errors);
        ValidateRuntimeDocument(PreviousPhaseDocumentPath, "Product closure Phase 17 product pilot proof document", errors);
        ValidateRuntimeDocument(ResourcePackGuideDocumentPath, "External consumer resource pack guide document", errors);
        ValidateRuntimeDocument(RuntimeTargetDistBindingPlanService.GuideDocumentPath, "Target dist binding plan guide document", errors);
        ValidateRuntimeDocument(RuntimeCliActivationPlanService.ActivationGuideDocumentPath, "CLI activation plan guide document", errors);
        ValidateRuntimeDocument(RuntimeCliInvocationContractService.InvocationGuideDocumentPath, "CLI invocation contract guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotGuideService.GuideDocumentPath, "Productized pilot guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotStatusService.GuideDocumentPath, "Productized pilot status document", errors);
        ValidateRuntimeDocument(RuntimeTargetAgentBootstrapPackService.GuideDocumentPath, "Target agent bootstrap pack guide document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistHandoffService.LocalDistGuideDocumentPath, "Runtime local dist guide document", errors);
        ValidateRuntimeDocument(RuntimeExternalTargetPilotStartService.QuickstartGuideDocumentPath, "External agent quickstart guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemIntakeService.ProblemIntakeGuideDocumentPath, "Agent problem intake guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemTriageLedgerService.TriageLedgerGuideDocumentPath, "Agent problem triage ledger guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpCandidatesService.FollowUpCandidatesGuideDocumentPath, "Agent problem follow-up candidates guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpDecisionPlanService.DecisionPlanGuideDocumentPath, "Agent problem follow-up decision plan guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpDecisionRecordService.DecisionRecordGuideDocumentPath, "Agent problem follow-up decision record guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpPlanningIntakeService.PlanningIntakeGuideDocumentPath, "Agent problem follow-up planning intake guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpPlanningGateService.PlanningGateGuideDocumentPath, "Agent problem follow-up planning gate guide document", errors);
        ValidateRuntimeDocument("docs/guides/CARVES_CLI_DISTRIBUTION.md", "CLI distribution guide document", errors);
        ValidateRuntimeDocument("docs/runtime/runtime-governed-agent-handoff-proof.md", "Runtime governed agent handoff proof document", errors);
        ValidateRuntimeDocument("docs/session-gateway/governed-agent-handoff-proof.md", "Session-gateway governed agent handoff proof document", errors);
        ValidateRuntimeFile(RuntimeCliWrapperPaths.PreferredWrapperFileName, "Runtime CLI wrapper", errors);

        var runtimeOwnedResources = BuildRuntimeOwnedResources();
        var targetGeneratedResources = BuildTargetGeneratedResources();
        var commandEntries = BuildCommandEntries();
        var boundaryRules = BuildBoundaryRules();
        var readbacks = BuildRequiredReadbacks();
        var resourcePackComplete = errors.Count == 0;

        return new RuntimeExternalConsumerResourcePackSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            PreviousPhaseDocumentPath = PreviousPhaseDocumentPath,
            ResourcePackGuideDocumentPath = ResourcePackGuideDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = resourcePackComplete
                ? "external_consumer_resource_pack_ready"
                : "external_consumer_resource_pack_blocked_by_missing_runtime_resources",
            ResourcePackComplete = resourcePackComplete,
            RuntimeOwnedResourceCount = runtimeOwnedResources.Length,
            TargetGeneratedResourceCount = targetGeneratedResources.Length,
            CommandEntryCount = commandEntries.Length,
            RuntimeOwnedResources = runtimeOwnedResources,
            TargetGeneratedResources = targetGeneratedResources,
            CommandEntries = commandEntries,
            BoundaryRules = boundaryRules,
            RequiredReadbackCommands = readbacks,
            Gaps = errors.Count == 0 ? [] : errors.Select(static error => $"missing_or_invalid_resource:{error}").ToArray(),
            Summary = resourcePackComplete
                ? "External consumer resource pack is ready: external projects can read Runtime-owned quickstart, one-command agent thread start, start/next bundle, problem intake, triage ledger, follow-up candidates, follow-up decision plan, follow-up decision record, follow-up planning intake, follow-up planning gate, doctrine, alpha readiness, invocation contract, activation plan, dist freshness smoke, dist binding plan, frozen dist target readback proof, residue policy, ignore decision plan, ignore decision record, audit, and commit readback contracts from the Runtime document root and materialize only target bootstrap projections."
                : "External consumer resource pack is blocked until required Runtime-owned docs, guide, and CLI wrapper resources are restored.",
            RecommendedNextAction = resourcePackComplete
                ? "From an external target repo, start a new agent thread with carves agent start --json, follow next_governed_command exactly, and use the detailed pilot start/next/problem/follow-up readbacks only when the start payload reports a gap or a blocker; use report-problem when blocked, agent bootstrap --write when bootstrap files are missing, and dist, residue, ignore, target-proof, and product-proof readbacks before claiming final closure."
                : "Restore the missing Runtime-owned resource pack files, then rerun carves pilot resources --json.",
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not copy Runtime docs into a target repo.",
                "This surface does not initialize, plan, review, write back, stage, commit, push, tag, release, pack, repair, or retarget anything.",
                "This surface does not make any specific external target repo a Runtime closure prerequisite.",
                "This surface does not claim public package distribution, signed release manifests, OS sandboxing, full ACP, full MCP, or remote worker orchestration.",
            ],
        };
    }

    private static RuntimeExternalConsumerResourceSurface[] BuildRuntimeOwnedResources()
    {
        return
        [
            BuildRuntimeResource("carves", "runtime_cli_wrapper", "Unix/WSL command entry for source-tree or local-dist consumption", "runtime_owned_entry_do_not_copy_into_target_truth"),
            BuildRuntimeResource("carves.ps1", "runtime_cli_wrapper", "PowerShell command entry for source-tree or local-dist consumption", "runtime_owned_entry_do_not_copy_into_target_truth"),
            BuildRuntimeResource("carves.cmd", "runtime_cli_wrapper", "Windows cmd shim for tooling that cannot call PowerShell directly", "runtime_owned_entry_do_not_copy_into_target_truth"),
            BuildRuntimeResource("docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md", "runtime_guide", "explains the read-only external target frozen-dist readback proof", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md", "runtime_guide", "explains read-only smoke proof for the frozen local Runtime dist freshness", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md", "runtime_guide", "explains operator-owned target binding to a frozen local Runtime dist", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_CLI_ACTIVATION_PLAN.md", "runtime_guide", "explains operator-owned activation options without mutating target truth or shell state", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md", "runtime_guide", "explains source-tree, local-dist, cmd shim, and global alias invocation boundaries", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_CLI_DISTRIBUTION.md", "runtime_guide", "explains CLI-first distribution and wrapper usage", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_PRODUCTIZED_PILOT_GUIDE.md", "runtime_guide", "ordered external-project pilot route", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md", "runtime_guide", "current-stage and next-command interpretation", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md", "runtime_guide", "short external-agent start protocol for the one-command agent thread start and troubleshooting readbacks", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md", "runtime_guide", "structured problem-intake protocol for blocked external agents", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md", "runtime_guide", "operator-facing friction ledger for recorded agent problem intake", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md", "runtime_guide", "operator-facing follow-up candidates for repeated or blocking problem intake patterns", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md", "runtime_guide", "operator-facing decision plan for follow-up candidates", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md", "runtime_guide", "durable operator decision records for follow-up candidates", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md", "runtime_guide", "read-only planning intake for accepted follow-up decision records", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md", "runtime_guide", "read-only planning gate for accepted follow-up planning inputs and the active planning slot", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md", "runtime_guide", "target bootstrap materialization contract", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_RUNTIME_LOCAL_DIST.md", "runtime_guide", "local dist handoff and stable consumption contract", "runtime_document_root_only"),
            BuildRuntimeResource("docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md", "runtime_guide", "external consumer resource pack inventory and boundaries", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/runtime-governed-agent-handoff-proof.md", "runtime_doctrine", "constraint ladder, collaboration plane, and governed handoff proof", "runtime_document_root_only"),
            BuildRuntimeResource(RuntimeProductClosureMetadata.CurrentDocumentPath, "runtime_phase_truth", "current product closure phase truth", "runtime_document_root_only"),
            BuildRuntimeResource(RuntimeProductClosureMetadata.PreviousDocumentPath, "runtime_phase_truth", "previous product closure phase truth", "runtime_document_root_only"),
            BuildRuntimeResource(RuntimeAgentProblemFollowUpPlanningIntakeService.Phase39DocumentPath, "runtime_phase_truth", "supporting agent problem follow-up planning intake truth", "runtime_document_root_only"),
            BuildRuntimeResource(RuntimeAgentProblemFollowUpDecisionRecordService.Phase38DocumentPath, "runtime_phase_truth", "supporting agent problem follow-up decision record truth", "runtime_document_root_only"),
            BuildRuntimeResource(RuntimeAgentProblemFollowUpDecisionPlanService.Phase37DocumentPath, "runtime_phase_truth", "supporting agent problem follow-up decision plan truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md", "runtime_phase_truth", "supporting agent problem follow-up candidate truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md", "runtime_phase_truth", "supporting agent problem triage truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md", "runtime_phase_truth", "supporting agent problem intake truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md", "runtime_phase_truth", "supporting external target pilot start bundle truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md", "runtime_phase_truth", "supporting alpha external-use readiness truth", "runtime_document_root_only"),
            BuildRuntimeResource(RuntimeTargetIgnoreDecisionRecordService.CommitReadbackPhaseDocumentPath, "runtime_phase_truth", "previous product closure phase truth", "runtime_document_root_only"),
            BuildRuntimeResource(RuntimeTargetIgnoreDecisionRecordService.AuditPhaseDocumentPath, "runtime_phase_truth", "supporting target ignore decision record audit truth", "runtime_document_root_only"),
            BuildRuntimeResource(RuntimeTargetIgnoreDecisionRecordService.DecisionRecordPhaseDocumentPath, "runtime_phase_truth", "supporting target ignore decision record truth", "runtime_document_root_only"),
            BuildRuntimeResource(RuntimeTargetIgnoreDecisionPlanService.PhaseDocumentPath, "runtime_phase_truth", "supporting target ignore decision plan truth", "runtime_document_root_only"),
            BuildRuntimeResource(RuntimeTargetResiduePolicyService.PhaseDocumentPath, "runtime_phase_truth", "supporting target residue policy truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-26a-product-closure-projection-cleanup.md", "runtime_phase_truth", "supporting product closure projection cleanup truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md", "runtime_phase_truth", "previous product closure phase truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md", "runtime_phase_truth", "previous product closure phase truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-23-frozen-dist-target-readback-proof.md", "runtime_phase_truth", "previous product closure phase truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-22-local-dist-freshness-smoke.md", "runtime_phase_truth", "previous product closure phase truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-21-target-dist-binding-plan.md", "runtime_phase_truth", "previous product closure phase truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-20-cli-activation-plan.md", "runtime_phase_truth", "previous product closure phase truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-19-cli-invocation-contract.md", "runtime_phase_truth", "previous product closure phase truth", "runtime_document_root_only"),
            BuildRuntimeResource("docs/runtime/carves-product-closure-phase-18-external-consumer-resource-pack.md", "runtime_phase_truth", "previous product closure phase truth", "runtime_document_root_only"),
        ];
    }

    private static RuntimeExternalConsumerGeneratedResourceSurface[] BuildTargetGeneratedResources()
    {
        return
        [
            BuildGeneratedResource(".ai/runtime.json", "target_attach_truth", "carves init [target-path] --json", "created_by_attach_init_not_agent_authored"),
            BuildGeneratedResource(".ai/runtime/attach-handshake.json", "target_attach_truth", "carves init [target-path] --json", "created_by_attach_init_not_agent_authored"),
            BuildGeneratedResource(".carves/carves", "target_bootstrap_projection", "carves agent bootstrap --write", "project_local_launcher_no_global_alias_requirement"),
            BuildGeneratedResource(".carves/AGENT_START.md", "target_bootstrap_projection", "carves agent bootstrap --write", "human_readable_project_local_agent_start"),
            BuildGeneratedResource(".carves/agent-start.json", "target_bootstrap_projection", "carves agent bootstrap --write", "machine_readable_project_local_agent_start"),
            BuildGeneratedResource(".ai/AGENT_BOOTSTRAP.md", "target_bootstrap_projection", "carves agent bootstrap --write", "create_missing_only_do_not_edit_official_truth_manually"),
            BuildGeneratedResource("AGENTS.md", "target_bootstrap_projection", "carves agent bootstrap --write", "create_missing_only_existing_file_is_target_owned"),
        ];
    }

    private static RuntimeExternalConsumerCommandEntrySurface[] BuildCommandEntries()
    {
        return
        [
            BuildCommand("carves agent start --json", "runtime-agent-thread-start", "read_only_thread_start", "single first readback for external agent threads; selects next_governed_command and exposes minimal rules"),
            BuildCommand("carves agent context --json", "runtime-agent-short-context", "read_only_short_context_aggregate", "single compact readback over thread start, bootstrap packet, task overlay, and context-pack pointers"),
            BuildCommand("carves api runtime-markdown-read-path-budget", "runtime-markdown-read-path-budget", "read_only_markdown_budget_projection", "show which Markdown reads are mandatory, deferred, task-scoped, or escalation-only without loading broad generated views"),
            BuildCommand("carves api runtime-worker-execution-audit <query>", "runtime-worker-execution-audit", "read_only_worker_execution_audit_query", "query compact append-only worker execution audit rows by task, status, backend, provider, safety, or limit without reading full artifacts"),
            BuildCommand("carves api runtime-governance-surface-coverage-audit", "runtime-governance-surface-coverage-audit", "read_only_governance_surface_coverage_audit", "show whether bounded governance-critical surfaces are wired through registry, usage, resource-pack/docs, and host-contract evidence"),
            BuildCommand("carves api runtime-default-workflow-proof", "runtime-default-workflow-proof", "read_only_default_workflow_proof", "prove the default external-agent workflow remains a short path over agent start, short context, Markdown budget, and conditional troubleshooting"),
            BuildCommand("carves pilot invocation --json", "runtime-cli-invocation-contract", "read_only_invocation_contract", "show source-tree, local-dist, cmd shim, and future global alias invocation boundaries"),
            BuildCommand("carves pilot start --json", "runtime-external-target-pilot-start", "read_only_agent_entry_bundle", "show the external-agent start pack, start readbacks, guardrails, and current next command"),
            BuildCommand("carves pilot next --json", "runtime-external-target-pilot-next", "read_only_next_step_selector", "show the next governed command and stop/report triggers before each step"),
            BuildCommand("carves pilot problem-intake --json", "runtime-agent-problem-intake", "read_only_problem_intake_contract", "show the problem payload schema, stop triggers, and recent agent problem reports"),
            BuildCommand("carves pilot triage --json", "runtime-agent-problem-triage-ledger", "read_only_problem_triage_ledger", "group recorded problem reports into an operator-facing friction queue without resolving them"),
            BuildCommand("carves pilot follow-up --json", "runtime-agent-problem-follow-up-candidates", "read_only_problem_follow_up_candidates", "promote repeated or blocking problem patterns into operator-review follow-up candidates without creating cards or tasks"),
            BuildCommand("carves pilot follow-up-plan --json", "runtime-agent-problem-follow-up-decision-plan", "read_only_problem_follow_up_decision_plan", "project accept/reject/wait decisions for follow-up candidates without recording decisions or creating cards"),
            BuildCommand("carves pilot follow-up-record --json", "runtime-agent-problem-follow-up-decision-record", "read_only_problem_follow_up_decision_record", "show whether current follow-up-plan candidates have durable operator decision records and whether those records pass audit"),
            BuildCommand("carves pilot follow-up-intake --json", "runtime-agent-problem-follow-up-planning-intake", "read_only_problem_follow_up_planning_intake", "project accepted follow-up decision records into formal planning inputs without creating cards or tasks"),
            BuildCommand("carves pilot follow-up-gate --json", "runtime-agent-problem-follow-up-planning-gate", "read_only_problem_follow_up_planning_gate", "project accepted follow-up planning inputs against intent draft and the single active planning slot before plan init"),
            BuildCommand("carves pilot record-follow-up-decision <decision> --all --reason <text>", "runtime-agent-problem-follow-up-decision-record", "operator_decision_truth", "record a reviewed accept, reject, or wait decision without creating cards or tasks"),
            BuildCommand("carves pilot report-problem <json-path> --json", "runtime-agent-problem-intake", "target_runtime_problem_evidence", "record a blocked-agent problem report and mirror it into pilot evidence without authorizing the blocked change"),
            BuildCommand("carves pilot activation --json", "runtime-cli-activation-plan", "read_only_activation_plan", "show absolute wrapper, session alias, PATH entry, cmd shim, and optional tool activation choices"),
            BuildCommand("carves pilot dist-smoke --json", "runtime-local-dist-freshness-smoke", "read_only_dist_freshness_smoke", "prove the frozen local Runtime dist exists, includes current resources, and matches the clean source HEAD"),
            BuildCommand("carves pilot readiness --json", "runtime-alpha-external-use-readiness", "read_only_alpha_readiness_rollup", $"show whether CARVES Runtime {RuntimeAlphaVersion.Current} is ready for bounded external-project alpha use"),
            BuildCommand("carves pilot dist-binding --json", "runtime-target-dist-binding-plan", "read_only_dist_binding_plan", "show the operator-owned path for binding the target repo to a frozen local Runtime dist"),
            BuildCommand("carves pilot target-proof --json", "runtime-frozen-dist-target-readback-proof", "read_only_target_readback_proof", "prove the current target is initialized, bootstrapped, and bound to the fresh frozen Runtime dist"),
            BuildCommand("carves pilot resources --json", "runtime-external-consumer-resource-pack", "read_only_resource_inventory", "show the external-consumer resource pack and boundaries"),
            BuildCommand("carves agent handoff --json", "runtime-governed-agent-handoff-proof", "read_only_contract", "show constraint ladder, collaboration plane, and current product closure phase"),
            BuildCommand("carves agent bootstrap --write", "runtime-target-agent-bootstrap-pack", "target_bootstrap_projection", "materialize missing target bootstrap files without overwriting target-owned instructions"),
            BuildCommand("carves pilot status --json", "runtime-product-closure-pilot-status", "read_only_stage_selector", "select the next governed command before planning or editing"),
            BuildCommand("carves pilot guide --json", "runtime-product-closure-pilot-guide", "read_only_route", "show the full external-project route"),
            BuildCommand("carves pilot residue --json", "runtime-target-residue-policy", "read_only_residue_policy", "show whether excluded local/tooling residue can remain local and which ignore entries are review candidates"),
            BuildCommand("carves pilot ignore-plan --json", "runtime-target-ignore-decision-plan", "read_only_ignore_decision_plan", "show operator-reviewed keep-local, cleanup, or .gitignore decisions without mutating the target"),
            BuildCommand("carves pilot ignore-record --json", "runtime-target-ignore-decision-record", "read_only_ignore_decision_record", "show whether current ignore-plan entries have durable operator decision records and whether those records pass audit"),
            BuildCommand("carves pilot record-ignore-decision <decision> --all --reason <text>", "runtime-target-ignore-decision-record", "operator_decision_truth", "record a reviewed keep-local, cleanup, or .gitignore decision without mutating .gitignore"),
            BuildCommand("carves pilot dist --json", "runtime-local-dist-handoff", "read_only_distribution_handoff", "verify frozen local dist consumption when stable external use is required"),
            BuildCommand("carves pilot proof --json", "runtime-product-pilot-proof", "read_only_pilot_proof", "aggregate dist handoff and target commit closure after a pilot is complete"),
        ];
    }

    private static string[] BuildBoundaryRules()
    {
        return
        [
            "Runtime-owned docs stay under the Runtime document root recorded by attach; target repos should not copy them into target product truth.",
            "External agents should verify the invocation contract before relying on a generic global `carves` alias.",
            "External agents should run carves agent start --json as the first command in a new thread, follow next_governed_command exactly, and use pilot start/next only as troubleshooting readbacks.",
            "External agents should run pilot problem-intake before reporting blockers, use pilot report-problem instead of editing protected truth roots, use pilot triage to expose accumulated friction, use pilot follow-up to show operator-review candidates, use pilot follow-up-plan to show accept/reject/wait decisions, use pilot follow-up-record to prove durable operator decisions, use pilot follow-up-intake to project accepted decisions, and use pilot follow-up-gate before converting accepted decisions into formal planning.",
            "External agents should run alpha external-use readiness before treating the frozen Runtime dist as ready for bounded target work.",
            "External agents should treat activation as operator-owned convenience and must not edit shell profiles or PATH as project work.",
            "External agents should treat target dist binding as operator-owned and must not edit target runtime manifests or attach handshakes manually.",
            "Target-generated bootstrap files are projections only; they guide agents but do not own planning, task, review, or writeback truth.",
            "External agents must run pilot status before planning or editing and follow the named next_command instead of inventing a parallel workflow.",
            "Official target truth and approved output still enter git through operator-reviewed target commit plan and closure surfaces.",
            "Durable follow-up decision records live under .ai/runtime/agent-problem-follow-up-decisions/ and must go through target commit closure after they are written.",
            "Suggested ignore entries are decision candidates only; agents must not edit .gitignore before an operator reviews the ignore plan.",
            "Durable ignore decision records live under .ai/runtime/target-ignore-decisions/ and must go through target commit closure after they are written.",
            "Malformed, invalid, stale, or conflicting ignore decision records do not satisfy product proof.",
            "AgentCoach or any other external repo may serve as dogfood evidence, but no specific external repo is a Runtime closure prerequisite.",
        ];
    }

    private static string[] BuildRequiredReadbacks()
    {
        return
        [
            "carves agent start --json",
            "carves agent context --json",
            "carves api runtime-markdown-read-path-budget",
            "carves pilot invocation --json",
            "carves pilot start --json",
            "carves pilot next --json",
            "carves pilot problem-intake --json",
            "carves pilot triage --json",
            "carves pilot follow-up --json",
            "carves pilot follow-up-plan --json",
            "carves pilot follow-up-record --json",
            "carves pilot follow-up-intake --json",
            "carves pilot follow-up-gate --json",
            "carves pilot activation --json",
            "carves pilot dist-smoke --json",
            "carves pilot readiness --json",
            "carves pilot dist-binding --json",
            "carves pilot target-proof --json",
            "carves pilot resources --json",
            "carves agent bootstrap [--write] [--json]",
            "carves agent handoff --json",
            "carves pilot status --json",
            "carves pilot guide --json",
            "carves pilot residue --json",
            "carves pilot ignore-plan --json",
            "carves pilot ignore-record --json",
            "carves pilot dist --json",
            "carves pilot proof --json",
        ];
    }

    private static RuntimeExternalConsumerResourceSurface BuildRuntimeResource(
        string path,
        string resourceClass,
        string consumerUse,
        string boundary)
    {
        return new RuntimeExternalConsumerResourceSurface
        {
            Path = path,
            ResourceClass = resourceClass,
            ConsumerUse = consumerUse,
            Boundary = boundary,
        };
    }

    private static RuntimeExternalConsumerGeneratedResourceSurface BuildGeneratedResource(
        string path,
        string resourceClass,
        string materializationCommand,
        string boundary)
    {
        return new RuntimeExternalConsumerGeneratedResourceSurface
        {
            Path = path,
            ResourceClass = resourceClass,
            MaterializationCommand = materializationCommand,
            Boundary = boundary,
        };
    }

    private static RuntimeExternalConsumerCommandEntrySurface BuildCommand(
        string command,
        string surfaceId,
        string authorityClass,
        string consumerUse)
    {
        return new RuntimeExternalConsumerCommandEntrySurface
        {
            Command = command,
            SurfaceId = surfaceId,
            AuthorityClass = authorityClass,
            ConsumerUse = consumerUse,
        };
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private void ValidateRuntimeFile(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
