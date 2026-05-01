# CARVES Pack v1 Surface Mapping

## Purpose

This document freezes the Phase 2 mapping between:

- Pack v1 product language
- Pack v1 artifact semantics
- existing Runtime pack truth and inspect surfaces

The rule is simple:

```text
Pack v1 does not create a second control plane.
Pack v1 does not create a second truth root.
```

Pack v1 is a product-facing contract over existing Runtime-local pack lifecycle truth.

## First Principle

Pack v1 uses one product-level authored artifact:

- `docs/contracts/runtime-pack-v1.schema.json`

Runtime lifecycle truth remains in existing Runtime-local artifacts and surfaces, including:

- pack artifact validation
- declarative manifest bridge admission
- runtime pack attribution validation
- runtime-local admission
- runtime-local assignment
- current selection
- selection history
- bounded rollback
- local switch policy
- task explainability
- execution audit
- mismatch diagnostics

## Product Artifact vs Runtime Truth

These are different layers.

### Product-authored artifact

Pack v1 manifest is the authoring and validation shape for a declarative pack.

It defines:

- identity
- compatibility
- capability kinds
- requested permissions
- recipes and rubrics

It does **not** self-certify:

- artifact hash
- effective permissions
- protected-root denial
- admission outcome
- runtime selection outcome

### Runtime-owned truth

Runtime owns:

- whether the artifact is admitted
- whether it is assigned
- whether it is pinned
- whether it is rolled back
- which tasks used it
- whether mismatch was detected
- which pack-attributed execution evidence exists

## Mapping Rule

Pack v1 product actions must map onto existing Runtime pack surfaces.

If a future UX command is added, it must be a thin alias over those surfaces.

No Pack v1 action may introduce:

- a second pack lifecycle ledger
- a second admission ledger
- a second task attribution root
- a second review or execution authority

## Lifecycle Projection Model

Pack v1 lifecycle is expressed through three semantic projections.

### 1. Artifact Admission Projection

States:

- `discovered`
- `validated`
- `rejected`
- `admitted`

Runtime truth mapping:

- `validate pack-artifact <json-path>`
- `validate runtime-pack-attribution <json-path>`
- `runtime admit-pack <pack-artifact-path> --attribution <runtime-pack-attribution-path>`
- `inspect runtime-pack-admission`

Meaning:

- `discovered` is local product posture before Runtime truth exists
- `validated` means schema and compatibility checks passed
- `rejected` means validation or admission failed
- `admitted` means Runtime accepted the bounded local pack + attribution pair

### 2. Repo Selection Projection

States:

- `unassigned`
- `assigned`
- `pinned`
- `disabled`
- `rolled_back`

Runtime truth mapping:

- `runtime assign-pack <pack-id> [--pack-version <version>] [--channel <channel>] [--reason <text>]`
- `runtime pin-current-pack [--reason <text>]`
- `runtime clear-pack-pin [--reason <text>]`
- `runtime rollback-pack <selection-id> [--reason <text>]`
- `inspect runtime-pack-selection`
- `inspect runtime-pack-switch-policy`

Meaning:

- `unassigned` means there is no current selected pack for new task selection
- `assigned` means current local selection exists
- `pinned` means divergent switch/rollback is constrained by Runtime policy
- `disabled` is a product posture only until a first-class Runtime disable surface exists; v1 does not require a separate canonical disabled command
- `rolled_back` means current local selection was moved to a prior compatible selection

### 3. Diagnostic Projection

States:

- `clean`
- `mismatch_detected`
- `incompatible`
- `stale`

Runtime truth mapping:

- `inspect runtime-pack-mismatch-diagnostics`
- `inspect runtime-pack-execution-audit`
- `inspect runtime-pack-task-explainability <task-id>`

Meaning:

- diagnostics explain divergence and attribution
- diagnostics do not replace admission or selection truth

## Runtime Surface Mapping Table

| Product action | Runtime truth owner | Existing Runtime surface |
| --- | --- | --- |
| inspect pack artifact | local product artifact | `docs/contracts/runtime-pack-v1.schema.json` |
| validate pack manifest | product validation step, before admission | Pack v1 validator over `runtime-pack-v1.schema.json` |
| admit declarative pack manifest | Runtime-owned bridge over existing admission truth | `runtime admit-pack-v1 <runtime-pack-v1-manifest-path> [--channel <channel>] [--published-by <principal>] [--source-line <line>]`, `pack admit <runtime-pack-v1-manifest-path> [...]`, and `inspect runtime-pack-admission` |
| validate runtime-local admission pair | Runtime validation truth | `validate pack-artifact <json-path>` and `validate runtime-pack-attribution <json-path>` |
| admit pack | Runtime admission truth | `runtime admit-pack <pack-artifact-path> --attribution <runtime-pack-attribution-path>` and `inspect runtime-pack-admission` |
| assign pack | Runtime selection truth | `runtime assign-pack <pack-id> ...` and `inspect runtime-pack-selection` |
| pin pack | Runtime switch policy truth | `runtime pin-current-pack` and `inspect runtime-pack-switch-policy` |
| clear pin | Runtime switch policy truth | `runtime clear-pack-pin` and `inspect runtime-pack-switch-policy` |
| rollback pack | Runtime selection truth plus local audit | `runtime rollback-pack <selection-id> ...` and `inspect runtime-pack-selection` |
| explain pack effect on a task | Runtime task attribution projection | `inspect runtime-pack-task-explainability <task-id>` |
| audit pack execution footprint | Runtime execution audit projection | `inspect runtime-pack-execution-audit` |
| inspect pack mismatch | Runtime diagnostics projection | `inspect runtime-pack-mismatch-diagnostics` |

## Product UX Rule

Future Pack UX commands are allowed only as thin aliases.

Examples:

```text
carves pack validate
carves pack admit
carves pack assign
carves pack explain --task <task-id>
```

But those commands must map to existing Runtime-owned truth.

For Pack v1 manifest admission specifically:

```text
pack admit <runtime-pack-v1-manifest-path>
```

is a product-facing alias over:

```text
runtime admit-pack-v1 <runtime-pack-v1-manifest-path>
```

which in turn compiles a bounded pack-artifact plus runtime-pack-attribution pair and delegates to existing `runtime admit-pack` truth.

They may improve wording and discoverability.

They may not:

- create a second admission record
- create a second selection record
- create a second attribution record
- bypass Runtime review or safety policy

## Protected Root Rule

Protected truth roots remain Runtime-owned regardless of Pack v1 product wording.

Product language may say:

```text
this pack cannot write protected roots
```

But the enforced rule is still:

```text
Runtime denies protected-root mutation through effective policy.
```

Pack manifests request permissions.

Runtime decides effective permissions.

## Historical Attribution Rule

Pack lifecycle changes affect future selection only.

Hard rule:

```text
rollback, pin, unpin, or future disable operations do not rewrite historical task attribution.
```

Task explainability must read historical attribution records, not re-infer them from the current selection.

## Phase 2 Exit Criteria

Phase 2 is complete when:

- Pack v1 schema exists as a closed allowlist schema
- product lifecycle language maps to existing Runtime pack truth
- no second truth root is introduced
- Pack UX is explicitly constrained to thin aliases over Runtime surfaces

Phase 2 completion does **not** yet imply:

- command admission schema completion
- task attribution schema completion
- conflict merge algorithm completion
- dogfood pack completion
