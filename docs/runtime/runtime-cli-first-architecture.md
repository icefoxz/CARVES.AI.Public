# Runtime CLI-First Architecture

## Purpose

This document freezes the preferred Runtime execution posture for the next bounded architecture line:

```text
CLI first
ACP second
MCP optional
```

It exists so `CARVES.Runtime` can stay:

- project-local and controlled on its hot path
- compatible with multiple shells or agents
- economical in high-frequency status and execution exchange
- open to bounded external integrations without making them the core runtime

This document is subordinate to:

- `runtime-constraint-ladder-and-collaboration-plane.md`
- `runtime-agent-working-modes-and-constraint-ladder.md`
- `runtime-agent-working-modes-implementation-plan.md`
- `runtime-external-agent-ingress-contract.md`
- `runtime-external-agent-protocol-bridge-contract.md`
- `runtime-external-agent-adapter-matrix.md`
- `runtime-mcp-surface-contract.md`
- `../session-gateway/event-model.md`

It does **not** claim that every listed command family or adapter is already complete.

## Core Rule

The Runtime-owned hot path should not depend on MCP.

The preferred flow is:

```text
agent or shell
-> ACP or thin adapter
-> Session Gateway or local daemon
-> CARVES CLI / Runtime-owned command surface
-> governed execution lane
```

That keeps:

- project-local execution structured
- review and approval host-routed
- context exchange compact
- external protocol bridges optional

## Why CLI First

### Lowest Common Denominator

Almost every shell, IDE, automation harness, or agent can invoke a local command.

That makes CLI the most portable control surface for project-local Runtime work.

### Easier Governance Boundary

When the rule is:

- do not mutate the repo directly
- do route high-risk actions through `carves ...`

then Runtime remains the only execution control plane.

### Lower Hot-Path Cost

CLI output can stay compact and structured:

- ids
- status
- summaries
- evidence refs
- review posture

This is preferable to repeatedly pushing large opaque payloads through a general-purpose protocol on the main execution path.

### Better Reviewability

A bounded command surface is easier to audit and easier to route through:

- accepted-operation
- review
- approval
- writeback

than a broad raw tool surface.

## Runtime Structure

The preferred v1 structure is:

```text
User
  ↓
Any Agent / IDE / Chat Shell / Workbench
  ↓
ACP or Thin Adapter
  ↓
CARVES Session Gateway / Local Daemon
  ↓
CARVES CLI Surface
  ↓
Planner -> TaskGraph -> Worker -> Safety -> Worktree -> Review
  ↓
Event Bus / Event Store
```

CLI-first does **not** mean CLI-only.

Runtime still benefits from:

- a session gateway or local daemon
- event streaming
- accepted-operation tracking
- review and approval coordination

The difference is that those layers remain subordinate to a bounded Runtime-owned command surface.

## Layer Responsibilities

### Instruction Layer

Use repository-local guidance to teach the agent:

- CARVES exists
- Runtime owns governed execution
- current state and boundary must be read before mutation

This layer teaches; it does not enforce.

### ACP Or Thin Adapter

This layer should:

- normalize frontend protocol shape
- forward bounded requests into Runtime
- subscribe to event or status projection
- render progress, review, and approval state

It should not:

- own repo mutation
- own task truth
- become a second planner

### Session Gateway Or Local Daemon

This layer should own:

- session identity and continuity
- intent routing
- accepted-operation coordination
- event streaming and notification return

It should not replace the governed execution lane.

### CARVES CLI Surface

This is the first-class Runtime command surface.

The command family should converge around:

- context
- plan
- run
- review
- approve / reject
- status
- events tail
- search / codegraph

The important rule is structural, not branding:

- compact output
- machine-readable output
- governed action classes
- no raw repo write surface as the default public contract

### MCP Optional Layer

MCP remains valuable, but only as an optional extension layer.

Good MCP use cases:

- bounded read-only resources
- low-output Runtime request tools
- external system integration
- enterprise-side bridge surfaces

Bad MCP use cases for the hot path:

- direct repo mutation
- direct task or review truth mutation
- high-frequency main execution control
- large unbounded context transfer

## MCP Rule

MCP should stay:

- read-biased
- low-output
- narrow
- subordinate to Runtime-owned mutation authority

Recommended MCP-facing surfaces remain things like:

- bounded status or inspect
- bounded task packet reads
- governed run requests
- review or approval attention requests

MCP should **not** become the primary project-local execution engine.

## ACP Rule

ACP or ACP-like adapters are the preferred protocol shape for frontend-to-agent or IDE-to-agent attachment when a standard protocol bridge is useful.

They are a good fit for:

- session-oriented shells
- IDE integrations
- thin adapters over Runtime-owned event and command surfaces

They remain adapters, not truth owners.

## Minimal V1 Implementation Order

The next bounded implementation order should be:

1. keep CLI or host-routed command surfaces as the primary control lane
2. keep Session Gateway or local daemon as the collaboration and session layer
3. expose compact event or status streaming for high-value workflows
4. add ACP or thin-adapter attachment where needed
5. add MCP only for bounded read or request surfaces

The wrong order would be:

1. widen MCP into the main runtime
2. expose raw repo write tools
3. retrofit governance later

## Non-Goals

This document does not claim:

- that MCP should be abandoned
- that CLI eliminates the need for a gateway or event layer
- that every frontend must use ACP
- that raw repo shell access should become a public Runtime protocol
- that Runtime should become a general-purpose protocol hub instead of a governed execution system
