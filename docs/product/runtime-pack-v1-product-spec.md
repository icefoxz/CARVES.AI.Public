# CARVES Pack v1 Product Spec

## Status

This document freezes **Phase 1** of Pack v1:

- product definition
- scope
- non-goals
- authority boundary
- Pack v1 capability model
- Pack v1 risk model

It is a **decision baseline** and a **normative product-direction document**.

It is **not yet** the full engineering implementation spec for Pack v1.

Follow-on required artifacts remain separate:

- `docs/contracts/runtime-pack-v1.schema.json`
- `docs/contracts/runtime-pack-command-admission.schema.json`
- `docs/contracts/runtime-pack-task-attribution.schema.json`
- `docs/product/runtime-pack-conflict-resolution-v1.md`
- `docs/product/runtime-pack-v1-surface-mapping.md`

## Product Definition

Pack v1 is **not** an Adapter Host and **not** a marketplace.

Pack v1 is the productized convergence of existing Runtime-local pack policy.

Plain meaning:

```text
Pack v1 is a Runtime-governed declarative capability bundle.
```

Pack v1 exists to enhance three bounded parts of CARVES behavior:

- project understanding
- verification recipes
- review rubrics

Pack v1 does **not** become the execution subject.

The execution subject remains:

```text
CARVES Runtime
```

## Core Boundary

Pack v1 may contribute:

- project signals
- context priority hints
- include and exclude rules
- verification recipes
- review checklists
- risk hints
- evidence expectations

Pack v1 may **not** own:

- TaskGraph truth
- task state mutation
- Safety final gate
- Review verdicts
- Worktree lifecycle
- merge authority
- Truth Writeback
- protected-root mutation

This product boundary is mandatory:

```text
Pack enhances CARVES understanding, verification, and review preparation.
Runtime retains final authority over execution, review, and truth.
```

## Scope

Pack v1 supports exactly three capability families.

### 1. Project Understanding Recipe

Purpose:

- repo signals
- directory and file priority rules
- framework hints
- include and exclude rules for bounded context shaping

Pack v1 Project Understanding Recipe does **not** mean a custom CodeGraph engine.

It does **not** promise:

- symbol-level ranking
- semantic dependency analysis
- impact analysis
- test affinity
- language-engine replacement

### 2. Verification Recipe

Purpose:

- declare bounded build, test, lint, and typecheck recipes
- provide verification expectations for Runtime admission

Verification Recipe does **not** execute commands by itself.

It does **not** decide:

- whether a command is admitted
- whether a command is executed
- whether a command result counts as valid evidence

### 3. Review Rubric

Purpose:

- review checklist items
- risk hints
- evidence expectations
- domain-specific review questions

Review Rubric does **not** provide:

- approve
- reject
- reopen
- merge-readiness authority
- task status mutation

## Pack Structure

In Pack v1:

```text
Pack is the container.
Recipe or rubric is the declared capability item.
```

A single pack may contain one or more supported capability items, but only from the Pack v1 allowlist:

- `project_understanding_recipe`
- `verification_recipe`
- `review_rubric`

Anything outside that allowlist is out of scope for v1.

## Authority Model

Runtime remains the sole owner of:

- task lifecycle actions
- review lifecycle actions
- safety and boundary decisions
- truth writeback
- protected-root policy
- execution routing
- command admission
- pack admission
- pack assignment
- pack rollback and pin policy

Pack may propose, declare, or annotate.

Pack may not approve, mutate, or finalize.

## Protected Root Rule

Protected truth roots stay Runtime-owned.

Pack v1 must not directly mutate:

- `.ai/tasks/`
- `.ai/memory/`
- `.ai/artifacts/reviews/`
- `.carves-platform/`
- `.git/`

Pack v1 also does not gain direct authority over secret-like paths or runtime-owned governance files.

Protected-root denial is a Runtime rule, not a Pack self-certification right.

## Risk Model

Pack v1 uses a bounded risk ladder.

### L0: Metadata-only

Allowed examples:

- repo signals
- framework tags
- compatibility hints
- descriptive labels

### L1: Read-only projection

Allowed examples:

- context include and exclude hints
- file and directory priority rules
- report and rendering hints

No command execution.

### L2: Advisory generation

Allowed examples:

- review checklist items
- risk hints
- evidence expectations
- bounded task or refactor suggestions

No task truth mutation.

### L3: Verification recipe

Allowed examples:

- fixed verification command declarations

Pack v1 may declare L3 material, but L3 does **not** become active by default.

L3 only takes effect through Runtime command admission.

### L4: Code adapter

Deferred beyond v1.

### L5: Runtime authority

Forbidden to community Pack v1.

## Community Envelope

Community Pack v1 default envelope is:

- L0
- L1
- L2

L3 is conditional:

```text
declared by the Pack,
admitted by Runtime,
executed only through Runtime boundary,
and validated only through Runtime evidence and review flow.
```

L4 is deferred.

L5 is forbidden.

## Non-Goals

Pack v1 does **not** open:

- Adapter Host as a user-facing product
- marketplace
- registry
- rollout
- remote distribution trust
- automatic activation
- auto-update
- code adapters
- worker adapters
- tool adapters
- planner adapters
- scheduler strategy extension
- merge strategy extension
- safety override
- truth mutation by pack

Pack v1 is not a second control plane.

Pack v1 is not a second truth root.

## Runtime Alignment Rule

Pack v1 must align to existing Runtime-local pack policy and Runtime surfaces.

Pack UX may add thin aliases later, but:

```text
no alias may create a second truth root,
no alias may create a second lifecycle authority,
and no alias may bypass existing Runtime governance surfaces.
```

## Product Promise

User-facing plain language:

```text
Pack lets CARVES better recognize a project,
better know how to verify changes,
and better prepare review guidance.
```

But the hard boundary remains:

```text
Runtime still decides what runs,
what counts as valid evidence,
what review means,
and what may become truth.
```

## Phase 1 Exit Criteria

Phase 1 is complete when the following are frozen in writing:

- Pack v1 definition
- supported capability families
- Runtime-held authority boundary
- Pack-held non-authority boundary
- risk model
- v1 non-goals

Phase 1 completion does **not** imply:

- manifest schema completion
- command admission schema completion
- attribution schema completion
- conflict algorithm completion
- engineering implementation approval

## Next Required Phases

Phase 2 and later must define:

1. closed Pack artifact schema
2. Runtime surface mapping
3. command admission schema
4. task-level attribution schema
5. conflict merge semantics
6. implementation tests

Until those artifacts exist, this document is a frozen direction baseline, not a complete build contract.
