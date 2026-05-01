# CARVES Pack v1 Task Attribution

## Purpose

This document freezes the Phase 3 Pack v1 rule for task-level attribution.

Pack attribution is the history snapshot that explains:

- which admitted and selected pack influenced a task run
- which Pack v1 capabilities were actually used
- which recipe or rubric items contributed to that run
- which Runtime admission decisions governed those contributions

This document is normative for Pack v1 explainability and audit.

It is not a second control plane.

It is not a second task ledger.

## Core Rule

Pack attribution is an immutable historical snapshot.

Hard rule:

```text
task explainability must read attribution records,
not re-infer history from the current selected pack.
```

This means:

- pack rollback does not rewrite historical attribution
- pin or unpin does not rewrite historical attribution
- future disable posture does not rewrite historical attribution
- later pack updates do not rewrite historical attribution

## Scope

Pack v1 attribution records only the three allowed capability families:

- `project_understanding_recipe`
- `verification_recipe`
- `review_rubric`

Pack v1 attribution does not record:

- worker backend selection
- tool adapter behavior
- scheduler policy
- merge strategy
- truth mutation authority

Those are outside Pack v1 scope.

## Attribution Object

The Pack v1 task attribution record is defined by:

- `docs/contracts/runtime-pack-task-attribution.schema.json`

The record binds one task and one run to:

- one `packSelectionId`
- one or more pack entries
- pack identity and version
- bounded contribution references
- Runtime admission references

## Meaning Of Each Layer

### Task / Run identity

`taskId` and `runId` answer:

```text
which task execution are we explaining
```

### Pack selection identity

`packSelectionId` answers:

```text
which Runtime-local selected pack posture was active for this run
```

This is a historical reference, not a pointer to the current selection posture.

### Pack entry

Each pack entry answers:

```text
which pack
which version
which channel
which artifact reference
which capability families were actually used
```

### Contribution references

Contribution refs answer:

```text
which concrete recipe or rubric items shaped this task run
```

These refs must use stable ids.

They are bounded references, not full pack blobs.

### Admission references

Admission refs answer:

```text
which Runtime decisions allowed this pack and its commands to participate
```

Pack attribution must point to Runtime decisions, not self-certifications by the pack.

## Stable Id Rule

Every attributable contribution item must have a stable id.

This includes:

- project understanding recipes
- priority rules
- verification recipes
- verification commands
- review rubrics
- review checklist items

Without stable ids, Pack v1 cannot safely provide:

- deterministic dedupe
- conflict explanation
- audit replay
- task explainability
- rollback-safe historical readback

## Runtime Ownership Rule

Pack manifests may request capability and permission inputs.

Runtime owns:

- effective permissions
- admission outcome
- command admission outcome
- execution routing
- task attribution persistence

Pack attribution therefore records Runtime-governed results, not Pack self-claims.

## Relationship To Existing Runtime Surfaces

Pack v1 task attribution is not intended to replace existing Runtime-local surfaces.

It exists to make those surfaces deterministic and replay-safe.

Primary consumers remain:

- `inspect runtime-pack-task-explainability <task-id>`
- `inspect runtime-pack-execution-audit`
- `inspect runtime-pack-mismatch-diagnostics`

Pack attribution must feed those surfaces.

Those surfaces must not reconstruct Pack history from the current selection alone.

## Non-Goals

This phase does not yet define:

- verification command admission schema
- conflict merge semantics
- pack registry or rollout state
- code adapter attribution

Those remain later Pack v1 or post-v1 work.

## Phase 3 Exit Criteria

Phase 3 is complete when:

- task attribution schema exists
- immutable historical snapshot semantics are written down
- Runtime-owned admission refs are mandatory
- contribution refs use stable ids
- Pack attribution is explicitly positioned as input to explainability and audit, not a second truth root
