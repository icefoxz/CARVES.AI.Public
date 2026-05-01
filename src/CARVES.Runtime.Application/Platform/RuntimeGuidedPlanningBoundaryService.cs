namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeGuidedPlanningBoundaryService
{
    private readonly string repoRoot;

    public RuntimeGuidedPlanningBoundaryService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeGuidedPlanningBoundarySurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        const string boundaryDocumentPath = "docs/runtime/runtime-guided-planning-intent-stabilizer-graph-boundary.md";
        const string workbenchBoundaryPath = "docs/runtime/workbench-v1-scope-and-boundary.md";
        const string sessionGatewayPath = "docs/session-gateway/session-gateway-v1.md";
        const string firstRunPacketPath = "docs/runtime/runtime-first-run-operator-packet.md";
        const string quickstartGuidePath = "docs/guides/HOST_AND_PROVIDER_QUICKSTART.md";

        ValidatePath(boundaryDocumentPath, "Guided planning boundary document", errors);
        ValidatePath(workbenchBoundaryPath, "Workbench boundary document", errors);
        ValidatePath(sessionGatewayPath, "Session Gateway v1 document", errors);
        ValidatePath(firstRunPacketPath, "First-run operator packet document", errors);
        ValidatePath(quickstartGuidePath, "Host and provider quickstart guide", errors);

        return new RuntimeGuidedPlanningBoundarySurface
        {
            BoundaryDocumentPath = boundaryDocumentPath,
            WorkbenchBoundaryPath = workbenchBoundaryPath,
            SessionGatewayPath = sessionGatewayPath,
            FirstRunPacketPath = firstRunPacketPath,
            QuickstartGuidePath = quickstartGuidePath,
            OverallPosture = errors.Count == 0
                ? "bounded_guided_planning_boundary_ready"
                : "blocked_by_guided_planning_boundary_gaps",
            AuxiliaryGraphProjections =
            [
                "mermaid_export",
                "mermaid_read_only_snapshot",
            ],
            OfficialLifecycle =
            [
                "draft",
                "approved",
                "rejected",
                "archived",
            ],
            PlanningPostures =
            [
                "emerging",
                "needs_confirmation",
                "wobbling",
                "grounded",
                "paused",
                "forbidden",
                "ready_to_plan",
            ],
            PlanningObjects =
            [
                new RuntimeGuidedPlanningObjectSurface
                {
                    ObjectId = "scope_frame",
                    Summary = "Structured conversation bridge that stabilizes goal, validation artifact, constraints, and open questions before card formation.",
                    TruthRole = "runtime_owned_guided_planning_truth",
                    WritebackEligibility = "not_official_card_truth",
                    ProjectionUse = "chat_summary_and_graph_context",
                },
                new RuntimeGuidedPlanningObjectSurface
                {
                    ObjectId = "pending_decision",
                    Summary = "Explicit unresolved choice that explains why the plan is blocked, wobbling, or waiting for user confirmation.",
                    TruthRole = "runtime_owned_guided_planning_truth",
                    WritebackEligibility = "not_official_card_truth",
                    ProjectionUse = "decision_panel_and_focus_prompts",
                },
                new RuntimeGuidedPlanningObjectSurface
                {
                    ObjectId = "candidate_card",
                    Summary = "Bounded slice projected from the scope frame so the user can confirm, pause, or forbid the slice before it enters official planning.",
                    TruthRole = "runtime_owned_guided_planning_truth",
                    WritebackEligibility = "not_official_card_truth_until_grounded",
                    ProjectionUse = "graph_node_and_chat_focus",
                },
                new RuntimeGuidedPlanningObjectSurface
                {
                    ObjectId = "grounded_card",
                    Summary = "Confirmed slice that is stable enough to enter Host-routed card writeback while remaining distinct from canonical card lifecycle.",
                    TruthRole = "host_routed_writeback_bridge",
                    WritebackEligibility = "create_card_draft_then_approve_then_plan_card_persist_only",
                    ProjectionUse = "handoff_ready_slice_summary",
                },
            ],
            AllowedProjectionSurfaces =
            [
                "workbench_graph_projection",
                "session_gateway_plan_chat",
                "operator_shell_focus_and_route_state",
            ],
            WritebackCommands =
            [
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-guided-planning-boundary"),
                RuntimeHostCommandLauncher.Cold("api", "runtime-guided-planning-boundary"),
                RuntimeHostCommandLauncher.Cold("create-card-draft", "<guided-planning-card.json>"),
                RuntimeHostCommandLauncher.Cold("approve-card", "<card-id>", "<reason...>"),
                RuntimeHostCommandLauncher.Cold("plan-card", "<card-markdown-path>", "--persist"),
            ],
            BlockedClaims =
            [
                "free_form_chat_as_official_card_truth",
                "frontend_owned_card_or_task_truth",
                "candidate_card_auto_writeback_without_confirmation",
                "mermaid_as_canonical_truth",
                "mermaid_as_primary_interactive_editor",
                "unity_as_v1_guided_planning_shell",
                "second_planner_or_second_card_root",
            ],
            RecommendedNextAction = errors.Count == 0
                ? "Use runtime-guided-planning-boundary to keep Operator chat and graph UX aligned to one Runtime-owned truth lane before any grounded slice crosses into create-card-draft."
                : "Restore the missing guided-planning boundary anchors before treating guided planning projection or card handoff as governed Runtime behavior.",
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "This surface does not implement the downstream Operator chat shell or graph canvas.",
                "This surface does not create a second planner, second approval queue, or second task truth ingress.",
                "This surface keeps focus_card_id as a scoping hint for clarification turns, not a writeback trigger.",
                "This surface keeps Mermaid auxiliary and keeps Unity outside the v1 guided planning slice.",
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
