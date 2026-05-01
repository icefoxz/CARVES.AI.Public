# Runtime Managed Workspace File Operation Model

## Purpose

This document freezes the file and asset execution model for CARVES:

```text
freedom in the workspace
control at official ingress
```

CARVES should not reduce agent work to a weak text-edit API.

CARVES should also not let agents write directly into official project truth without governance.

## Core Claim

CARVES is not primarily a remote text editor.

CARVES is a managed workspace system.

The official project remains protected.

The agent gets a bounded, temporary workspace where it can work with much greater freedom.

After that work is done, CARVES decides whether the result may enter official truth.

## Two Places

The model should always distinguish:

### 1. Official Project Truth

This is the protected project space.

It contains:

- official repo code
- official `.ai/` truth
- task and review truth
- evidence and audit truth
- official accepted state

Agents should not treat this as an unrestricted live scratchpad.

### 2. Managed Workspace

This is the temporary, governed work area issued for a bounded task or execution packet.

It may be implemented as:

- a worktree
- a temporary directory copy
- a container mount
- another bounded execution workspace

What matters is not the name.

What matters is that it is:

- temporary
- isolated
- auditable
- verifiable
- disposable

## Managed Workspace Rule

CARVES should give agents freedom in the managed workspace, not direct freedom over official truth.

That means the system should allow meaningful work such as:

- reading and searching files
- editing many related files
- creating, deleting, renaming, and moving files
- running formatters, generators, build, and tests
- producing diffs, patches, or artifact bundles

But official acceptance still routes through:

- review
- safety and validation
- host-routed truth ingress

## Workspace Lease

Every managed workspace should be treated as a leased execution area rather than a permanent agent-owned project.

At minimum, a workspace lease should bind:

- task identity
- allowed paths
- allowed operation classes
- allowed tools or adapters
- asset capability scope
- expiry or lifecycle boundary
- approval posture

No lease means no governed execution authority.

## File and Asset Classes

CARVES should not use one universal file strategy for every format.

### Class A - Text and Code Files

Examples:

- source files
- markdown
- plain text
- HTML/CSS/JS

These should be the most direct class.

Typical governed actions:

- inspect
- edit
- create
- remove
- rename
- move
- patch
- format

### Class B - Structured Text

Examples:

- JSON
- YAML
- XML
- TOML
- CSV

These are still text, but CARVES should prefer structured operations where possible instead of only raw string replacement.

### Class C - Tool-Generated Files

Examples:

- generated code
- scaffolding outputs
- lockfiles
- compiled metadata

These are often best produced through governed tool execution rather than hand-editing.

### Class D - Machine-Processable Complex Assets

Examples:

- images
- audio
- some office files
- some design or engine assets

These should not be rejected by default merely because they are not simple text.

They should instead follow:

- capability declaration
- authorization
- verification
- acceptance

### Class E - Black-Box Assets

Examples:

- assets without reliable adapters
- opaque proprietary formats

These may still be brought under governance, but often through:

- whole-file replacement
- metadata registration
- source tracking
- explicit review

### Class F - Control Plane Truth

Examples:

- `.ai/tasks/`
- `.ai/memory/`
- `.carves-platform/`
- secrets and governance truth

These are not ordinary workspace files.

They should stay under stronger mutation discipline than normal worker-editable project content.

## Operation Classes

CARVES should think in more than CRUD.

The bounded file operation model should at least distinguish:

1. `Inspect`
2. `Edit`
3. `Create`
4. `Remove`
5. `Transform`
6. `Replace`
7. `RegisterExternalChange`

The last class matters because CARVES does not need to directly author every asset change itself. It can also govern externally produced changes and decide whether they become official.

## Capability and Authorization Rule

For complex or non-text assets, the decisive question should not be:

- "is this file binary?"

The decisive questions should be:

- is there a declared capability?
- is the operation authorized?
- is there a verification method?
- is there an acceptance path?

This keeps CARVES future-compatible without pretending all complex assets are automatically safe.

## Native Tooling Rule

The managed workspace should allow tool execution where the task and workspace lease permit it.

Examples:

- formatters
- build
- test
- code generation
- bounded asset adapters

This is critical because real project work is not only file editing.

## Acceptance Flow

Workspace freedom does not equal official acceptance.

The result should still pass through:

- change manifest collection
- safety / boundary checks
- validation
- review and approval where required
- official truth ingress

If the result fails, the workspace can be discarded without polluting official project state.

## Relation To Existing CARVES Truth

This doctrine extends current lineage instead of replacing it.

It stays consistent with:

- worktree-based task isolation
- `runtime-agent-working-modes-and-constraint-ladder.md`
- `runtime-agent-working-modes-implementation-plan.md`
- safety-layer review
- host-routed writeback
- official truth ingress doctrine

The concrete Phase 4 managed workspace lease contract is frozen in:

- `runtime-managed-workspace-lease.md`

The first bounded Phase 6 scoped-path enforcement contract is frozen in:

- `runtime-scoped-workspace-path-policy-enforcement.md`

The first follow-on Mode D hardening profile is frozen in:

- `runtime-mode-d-scoped-task-workspace-hardening.md`

This document remains the broader doctrine layer.

The lease contract document records the first bounded Runtime slice that turns this doctrine into one queryable managed-workspace baseline.

## Non-Goals

- turning CARVES into a raw filesystem API product
- giving agents unrestricted direct write access to official project truth
- forcing every complex asset to become human-only
- introducing a second workspace truth root outside CARVES governance

## Final Rule

CARVES should maximize useful agent freedom where mistakes are disposable.

That means:

- freedom inside the managed workspace
- control at official acceptance
