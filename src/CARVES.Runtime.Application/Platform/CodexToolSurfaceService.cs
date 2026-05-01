using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class CodexToolSurfaceService
{
    public CodexToolSurfaceSnapshot Build()
    {
        var tools = new[]
        {
            new CodexToolDescriptor
            {
                ToolId = "get_task",
                DisplayName = "Get Task",
                ActionClass = CodexToolActionClass.WorkerAllowed,
                Availability = CodexToolAvailability.Available,
                Summary = "Load current task truth through CARVES instead of reading repo truth files directly.",
                CurrentCommandMappings = ["task inspect <task-id>", "inspect task <task-id>"],
                Notes =
                [
                    "Canonical task truth stays in CARVES task graph and task-node JSON.",
                    "Use this before delegated execution instead of inferring state from thread text.",
                ],
            },
            new CodexToolDescriptor
            {
                ToolId = "get_execution_packet",
                DisplayName = "Get Execution Packet",
                ActionClass = CodexToolActionClass.WorkerAllowed,
                Availability = CodexToolAvailability.Available,
                Summary = "Retrieve the compiled execution packet that will later constrain Codex work to packet-scoped context and actions.",
                CurrentCommandMappings = ["inspect execution-packet <task-id>", "api execution-packet <task-id>"],
                DependsOnCards = ["CARD-300", "CARD-301"],
                Notes =
                [
                    "ExecutionPacket contract is defined by CARD-300.",
                    "Packet compiler and context assembler now compile packet truth from task, memory, and codegraph state.",
                ],
            },
            new CodexToolDescriptor
            {
                ToolId = "load_memory_bundle",
                DisplayName = "Load Memory Bundle",
                ActionClass = CodexToolActionClass.WorkerAllowed,
                Availability = CodexToolAvailability.Available,
                Summary = "Load current bounded context through CARVES context-pack truth rather than direct memory scans.",
                CurrentCommandMappings = ["inspect execution-packet <task-id>", "inspect context-pack <task-id>"],
                DependsOnCards = ["CARD-301"],
                Notes =
                [
                    "ExecutionPacket now makes the context assembly order explicit as Architecture, Relevant Modules, Current Task Files.",
                    "Context-pack truth remains the bounded prompt layer behind the packet surface.",
                ],
            },
            new CodexToolDescriptor
            {
                ToolId = "query_codegraph",
                DisplayName = "Query CodeGraph",
                ActionClass = CodexToolActionClass.WorkerAllowed,
                Availability = CodexToolAvailability.Available,
                Summary = "Query CARVES-owned codegraph projections instead of reading full graph truth or repository-wide structure directly.",
                CurrentCommandMappings = ["inspect execution-packet <task-id>", "show-graph", "inspect context-pack <task-id>"],
                DependsOnCards = ["CARD-301"],
                Notes =
                [
                    "Packet compilation now records packet-aligned codegraph queries in stable truth.",
                    "Summary-first codegraph retrieval remains the default read path.",
                ],
            },
            new CodexToolDescriptor
            {
                ToolId = "submit_result",
                DisplayName = "Submit Result",
                ActionClass = CodexToolActionClass.WorkerAllowed,
                Availability = CodexToolAvailability.Available,
                TruthAffecting = true,
                Summary = "Submit worker result envelopes back into CARVES for validation and review gating.",
                CurrentCommandMappings = ["task ingest-result <task-id>"],
                Notes =
                [
                    "TaskResultEnvelope contract is defined by CARD-300.",
                    "Worker submission is allowed through CARVES, but it is not equivalent to review approval or final truth writeback.",
                    "Safety and review remain separate gates after submission.",
                    "Packet enforcement rejects planner-only lifecycle requests and truth-write attempts before writeback.",
                ],
            },
            new CodexToolDescriptor
            {
                ToolId = "request_replan",
                DisplayName = "Request Replan",
                ActionClass = CodexToolActionClass.WorkerAllowed,
                Availability = CodexToolAvailability.Partial,
                Summary = "Escalate execution drift or boundary stop back into CARVES rather than silently widening task scope.",
                CurrentCommandMappings = ["task retry <task-id> <reason...>", "inspect replan <task-id>"],
                DependsOnCards = ["CARD-300", "CARD-301"],
                Notes =
                [
                    "Current surface is retry-oriented and inspectable, not yet a dedicated request_replan endpoint.",
                    "Planner verdict contracts are defined by CARD-300.",
                    "Planner-owned recovery stays outside worker lifecycle truth even after packet compilation.",
                ],
            },
            new CodexToolDescriptor
            {
                ToolId = "review_task",
                DisplayName = "Review Task",
                ActionClass = CodexToolActionClass.PlannerOnly,
                Availability = CodexToolAvailability.Available,
                TruthAffecting = true,
                Summary = "Apply planner-owned review verdicts through CARVES instead of allowing workers to self-approve lifecycle truth.",
                CurrentCommandMappings = ["review-task <task-id> <verdict> <reason...>", "approve-review <task-id> <reason...>", "reject-review <task-id> <reason...>", "reopen-review <task-id> <reason...>"],
                Notes =
                [
                    "Review is a stateful control-plane action and must remain CARVES-owned.",
                    "Worker execution may gather evidence, but it does not own review or approval.",
                ],
            },
            new CodexToolDescriptor
            {
                ToolId = "sync_state",
                DisplayName = "Sync State",
                ActionClass = CodexToolActionClass.PlannerOnly,
                Availability = CodexToolAvailability.Available,
                TruthAffecting = true,
                Summary = "Reconcile task graph, projections, and collaboration views after validated writeback.",
                CurrentCommandMappings = ["sync-state"],
                Notes =
                [
                    "sync-state is a control-plane reconciliation action, not a worker local step.",
                ],
            },
            new CodexToolDescriptor
            {
                ToolId = "audit_runtime",
                DisplayName = "Audit Runtime",
                ActionClass = CodexToolActionClass.PlannerOnly,
                Availability = CodexToolAvailability.Available,
                TruthAffecting = true,
                Summary = "Run CARVES-owned runtime or sustainability audit surfaces after execution or maintenance changes.",
                CurrentCommandMappings = ["audit sustainability", "inspect sustainability"],
                Notes =
                [
                    "Audit remains a control-plane truth surface even when a Codex-facing registry exposes it.",
                ],
            },
            new CodexToolDescriptor
            {
                ToolId = "read_code",
                DisplayName = "Read Code",
                ActionClass = CodexToolActionClass.LocalEphemeral,
                Availability = CodexToolAvailability.Available,
                Summary = "Read relevant source and tests inside the current allowed task scope.",
                CurrentCommandMappings = ["local worktree read"],
                Notes =
                [
                    "Local ephemeral actions remain packet-bound and do not directly mutate lifecycle truth.",
                ],
            },
            new CodexToolDescriptor
            {
                ToolId = "edit_code",
                DisplayName = "Edit Code",
                ActionClass = CodexToolActionClass.LocalEphemeral,
                Availability = CodexToolAvailability.Available,
                Summary = "Edit source and tests inside the current allowed task scope.",
                CurrentCommandMappings = ["local worktree edit"],
                Notes =
                [
                    "Edits are local execution actions and only become accepted truth after CARVES validation and writeback.",
                ],
            },
            new CodexToolDescriptor
            {
                ToolId = "run_build",
                DisplayName = "Run Build",
                ActionClass = CodexToolActionClass.LocalEphemeral,
                Availability = CodexToolAvailability.Available,
                Summary = "Run targeted build verification inside the current worktree.",
                CurrentCommandMappings = ["local worktree build"],
            },
            new CodexToolDescriptor
            {
                ToolId = "run_test",
                DisplayName = "Run Test",
                ActionClass = CodexToolActionClass.LocalEphemeral,
                Availability = CodexToolAvailability.Available,
                Summary = "Run targeted test verification inside the current worktree.",
                CurrentCommandMappings = ["local worktree test"],
            },
        };

        return new CodexToolSurfaceSnapshot
        {
            Summary = "CARVES-owned Codex tool registry exposing planner-only actions, worker-allowed control-plane actions, and packet-scoped local ephemeral actions.",
            Tools = tools,
        };
    }
}
