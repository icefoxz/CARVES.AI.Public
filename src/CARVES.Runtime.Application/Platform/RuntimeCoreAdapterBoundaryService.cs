using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeCoreAdapterBoundaryService
{
    private readonly RuntimeArtifactCatalogService artifactCatalogService;

    public RuntimeCoreAdapterBoundaryService(string repoRoot, ControlPlanePaths paths, SystemConfig systemConfig)
    {
        artifactCatalogService = new RuntimeArtifactCatalogService(repoRoot, paths, systemConfig);
    }

    public RuntimeCoreAdapterBoundarySurface Build()
    {
        var catalog = artifactCatalogService.LoadOrBuild(persist: false);
        var catalogFamilies = catalog.Families
            .Select(BuildFamilyDescriptor)
            .OrderBy(item => item.Classification, StringComparer.Ordinal)
            .ThenBy(item => item.FamilyId, StringComparer.Ordinal)
            .ToArray();

        RuntimeBoundaryFamilyDescriptor[] projectionFamilies =
        [
            new RuntimeBoundaryFamilyDescriptor
            {
                FamilyId = "agent_bootstrap_projection",
                DisplayName = "Agent bootstrap projection",
                Classification = "projection",
                LifecycleClass = "projection_only",
                Summary = "AGENTS.md and repo-local agent bootstrap assets remain consumer-facing projections over canonical runtime and CARVES truth.",
                PathRefs =
                [
                    "AGENTS.md",
                    ".codex/config.toml",
                    ".codex/rules/",
                    ".codex/skills/",
                ],
                TruthRefs =
                [
                    "README.md",
                    ".ai/memory/architecture/00_AI_ENTRY_PROTOCOL.md",
                    ".ai/memory/architecture/04_EXECUTION_RUNBOOK_CONTRACT.md",
                    ".ai/memory/architecture/05_EXECUTION_OS_METHODOLOGY.md",
                    ".ai/PROJECT_BOUNDARY.md",
                    ".ai/STATE.md",
                    ".ai/TASK_QUEUE.md",
                    ".ai/DEV_LOOP.md",
                ],
                Notes =
                [
                    "Current bootstrap files remain human-authored, but the boundary treats them as downstream guidance rather than core truth owners.",
                    "Future generation cards may automate these projections without changing the upstream truth set named here.",
                ],
            },
            new RuntimeBoundaryFamilyDescriptor
            {
                FamilyId = "provider_adapter_implementation",
                DisplayName = "Provider adapter implementation",
                Classification = "adapter_artifact",
                LifecycleClass = "implementation_boundary",
                Summary = "Provider- and agent-specific adapter implementations stay bounded to infrastructure code and must not redefine canonical runtime truth.",
                PathRefs = ["src/CARVES.Runtime.Infrastructure/AI/"],
                TruthRefs =
                [
                    "WorkerAdapterFactory",
                    "PlannerAdapterFactory",
                    "ConfiguredWorkerAdapter",
                    "RuntimePackDistributionBoundaryService",
                ],
                Notes =
                [
                    "This family is implementation-only; persisted truth remains in the runtime roots already classified below.",
                    "Codex, Claude, Gemini, OpenAI-compatible, and local adapters stay symmetric under this boundary.",
                ],
            },
            new RuntimeBoundaryFamilyDescriptor
            {
                FamilyId = "operator_surface_projection",
                DisplayName = "Operator surface projection",
                Classification = "projection",
                LifecycleClass = "projection_only",
                Summary = "inspect/api/dashboard text stays a projection over canonical JSON truth, adapter artifacts, and bounded mirrors.",
                PathRefs = [],
                TruthRefs =
                [
                    "inspect runtime-core-adapter-boundary",
                    "api runtime-core-adapter-boundary",
                    "inspect execution-contract-surface",
                    "inspect runtime-pack-distribution-boundary",
                ],
                Notes =
                [
                    "Operator text is explainability and collaboration surface, not a new truth owner.",
                ],
            },
        ];

        return new RuntimeCoreAdapterBoundarySurface
        {
            Summary = "CARVES runtime now classifies current persisted families across a second boundary axis: core truth, adapter artifact, or projection. This boundary is additive over lifecycle classes such as canonical truth, governed mirror, live state, and operational history; it does not move storage roots in this card line.",
            Families = [.. catalogFamilies, .. projectionFamilies],
            CoreContracts = BuildCoreContracts(),
            Notes =
            [
                "Classification is semantic and adapter-neutral; it does not replace the existing lifecycle and retention catalog.",
                "Provider-specific detail remains bounded to adapter artifacts even when some current files are still versioned or machine-readable truth today.",
                "AGENTS.md and repo-local bootstrap assets remain usable, but they are treated as projections fed by canonical CARVES and runtime truth rather than as upstream truth owners.",
            ],
        };
    }

    private static RuntimeBoundaryFamilyDescriptor BuildFamilyDescriptor(RuntimeArtifactFamilyPolicy family)
    {
        return new RuntimeBoundaryFamilyDescriptor
        {
            FamilyId = family.FamilyId,
            DisplayName = family.DisplayName,
            Classification = Classify(family.FamilyId),
            LifecycleClass = family.ArtifactClass.ToString(),
            Summary = family.Summary,
            PathRefs = family.Roots.ToArray(),
            TruthRefs = BuildTruthRefs(family.FamilyId),
            Notes = BuildNotes(family.FamilyId),
        };
    }

    private static string Classify(string familyId)
    {
        return familyId switch
        {
            "platform_provider_definition_truth" => "adapter_artifact",
            "platform_provider_live_state" => "adapter_artifact",
            "worker_execution_artifact_history" => "adapter_artifact",
            "governed_markdown_mirror" => "projection",
            "context_pack_projection" => "projection",
            "execution_packet_mirror" => "projection",
            "sustainability_projection" => "projection",
            "ephemeral_runtime_residue" => "projection",
            _ => "core_truth",
        };
    }

    private static string[] BuildTruthRefs(string familyId)
    {
        return familyId switch
        {
            "task_truth" => [".ai/tasks/graph.json", ".ai/tasks/nodes/", ".ai/tasks/cards/"],
            "memory_truth" => [".ai/memory/architecture/", ".ai/memory/modules/", ".ai/memory/project/", ".ai/memory/patterns/"],
            "execution_memory_truth" => [".ai/memory/execution/", "PlannerEmergenceService"],
            "routing_truth" =>
            [
                ".carves-platform/runtime-state/active_routing_profile.json",
                ".carves-platform/runtime-state/candidate_routing_profile.json",
                ".carves-platform/runtime-state/qualification_matrix.json",
            ],
            "platform_definition_truth" => [".ai/config/", ".carves-platform/policies/", ".carves-platform/repos/registry.json"],
            "validation_suite_truth" => [".ai/validation/tasks/", "RoutingValidationService"],
            "governed_markdown_mirror" => [".ai/STATE.md", ".ai/TASK_QUEUE.md", ".ai/CURRENT_TASK.md"],
            "codegraph_derived" => [".ai/codegraph/manifest.json", ".ai/codegraph/modules/", ".ai/codegraph/summaries/"],

            "runtime_pack_admission_evidence" => ["runtime admit-pack", "inspect runtime-pack-admission"],
            "runtime_pack_selection_evidence" => ["runtime assign-pack", "inspect runtime-pack-selection"],
            "runtime_pack_switch_policy_evidence" => ["runtime pin-current-pack", "inspect runtime-pack-switch-policy"],
            "runtime_pack_admission_policy_evidence" => ["inspect runtime-pack-admission-policy"],
            "runtime_pack_selection_audit_evidence" => ["runtime rollback-pack", "inspect runtime-pack-selection"],
            "runtime_pack_policy_audit_evidence" => ["inspect runtime-pack-policy-audit"],
            "runtime_pack_policy_preview_evidence" => ["runtime preview-pack-policy", "inspect runtime-pack-policy-preview"],
            "context_pack_projection" => ["ContextPackService", "inspect context-pack <task-id>"],
            "execution_packet_mirror" => ["ExecutionPacketCompilerService", "inspect execution-packet <task-id>"],
            "sustainability_projection" => ["inspect sustainability", "audit sustainability"],
            "planning_runtime_history" => [".ai/runtime/planning/", "create-card-draft", "create-taskgraph-draft"],
            "execution_surface_history" => [".ai/execution/", "task run <task-id>"],
            "validation_trace_history" => [".ai/validation/traces/", "validation trace history"],
            "validation_summary_history" => [".ai/validation/summaries/", "validation summary"],
            "execution_run_detail_history" => [".ai/runtime/runs/", "inspect run <run-id>"],
            "execution_run_report_history" => [".ai/runtime/run-reports/", "ExecutionRunReportService"],
            "runtime_failure_detail_history" => [".ai/failures/", ".ai/artifacts/runtime-failures/"],
            "worker_execution_artifact_history" => [".ai/artifacts/worker/", ".ai/artifacts/worker-executions/", ".ai/artifacts/provider/", ".ai/artifacts/worker-permissions/"],
            "platform_runtime_ledger_history" =>
            [
                ".carves-platform/runtime-state/qualification_run_ledger.json",
                ".carves-platform/runtime-state/delegated_run_lifecycles.json",
                ".carves-platform/runtime-state/delegated_run_recovery_ledger.json",
            ],
            "platform_provider_definition_truth" => [".carves-platform/providers/", ".carves-platform/providers/registry.json"],
            "platform_provider_live_state" => [".carves-platform/runtime-state/providers/"],
            "runtime_live_state" => [".ai/runtime/live-state/"],
            "platform_live_state" =>
            [
                ".carves-platform/runtime-state/fleet/",
                ".carves-platform/runtime-state/sessions/",
                ".carves-platform/runtime-state/workers/",
                ".carves-platform/runtime-state/host/",
                ".carves-platform/runtime-state/delegation/",
            ],
            "incident_audit_archive" => [".carves-platform/runtime-state/events/"],
            "sustainability_archive" => [".ai/runtime/sustainability/archive/"],
            "ephemeral_runtime_residue" => [".carves-temp/", ".ai/runtime/tmp/", ".ai/runtime/staging/", ".carves-platform/runtime-state/*.tmp"],
            _ => [],
        };
    }

    private static string[] BuildNotes(string familyId)
    {
        return familyId switch
        {
            "platform_provider_definition_truth" =>
            [
                "Lifecycle class stays CanonicalTruth for current persistence, but semantic classification is adapter_artifact because the files only make sense in the presence of provider backends and adapter routing.",
            ],
            "platform_provider_live_state" =>
            [
                "This family is provider-specific live drift and must never be promoted into neutral core truth.",
            ],
            "worker_execution_artifact_history" =>
            [
                "Worker/provider/review artifacts remain bounded evidence and should not become upstream truth for task, run, or safety semantics.",
                "Review artifacts currently persist in the same family for compatibility; future migration may split planner review evidence from provider-adapter detail.",
            ],
            "governed_markdown_mirror" =>
            [
                "Markdown mirrors remain collaboration views generated from machine truth rather than primary truth owners.",
            ],
            "context_pack_projection" =>
            [
                "Context packs are execution projections assembled for bounded AI consumption and must not replace local truth roots.",
            ],
            "execution_packet_mirror" =>
            [
                "Execution packets remain canonical contracts, but the stored packet files are treated here as projection mirrors over broader task, memory, and codegraph truth.",
            ],
            "sustainability_projection" =>
            [
                "Sustainability reports stay projection-only and regenerate from runtime artifact catalog and archive truth.",
            ],
            "ephemeral_runtime_residue" =>
            [
                "Ephemeral residue has no truth ownership and remains projection-class cleanup material only.",
            ],
            _ => [],
        };
    }

    private static RuntimeNeutralCoreContractDescriptor[] BuildCoreContracts()
    {
        return
        [
            Contract(
                "card",
                "Card",
                "docs/contracts/card.schema.json",
                "Canonical card truth remains meaningful without any specific agent or provider and defines planning scope, goal, acceptance, and lifecycle intent.",
                "Provider-specific extensions may appear only as bounded refs such as provider_id, profile_id, or artifact_ref carried in adjacent runtime evidence rather than embedded card payload.",
                [".ai/tasks/cards/*.md", "docs/contracts/card.schema.json"],
                ["provider_id", "profile_id", "artifact_ref"],
                ["raw_provider_response", "session_handle", "cli_transcript"]),
            Contract(
                "task",
                "Task",
                "docs/contracts/task.schema.json",
                "Canonical task truth remains neutral across providers and records lifecycle, scope, acceptance, planner review, and bounded metadata.",
                "Adapter-specific detail may only enter task truth by bounded references such as worker_run_id, provider_detail_ref, or routing_profile_id.",
                [".ai/tasks/nodes/*.json", "docs/contracts/task.schema.json"],
                ["worker_run_id", "provider_detail_ref", "routing_profile_id", "pack_attribution_ref"],
                ["raw_provider_payload", "approval_transcript", "adapter_prompt_body"]),
            Contract(
                "taskgraph_node",
                "TaskGraphNode",
                "docs/contracts/taskgraph.schema.json",
                "TaskGraph node truth stays canonical for dependency, dispatch, and card-task topology independent of any single adapter.",
                "Provider-specific routing or worker detail must remain in referenced execution/run evidence rather than in graph topology objects.",
                [".ai/tasks/graph.json", "docs/contracts/taskgraph.schema.json"],
                ["task_id", "card_id", "artifact_ref"],
                ["raw_worker_output", "provider_session_state", "adapter_specific_prompt"]),
            Contract(
                "execution_packet",
                "ExecutionPacket",
                "docs/contracts/execution-packet.schema.json",
                "ExecutionPacket is a neutral planner-issued contract compiled from task, memory, and codegraph truth before any adapter-specific transport begins.",
                "Provider-specific routing stays bounded to references such as provider_id, profile_id, routing_profile_id, and detail_ref rather than embedded request or transcript payloads.",
                ["inspect execution-packet <task-id>", "docs/contracts/execution-packet.schema.json", "ExecutionPacketCompilerService"],
                ["provider_id", "profile_id", "routing_profile_id", "detail_ref"],
                ["raw_provider_request", "raw_provider_response", "session_handle"]),
            Contract(
                "execution_run_report",
                "ExecutionRunReport",
                "docs/contracts/execution-run-report.schema.json",
                "Execution run reports remain neutral execution truth describing run outcome, evidence, and bounded attribution for replay and audit.",
                "Adapter-specific details may appear only as references such as worker_detail_ref, provider_detail_ref, and selected_pack_ref.",
                [".ai/runtime/run-reports/", "docs/contracts/execution-run-report.schema.json", "ExecutionRunReportService"],
                ["worker_detail_ref", "provider_detail_ref", "selected_pack_ref"],
                ["raw_transcript", "provider_completion_blob", "full_approval_chat"]),
            Contract(
                "safety_verdict",
                "SafetyVerdict",
                "docs/contracts/safety.schema.json",
                "Safety verdict truth remains a neutral boundary decision contract over execution evidence, budget, and policy rather than any provider transport detail.",
                "Provider-specific safety detail may only appear through issue refs or artifact refs attached to the verdict, not embedded transcripts or session state.",
                [".ai/artifacts/safety/", "docs/contracts/safety.schema.json", "BoundaryDecisionService"],
                ["issue_ref", "artifact_ref", "policy_ref"],
                ["provider_raw_trace", "session_state_blob", "full_worker_transcript"]),
        ];
    }

    private static RuntimeNeutralCoreContractDescriptor Contract(
        string contractId,
        string displayName,
        string schemaPath,
        string summary,
        string providerExtensionBoundary,
        string[] truthRefs,
        string[] allowedExtensionReferences,
        string[] forbiddenEmbeddedPayloads)
    {
        return new RuntimeNeutralCoreContractDescriptor
        {
            ContractId = contractId,
            DisplayName = displayName,
            SchemaPath = schemaPath,
            Summary = summary,
            ProviderExtensionBoundary = providerExtensionBoundary,
            TruthRefs = truthRefs,
            AllowedExtensionReferences = allowedExtensionReferences,
            ForbiddenEmbeddedPayloads = forbiddenEmbeddedPayloads,
            Notes =
            [
                "This card line defines visibility and boundary semantics; it does not rename existing truth files or change transport implementations.",
            ],
        };
    }
}
