using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAdapterHandoffContractService
{
    private const string ContractDocumentPath = "docs/runtime/runtime-adapter-handoff-contract.md";
    private const string SessionGatewayDocumentPath = "docs/session-gateway/adapter-handoff-contract.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeAdapterHandoffContractService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeAdapterHandoffContractSurface Build()
    {
        var errors = new List<string>();
        ValidatePath(ContractDocumentPath, "Adapter handoff contract document", errors);
        ValidatePath(SessionGatewayDocumentPath, "Session-gateway adapter handoff document", errors);

        return new RuntimeAdapterHandoffContractSurface
        {
            ContractDocumentPath = ContractDocumentPath,
            SessionGatewayDocumentPath = SessionGatewayDocumentPath,
            OverallPosture = errors.Count == 0
                ? "adapter_handoff_contract_ready"
                : "blocked_by_adapter_handoff_contract_gaps",
            Lanes = BuildLanes(),
            InspectCommands =
            [
                "inspect runtime-adapter-handoff-contract",
                "api runtime-adapter-handoff-contract",
                "inspect runtime-agent-working-modes",
                "inspect runtime-governed-agent-handoff-proof",
            ],
            RecommendedNextAction = errors.Count == 0
                ? "Use the CLI-first lane as the portable adapter baseline; treat ACP and MCP as bounded adapter projections that cannot bypass planner review or host writeback."
                : "Restore the adapter handoff contract documents before using this surface as an adapter integration anchor.",
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not implement a full ACP server.",
                "This surface does not implement a full MCP server.",
                "This surface does not grant adapters direct authority over task truth, review truth, memory truth, or platform truth.",
                "This surface does not vendor-lock the baseline to Codex, Claude, Cursor, Copilot, or any IDE.",
            ],
        };
    }

    private static RuntimeAdapterHandoffLaneSurface[] BuildLanes()
    {
        return
        [
            new RuntimeAdapterHandoffLaneSurface
            {
                LaneId = "cli_first",
                DisplayName = "CLI-first portable adapter lane",
                PriorityOrder = 1,
                TransportPosture = "portable_baseline",
                RuntimeStatus = "contracted",
                RequiredInputs =
                [
                    "repo_root",
                    "bootstrap_source_readback",
                    "formal_plan_handle_or_candidate_card_id",
                    "task_id_when_execution_is_bound",
                    "runtime inspect/api command set",
                ],
                RequiredOutputs =
                [
                    "operator readback text or api json",
                    "result-envelope.v1 for brokered execution",
                    "changed-path and validation evidence",
                    "explicit replan request when scope expands",
                ],
                AllowedRuntimeCommands =
                [
                    "inspect <runtime-surface>",
                    "api <runtime-surface>",
                    "plan init [candidate-card-id]",
                    "plan status",
                    "inspect runtime-brokered-execution <task-id>",
                ],
                NonAuthorityBoundaries =
                [
                    "cannot mutate official truth roots directly",
                    "cannot approve review or writeback its own result",
                    "cannot synthesize task truth after execution dispatch",
                    "cannot treat CLI output as a second control plane",
                ],
                CompletionSignal = "adapter returns bounded result evidence, then stops for planner review and host writeback",
            },
            new RuntimeAdapterHandoffLaneSurface
            {
                LaneId = "acp_second",
                DisplayName = "ACP-second adapter lane",
                PriorityOrder = 2,
                TransportPosture = "protocol_projection_after_cli_contract",
                RuntimeStatus = "bounded_contract_only",
                RequiredInputs =
                [
                    "same governed contract as CLI-first",
                    "ACP request envelope carrying repo_root, plan/task context, and requested Runtime surface",
                    "adapter identity and capability declaration",
                ],
                RequiredOutputs =
                [
                    "ACP response mirroring Runtime inspect/api payloads",
                    "bounded execution result or replan request",
                    "adapter capability and evidence metadata",
                ],
                AllowedRuntimeCommands =
                [
                    "inspect/api surface equivalents",
                    "plan/status equivalents only through Runtime-owned commands",
                    "brokered result submission equivalent to carves.submit_result",
                ],
                NonAuthorityBoundaries =
                [
                    "ACP does not own planning truth",
                    "ACP does not own review decisions",
                    "ACP does not widen packet scope without replan",
                    "ACP does not replace the CLI-first contract",
                ],
                CompletionSignal = "ACP adapter returns a Runtime-equivalent payload and waits for host-owned review/writeback",
            },
            new RuntimeAdapterHandoffLaneSurface
            {
                LaneId = "mcp_optional",
                DisplayName = "MCP-optional acceleration lane",
                PriorityOrder = 3,
                TransportPosture = "optional_read_model_and_tool_acceleration",
                RuntimeStatus = "bounded_contract_only",
                RequiredInputs =
                [
                    "explicit MCP tool/resource capability list",
                    "Runtime surface id or task id",
                    "read/write intent classification",
                ],
                RequiredOutputs =
                [
                    "Runtime read-model projection",
                    "bounded tool result",
                    "evidence reference for any write-intent handoff",
                ],
                AllowedRuntimeCommands =
                [
                    "read-only inspect/api projections",
                    "bounded tool calls that map back to Runtime-owned commands",
                    "submit_result only through Runtime-governed result return",
                ],
                NonAuthorityBoundaries =
                [
                    "MCP is optional acceleration, not the governance baseline",
                    "MCP resources are not official truth owners",
                    "MCP tools cannot bypass protected-root policy",
                    "MCP cannot approve, merge, or write back returned material",
                ],
                CompletionSignal = "MCP returns a bounded projection or result reference; Runtime remains the truth-ingress authority",
            },
        ];
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
