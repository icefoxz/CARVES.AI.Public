# Workbench v1 Scope and Boundary

`CARD-308` defines Workbench v1 as a thin supervision surface over existing CARVES runtime truth.

Workbench is not a second control plane. It is the canonical observation and approval surface layered on top of the same application services and truth used by CLI and host surfaces.

## Product Role

Workbench v1 has two surfaces:

- browser workbench
- CLI-resident text workbench

Both surfaces must answer the same core questions:

1. What card is active now?
2. How is that card decomposed into tasks and dependencies?
3. Which worker or run is active and where is it in the execution lifecycle?
4. Why did a task fail, block, or retry?
5. What currently needs human review or approval?

## Included v1 Surfaces

Workbench v1 is explicitly limited to four read-first pages plus the equivalent CLI-resident text view:

- `Overview`
- `Card`
- `Task`
- `Review`

### Overview

Must summarize:

- current card
- current dispatch state
- blocked count
- review count
- host/runtime health
- recent check or clean results

### Card

Must project:

- card summary
- taskgraph dependencies
- task status distribution
- recent activity
- next action

### Task

Must project:

- task status
- execution run state
- evidence
- build or test outcomes
- artifacts
- failure or block reason

### Review

Must project:

- pending review items
- safety summary
- validation summary
- allowed decision actions

## CLI Workbench

CLI Workbench is part of the same product surface, not a fallback or separate mode.

Its purpose is continuous terminal-visible supervision for operators who want to keep CARVES state resident in the shell.

CLI Workbench may be:

- resident
- watch-oriented
- text-first

But it must still reuse the same read models as browser Workbench.

## Action Boundary

Workbench v1 is review-first, not admin-first.

Allowed actions are limited to:

- `approve`
- `reject`
- `done`
- `fail`
- `block`
- `sync`

These actions must:

- reuse existing host and application-service lifecycle paths
- produce the same truth writeback outcomes as CLI and host commands
- avoid any UI-only lifecycle semantics

## Explicitly Excluded From v1

Workbench v1 does **not** include:

- direct truth file editing
- direct code editing
- ad hoc state mutation in the UI
- drag-and-drop orchestration
- a second execution pipeline
- UI-local lifecycle computation

## Truth and Projection Rules

Workbench must remain projection-only.

Rules:

- Workbench reads shared application/query services.
- Workbench does not derive its own lifecycle truth from raw files.
- Workbench does not become a second cache of runtime state.
- Browser and CLI workbench surfaces must project the same underlying truth.

The shared source hierarchy is:

```text
Application Services
    ↓
Runtime Truth / TaskGraph / Review / Audit / Host surfaces
    ↓
Workbench browser surface
Workbench CLI-resident text surface
```

## Residue Isolation Rule

`CARD-308` must not absorb unrelated runtime residue into its delivery boundary.

This means:

- resident `.carves-platform` runtime drift is not part of Workbench acceptance by default
- unrelated `.ai` runtime/session residue is not part of Workbench acceptance by default
- Workbench must remain stable even when such residue exists

In practice:

- `CARD-308` delivers read models, page contracts, and review actions
- it does not promise a globally clean repository or a residue-free runtime

## Delivery Order

Workbench v1 must be delivered in this order:

1. freeze scope and boundary
2. define read models
3. implement shared query services
4. wire browser and CLI workbench shells
5. expose review-first actions
6. validate CLI / host / Workbench parity

This order is mandatory because services and truth contracts must stabilize before UI surfaces widen.

## Runtime Agent v1 Stage 5 Alignment

For Runtime Agent v1 Stage 5 thin frontend bridge:

- browser and CLI Workbench may act as a bounded bridge for non-author operators only when they stay on the same resident Host lane as the rest of Runtime truth
- Workbench remains a projection and review surface, not a second planner, second review queue, or bootstrap root
- Session Gateway shell and Workbench may coexist as bridge surfaces, but both must stay subordinate to the same Runtime-owned Host and gateway routes
- installable operator product-shell work stays outside this document and outside Runtime-owned scope

## Guided Planning Follow-On Alignment

For guided planning follow-on work:

- Workbench may project Scope Frame summaries, Pending Decisions, Candidate Cards, grounded-card readiness, and graph focus state
- Workbench remains projection-only even when the user enters through a planning graph or card-focused chat view
- candidate-card posture must not replace canonical card lifecycle
- clicking or focusing a card in Workbench may scope the next clarification turn, but it may not mutate official card or task truth by itself
- Mermaid may appear as export or read-only projection, but it is not the preferred primary interactive planning surface
