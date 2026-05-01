using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeProductClosurePilotGuideService
{
    private const string PhaseDocumentPath = RuntimeProductClosureMetadata.CurrentDocumentPath;
    public const string GuideDocumentPath = "docs/guides/CARVES_PRODUCTIZED_PILOT_GUIDE.md";
    private const string PreviousProofDocumentPath = RuntimeLocalDistFreshnessSmokeService.PhaseDocumentPath;

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeProductClosurePilotGuideService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeProductClosurePilotGuideSurface Build()
    {
        var errors = new List<string>();
        ValidatePath(PhaseDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidatePath(GuideDocumentPath, "Productized pilot guide document", errors);
        ValidatePath(RuntimeFrozenDistTargetReadbackProofService.GuideDocumentPath, "Frozen dist target readback proof guide document", errors);
        ValidatePath(RuntimeLocalDistFreshnessSmokeService.GuideDocumentPath, "Local dist freshness smoke guide document", errors);
        ValidatePath(RuntimeTargetDistBindingPlanService.GuideDocumentPath, "Target dist binding plan guide document", errors);
        ValidatePath(RuntimeCliActivationPlanService.ActivationGuideDocumentPath, "CLI activation plan guide document", errors);
        ValidatePath(RuntimeCliInvocationContractService.InvocationGuideDocumentPath, "CLI invocation contract guide document", errors);
        ValidatePath(RuntimeExternalTargetPilotStartService.QuickstartGuideDocumentPath, "External agent quickstart guide document", errors);
        ValidatePath(RuntimeAgentProblemIntakeService.ProblemIntakeGuideDocumentPath, "Agent problem intake guide document", errors);
        ValidatePath(RuntimeAgentProblemTriageLedgerService.TriageLedgerGuideDocumentPath, "Agent problem triage ledger guide document", errors);
        ValidatePath(RuntimeAgentProblemFollowUpCandidatesService.FollowUpCandidatesGuideDocumentPath, "Agent problem follow-up candidates guide document", errors);
        ValidatePath(RuntimeAgentProblemFollowUpDecisionPlanService.DecisionPlanGuideDocumentPath, "Agent problem follow-up decision plan guide document", errors);
        ValidatePath(RuntimeAgentProblemFollowUpDecisionRecordService.DecisionRecordGuideDocumentPath, "Agent problem follow-up decision record guide document", errors);
        ValidatePath(RuntimeAgentProblemFollowUpPlanningIntakeService.PlanningIntakeGuideDocumentPath, "Agent problem follow-up planning intake guide document", errors);
        ValidatePath(RuntimeAgentProblemFollowUpPlanningGateService.PlanningGateGuideDocumentPath, "Agent problem follow-up planning gate guide document", errors);
        ValidatePath(PreviousProofDocumentPath, "Product closure Phase 22 local dist freshness smoke document", errors);
        ValidatePath(RuntimeTargetDistBindingPlanService.PhaseDocumentPath, "Product closure Phase 21 target dist binding plan document", errors);
        ValidatePath(RuntimeCliActivationPlanService.PhaseDocumentPath, "Product closure Phase 20 CLI activation plan document", errors);
        ValidatePath(RuntimeCliInvocationContractService.PhaseDocumentPath, "Product closure Phase 19 CLI invocation contract document", errors);
        ValidatePath(RuntimeExternalConsumerResourcePackService.PhaseDocumentPath, "Product closure Phase 18 external consumer resource pack document", errors);
        ValidatePath(RuntimeExternalConsumerResourcePackService.PreviousPhaseDocumentPath, "Product closure Phase 17 product pilot proof document", errors);
        ValidatePath(RuntimeLocalDistHandoffService.PhaseDocumentPath, "Product closure Phase 16 local dist handoff document", errors);

        return new RuntimeProductClosurePilotGuideSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            GuideDocumentPath = GuideDocumentPath,
            PreviousProofDocumentPath = PreviousProofDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            OverallPosture = errors.Count == 0
                ? "productized_pilot_guide_ready"
                : "blocked_by_productized_pilot_guide_gaps",
            Steps = BuildSteps(),
            CommitHygieneRules =
            [
                "commit official target truth and approved output files after review approve",
                "treat init-generated .ai/config/, .ai/codegraph/, .ai/opportunities/, .ai/refactoring/, and .ai/PROJECT_BOUNDARY.md as official target truth when produced by Runtime attach/init",
                "exclude .ai/runtime/attach.lock.json as local attach coordination residue",
                "use carves pilot residue --json after commit closure when only excluded local/tooling residue remains",
                "use carves pilot ignore-plan --json before editing .gitignore for local/tooling residue",
                "record reviewed follow-up decisions with carves pilot record-follow-up-decision before converting accepted candidates into governed planning input",
                "record the reviewed ignore decision with carves pilot record-ignore-decision before claiming final proof when ignore-plan reports missing entries",
                "treat suggested .gitignore entries from runtime-target-residue-policy as operator-reviewed target decisions, not automatic mutations",
                "do not commit .ai/runtime/live-state/ unless the operator explicitly decides it is product truth",
                "do not copy files manually from a managed workspace after Phase 8; use plan submit-workspace and review approve",
                "do not treat this guide command as an execution or approval authority",
            ],
            RecommendedNextAction = errors.Count == 0
                ? "From the target repo, run carves agent start --json first, treat next_governed_command as a legacy projection hint, prefer available_actions when present, and use carves pilot guide for the full staged sequence from init/doctor through managed workspace submit, review approve, carves pilot commit-plan, carves pilot closure, carves pilot residue when local/tooling residue remains, carves pilot ignore-plan before editing .gitignore, carves pilot ignore-record / record-ignore-decision for durable operator decisions, carves pilot dist-smoke, carves pilot dist-binding, carves pilot dist, carves pilot target-proof, and carves pilot proof; when blocked, use carves pilot report-problem instead of mutating protected truth."
                : "Restore the current product closure residue policy documents before using carves pilot guide as the external-project productization entry.",
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This guide does not create a project, card, task, workspace, review, or commit by itself.",
                "This guide does not replace carves init, doctor, agent handoff, formal planning, or review/writeback commands.",
                "This guide does not implement OS sandboxing, full ACP, full MCP, remote worker orchestration, or a package registry.",
                "This guide does not make CARVES.Runtime the consumer product shell; external operator packaging remains a sibling-repo concern.",
            ],
        };
    }

    private static RuntimeProductClosurePilotGuideStepSurface[] BuildSteps()
    {
        return
        [
            BuildStep(
                0,
                "external_agent_thread_start",
                "carves agent start --json",
                "read_only_thread_start",
                "Give an external agent one compact start packet that aggregates the entry bundle, planning gate, current target posture, handoff proof, minimal rules, stop/report triggers, troubleshooting readbacks, and the legacy next-command projection plus available actions before planning or editing.",
                "thread_start_ready=true and next_governed_command is non-empty"),
            BuildStep(
                1,
                "attach_target",
                "carves init [target-path] --json",
                "host_owned_attach",
                "Bind an existing git target repo to the Runtime document root and attach manifest.",
                "action is initialized_runtime or attached_existing_runtime"),
            BuildStep(
                2,
                "readiness_check",
                "carves doctor --json",
                "read_only_readiness",
                "Separate CLI/tool, target repo, and resident host readiness before planning.",
                "is_ready is true or gaps name the blocked prerequisite"),
            BuildStep(
                3,
                "target_agent_bootstrap",
                "carves agent bootstrap --write",
                "repo_bootstrap_projection",
                "Materialize missing target agent bootstrap files without overwriting target-owned AGENTS.md.",
                ".ai/AGENT_BOOTSTRAP.md exists and root AGENTS.md exists or is target-owned"),
            BuildStep(
                4,
                "cli_invocation_contract",
                "carves pilot invocation --json",
                "read_only_invocation_contract",
                "Separate source-tree, local-dist, cmd shim, and future global alias invocation before relying on a generic carves command.",
                "invocation_contract_complete=true"),
            BuildStep(
                5,
                "cli_activation_plan",
                "carves pilot activation --json",
                "read_only_activation_plan",
                "Show operator-owned activation choices such as absolute wrapper, session alias, PATH entry, cmd shim, and optional tool install.",
                "activation_plan_complete=true"),
            BuildStep(
                6,
                "external_consumer_resource_pack",
                "carves pilot resources --json",
                "read_only_resource_inventory",
                "Show the Runtime-owned docs, target-generated bootstrap files, command entries, and boundaries for external consumers.",
                "resource_pack_complete=true"),
            BuildStep(
                7,
                "handoff_contract",
                "carves agent handoff --json",
                "read_only_contract",
                "Show the agent the active constraint ladder, working modes, and current product closure phase.",
                $"product_closure_phase is {RuntimeProductClosureMetadata.CurrentPhase}"),
            BuildStep(
                8,
                "intent_capture",
                "carves discuss context",
                "read_only_discussion_handoff",
                "Before durable planning exists, stay in ordinary discussion, clarify the project purpose and whether the user wants real engineering work, then enter intent draft --persist only after a bounded scope is explicit.",
                "the scope is clarified in discussion, or intent draft is started from an explicit bounded request"),
            BuildStep(
                9,
                "formal_plan_init",
                "carves plan init [candidate-card-id]",
                "planning_truth",
                "Create or bind the single active formal planning card.",
                "plan status reports one active planning packet"),
            BuildStep(
                10,
                "card_taskgraph_writeback",
                "carves plan export-card <json-path> -> carves plan draft-card <json-path> -> carves plan approve-card <card-id> <reason> -> carves plan draft-taskgraph <json-path> -> carves plan approve-taskgraph <draft-id> <reason>",
                "official_planning_truth",
                "Move from planning packet to approved card, taskgraph, task truth, and acceptance contract.",
                "inspect task <task-id> shows a projected acceptance contract"),
            BuildStep(
                11,
                "workspace_issue",
                "carves plan issue-workspace <task-id>",
                "managed_workspace_lease",
                "Issue a task-bound Mode D workspace with declared writable paths.",
                "inspect runtime-managed-workspace shows task_bound_workspace_active"),
            BuildStep(
                12,
                "workspace_submit",
                "carves plan submit-workspace <task-id> <reason...>",
                "review_candidate",
                "Submit workspace output into Runtime review instead of copying files manually.",
                "task moves to Review and changed paths are listed"),
            BuildStep(
                13,
                "pre_writeback_audit",
                "carves inspect runtime-workspace-mutation-audit <task-id>",
                "read_only_preflight",
                "Check path policy, protected roots, and mutation blockers before approval.",
                "Can proceed to writeback is True"),
            BuildStep(
                14,
                "review_writeback",
                "carves review approve <task-id> <reason...>",
                "host_owned_writeback",
                "Materialize accepted files and official task/review truth through Runtime-owned writeback.",
                "review is approved, writeback is applied, and the workspace lease is released"),
            BuildStep(
                15,
                "closure_readback",
                "carves inspect task <task-id> -> carves inspect runtime-managed-workspace",
                "read_only_closure",
                "Verify task completion, writeback_applied evidence, and no active managed workspace.",
                "task is Completed and Active leases is 0"),
            BuildStep(
                16,
                "target_commit_plan",
                "carves pilot commit-plan -> git add <stage_paths> -> git commit -m <message>",
                "operator_git_commit",
                "Project a read-only staging plan from target commit hygiene, commit official truth and approved output, and exclude local live/tooling residue.",
                "carves pilot commit-plan reports can_stage=true before the durable product proof commit"),
            BuildStep(
                17,
                "target_commit_closure",
                "carves pilot closure --json",
                "read_only_closure",
                "Verify the target git work tree is clean after the operator-reviewed product proof commit.",
                "pilot status reports target_commit_closure complete and commit_closure_complete=true"),
            BuildStep(
                18,
                "target_residue_policy",
                "carves pilot residue --json",
                "read_only_residue_policy",
                "Verify that any remaining dirty target paths are excluded local/tooling residue and that product proof can remain complete.",
                "residue_policy_ready=true"),
            BuildStep(
                19,
                "target_ignore_decision_plan",
                "carves pilot ignore-plan --json",
                "read_only_ignore_decision_plan",
                "Project operator-reviewed choices for keeping residue local, cleaning it manually, or editing .gitignore without mutating the target repo.",
                "ignore_decision_plan_ready=true"),
            BuildStep(
                20,
                "target_ignore_decision_record",
                "carves pilot ignore-record --json -> carves pilot record-ignore-decision keep_local --all --reason <operator-reviewed-reason>",
                "operator_decision_truth",
                "Record the reviewed keep-local, cleanup, or .gitignore decision as target truth without automatically mutating .gitignore.",
                "record_audit_ready=true, decision_record_commit_ready=true, and decision_record_ready=true"),
            BuildStep(
                21,
                "local_dist_freshness_smoke",
                "carves pilot dist-smoke --json",
                "read_only_dist_freshness_smoke",
                "Verify the frozen local Runtime dist exists, includes current product closure resources, and matches the clean Runtime source HEAD.",
                "local_dist_freshness_smoke_ready=true"),
            BuildStep(
                22,
                "target_dist_binding_plan",
                "carves pilot dist-binding --json",
                "read_only_dist_binding_plan",
                "Show the operator-owned path for binding or verifying the target repo against a frozen local Runtime dist.",
                "dist_binding_plan_complete=true"),
            BuildStep(
                23,
                "local_dist_handoff",
                "carves pilot dist --json",
                "read_only_distribution_handoff",
                "Verify the target repo is reading Runtime doctrine and CLI entry from a frozen local dist rather than the live Runtime source tree.",
                "stable_external_consumption_ready=true"),
            BuildStep(
                24,
                "frozen_dist_target_readback_proof",
                "carves pilot target-proof --json",
                "read_only_target_readback_proof",
                "Prove the current external target is initialized, bootstrapped, and reading from the fresh frozen local Runtime dist.",
                "frozen_dist_target_readback_proof_complete=true"),
            BuildStep(
                25,
                "product_pilot_proof",
                "carves pilot proof --json",
                "read_only_pilot_proof",
                "Aggregate frozen dist target proof, local dist handoff, and target commit closure into one external-project pilot proof posture.",
                "product_pilot_proof_complete=true"),
            BuildStep(
                26,
                "ready_for_new_intent",
                "carves discuss context",
                "read_only_discussion_handoff",
                "After product pilot proof is complete, return to normal discussion, clarify whether the user actually wants new project work, and only enter intent capture after a bounded scope is explicit.",
                "the next scope is clarified in discussion, or the agent stays in chat without opening new work"),
        ];
    }

    private static RuntimeProductClosurePilotGuideStepSurface BuildStep(
        int order,
        string stageId,
        string command,
        string authorityClass,
        string purpose,
        string exitSignal)
    {
        return new RuntimeProductClosurePilotGuideStepSurface
        {
            Order = order,
            StageId = stageId,
            Command = command,
            AuthorityClass = authorityClass,
            Purpose = purpose,
            ExitSignal = exitSignal,
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
