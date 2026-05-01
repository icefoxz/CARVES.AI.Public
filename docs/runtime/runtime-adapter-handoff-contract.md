# Runtime Adapter Handoff Contract

This document defines the Runtime-owned adapter contract for external agents.

The contract is intentionally lane-based:

- `cli_first`
- `acp_second`
- `mcp_optional`

CLI-first is the portable baseline. ACP and MCP are adapter projections or accelerators over the same Runtime-owned governance contract.

## Shared Authority Rule

Adapters consume Runtime truth. They do not own Runtime truth.

Every adapter lane must preserve these boundaries:

- formal planning remains Runtime-owned
- task truth remains Runtime-owned
- acceptance contracts remain task truth, not adapter-local state
- review decisions remain planner/human review truth
- official writeback remains host-owned
- protected truth roots are not ordinary writable paths

## Lane: CLI-First

Required inputs:

- repository root
- bootstrap/readback sources
- active plan handle, candidate card, or task id
- Runtime inspect/api commands
- task-scoped execution packet when Mode E is selected

Required outputs:

- operator readback text or API JSON
- result-envelope.v1 when brokered execution returns
- changed-path evidence
- validation evidence
- explicit replan request when scope must widen

Non-authority:

- CLI output is not a second control plane
- CLI adapters cannot mutate `.ai/tasks/`, `.ai/memory/`, `.ai/artifacts/reviews/`, or `.carves-platform/` as returned worker material
- CLI adapters cannot approve review or write back their own result

## Lane: ACP-Second

ACP may wrap the same Runtime contract in a protocol request/response shape.

Required inputs:

- the same repo/task/plan context required by CLI-first
- adapter identity and capability declaration
- requested Runtime surface or bounded action

Required outputs:

- ACP response equivalent to Runtime inspect/api payload
- bounded execution result or replan request
- evidence metadata matching the Runtime result-return contract

Non-authority:

- ACP does not own planning truth
- ACP does not own review truth
- ACP cannot widen packet scope without replan
- ACP cannot replace the CLI-first baseline contract

## Lane: MCP-Optional

MCP is optional acceleration for read models and bounded tools.

Required inputs:

- declared MCP tools/resources
- Runtime surface id or task id
- read/write intent classification

Required outputs:

- Runtime read-model projection
- bounded tool result
- evidence reference for write-intent handoff

Non-authority:

- MCP resources are not official truth owners
- MCP tools cannot bypass protected-root policy
- MCP cannot approve, merge, or write back returned material

## Surface Contract

`runtime-adapter-handoff-contract` must project:

- lane ids and priority order
- required inputs
- required outputs
- allowed Runtime commands
- non-authority boundaries
- completion signal
- non-claims

## Non-Claims

This contract does not implement:

- a full ACP server
- a full MCP server
- OS/process sandboxing
- vendor-specific IDE permissions as the portable baseline

This contract only defines the adapter-facing handoff shape that all transports must preserve.
