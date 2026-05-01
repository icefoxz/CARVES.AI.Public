# Session Gateway Adapter Handoff Contract

The session gateway presents the adapter contract as a collaboration-plane entry point.

Agents should start with CLI-first readbacks, then optionally consume ACP or MCP projections when those adapters exist.

## Recommended Sequence

1. Read `runtime-adapter-handoff-contract`.
2. Read `runtime-agent-working-modes`.
3. Enter formal planning when durable work starts.
4. Use a task-bound workspace or Mode E brokered execution for writable work.
5. Return changed-path and validation evidence.
6. Stop before review/writeback.

## Gateway Rule

The gateway may guide an agent, but Runtime remains the official authority for:

- planning truth
- task truth
- acceptance contract truth
- protected truth-root policy
- review evidence
- writeback

## Transport Posture

CLI-first is the baseline because every compatible agent can execute commands or read command output.

ACP-second may package the same calls as protocol messages.

MCP-optional may expose read models and bounded tools.

No transport can bypass the Runtime-owned contract.
