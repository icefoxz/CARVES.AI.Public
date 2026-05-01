namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAcceptanceContractIngressPolicyService
{
    private readonly string repoRoot;

    public RuntimeAcceptanceContractIngressPolicyService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeAcceptanceContractIngressPolicySurface Build()
    {
        var errors = new List<string>();

        const string policyDocumentPath = "docs/runtime/acceptance-contract-driven-planning.md";
        const string schemaPath = "docs/contracts/acceptance-contract.schema.json";

        ValidatePath(policyDocumentPath, "Acceptance contract planning doctrine document", errors);
        ValidatePath(schemaPath, "Acceptance contract canonical schema", errors);

        return new RuntimeAcceptanceContractIngressPolicySurface
        {
            PolicyDocumentPath = policyDocumentPath,
            SchemaPath = schemaPath,
            OverallPosture = errors.Count == 0
                ? "bounded_acceptance_contract_ingress_policy_ready"
                : "blocked_by_acceptance_contract_ingress_policy_gaps",
            PlanningTruthMutationPolicy = "auto_minimum_contract",
            ExecutionDispatchPolicy = "explicit_gap_required",
            PolicySummary = "planning_truth_mutation=auto_minimum_contract; execution_dispatch=explicit_gap_required",
            Ingresses =
            [
                new RuntimeAcceptanceContractIngressLaneSurface
                {
                    IngressId = "card_draft_ingress",
                    LaneKind = "planning_truth_preparation",
                    ContractPolicy = "auto_minimum_contract",
                    MissingContractOutcome = "normalize a bounded minimum acceptance contract during card draft writeback",
                    Triggers =
                    [
                        "create-card-draft",
                        "update-card-draft",
                    ],
                    SourceAnchors =
                    [
                        "PlanningDraftService.CreateCardDraft",
                        "PlanningDraftService.UpdateCardDraft",
                        "AcceptanceContractFactory.NormalizeCardContract",
                    ],
                    RecommendedAction = "Provide an explicit acceptance_contract when the intent already requires stricter checks, evidence, or human review policy.",
                },
                new RuntimeAcceptanceContractIngressLaneSurface
                {
                    IngressId = "taskgraph_draft_approval_ingress",
                    LaneKind = "planning_truth_mutation",
                    ContractPolicy = "auto_minimum_contract",
                    MissingContractOutcome = "materialize governed task truth only after each executable draft task records an explicit or synthesized minimum acceptance contract projection",
                    Triggers =
                    [
                        "create-taskgraph-draft",
                        "approve-taskgraph-draft",
                        "inspect taskgraph-draft",
                    ],
                    SourceAnchors =
                    [
                        "PlanningDraftService.ApproveTaskGraphDraft",
                        "TaskGraphAcceptanceContractMaterializationGuard",
                        "AcceptanceContractFactory.NormalizeTaskContract",
                    ],
                    RecommendedAction = "Inspect the taskgraph draft before approval; attach explicit task-level acceptance_contract data when the draft already knows the required checks or evidence set, otherwise rely only on the recorded synthesized_minimum projection.",
                },
                new RuntimeAcceptanceContractIngressLaneSurface
                {
                    IngressId = "planner_proposal_acceptance_ingress",
                    LaneKind = "planning_truth_mutation",
                    ContractPolicy = "auto_minimum_contract",
                    MissingContractOutcome = "materialize planner-proposed tasks with a normalized minimum task contract during proposal acceptance",
                    Triggers =
                    [
                        "planner proposal acceptance",
                    ],
                    SourceAnchors =
                    [
                        "PlannerProposalAcceptanceService.Accept",
                        "AcceptanceContractFactory.NormalizeTaskContract",
                    ],
                    RecommendedAction = "Upgrade planner-proposed tasks with explicit acceptance_contract content before acceptance when the planner already knows tighter acceptance evidence or review policy.",
                },
                new RuntimeAcceptanceContractIngressLaneSurface
                {
                    IngressId = "execution_dispatch_ingress",
                    LaneKind = "execution_dispatch",
                    ContractPolicy = "explicit_gap_required",
                    MissingContractOutcome = "block dispatch and project acceptance_contract_gate guidance onto operator surfaces",
                    Triggers =
                    [
                        "task run",
                        "scheduler dispatch",
                        "planner-worker prepare",
                    ],
                    SourceAnchors =
                    [
                        "AcceptanceContractExecutionGate",
                        "TaskScheduler",
                        "DevLoopService.Dispatch",
                        "PlannerWorkerCycle",
                    ],
                    RecommendedAction = "Project a minimum acceptance contract onto task truth before dispatch; execution lanes do not synthesize missing contracts at run time.",
                },
            ],
            RecommendedNextAction = errors.Count == 0
                ? "Treat planning truth mutation as the only lane allowed to synthesize a bounded minimum acceptance contract; once task truth exists, execution dispatch must fail closed on missing contracts."
                : "Restore the missing acceptance contract doctrine anchors before treating ingress classification as governed Runtime behavior.",
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not create a second planner, second task graph, or second execution gate.",
                "This surface does not allow execution dispatch to synthesize a missing contract after task truth already exists.",
                "This surface does not force every planning ingress to generate identical evidence requirements; it only classifies missing-contract behavior.",
            ],
        };
    }

    private void ValidatePath(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
