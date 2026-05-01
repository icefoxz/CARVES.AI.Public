# Pack v1 Unified Attribution Audit and Mismatch v1

## Scope

This phase closes the bounded integration line for declarative Pack v1 attribution, execution audit, and mismatch diagnostics.

The work in this phase is limited to:

- snapshotting declarative manifest contributions into existing Runtime-selected pack attribution
- persisting that snapshot through existing execution run and execution run report truth
- projecting declarative contribution evidence through existing task explainability, execution audit, and mismatch diagnostics surfaces

This phase does **not**:

- open Adapter Host
- open Marketplace or registry distribution
- grant Pack-owned truth mutation
- grant Review verdict or Merge authority
- replace existing Runtime admission, selection, or review truth

## Runtime integration result

Selected declarative Pack v1 manifests now contribute a bounded historical snapshot to `RuntimePackExecutionAttribution`.

That snapshot records:

- manifest path
- contribution fingerprint
- declared capability kinds
- project-understanding recipe ids
- verification recipe ids
- verification command ids
- review rubric ids
- review checklist item ids

Execution runs and execution run reports persist the selected pack snapshot as historical truth at run time.

## Audit and mismatch behavior

The following Runtime-owned surfaces now consume declarative contribution snapshots:

- `inspect runtime-pack-task-explainability <task-id>`
- `inspect runtime-pack-execution-audit`
- `inspect runtime-pack-mismatch-diagnostics`
- `pack explain --task <task-id>`
- `pack audit`
- `pack mismatch`

Mismatch diagnostics may now distinguish:

- identity drift between recent execution and current selection
- declarative contribution drift between historical run snapshots and the current selected manifest snapshot

## Boundary posture

- declarative contribution snapshots are attached to existing Runtime-owned execution truth
- no second control plane or second truth root is created
- mismatch diagnostics remain advisory and do not mutate selection truth
- historical task and run attribution is not recomputed from the current selected manifest

## Closure wording

The accurate closure wording for this phase is:

```text
Pack v1 unified attribution / audit / mismatch over declarative packs: completed
```

The inaccurate closure wording is:

```text
Pack v1 full Runtime integration: completed
```
