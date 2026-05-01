# CARVES Pack v1 Conflict Resolution

## Purpose

This document freezes the Phase 5 Pack v1 rule for deterministic merge behavior.

Pack v1 allows multiple admitted packs to contribute bounded capability items.

That requires a merge model that is:

- deterministic
- attributable
- replay-safe
- compatible with existing Runtime mismatch and explainability surfaces

This document is normative for Pack v1 conflict resolution.

It is not a second control plane.

It is not a second truth root.

## Core Rule

Hard rule:

```text
Pack contributions may be merged.
Runtime policy decides the effective result.
```

Another hard rule:

```text
Conflict resolution must be deterministic, attributable, and audit-safe.
```

Pack conflict resolution must feed existing Runtime-owned surfaces:

- `inspect runtime-pack-selection`
- `inspect runtime-pack-task-explainability <task-id>`
- `inspect runtime-pack-execution-audit`
- `inspect runtime-pack-mismatch-diagnostics`

## Scope

This phase covers only the three Pack v1 capability families:

- `project_understanding_recipe`
- `verification_recipe`
- `review_rubric`

It does not define:

- code adapter merge behavior
- worker or tool adapter merge behavior
- scheduler or merge strategy policy
- truth mutation authority

## Stable Id Requirement

Every mergeable Pack v1 contribution item must have a stable id.

This includes:

- include and exclude rules
- context priority rules
- repo signals
- framework hints
- verification recipes
- verification commands
- review rubrics
- review checklist items
- risk hints
- evidence expectations

Without stable ids, Runtime cannot safely provide:

- deterministic dedupe
- deterministic override
- source attribution
- mismatch diagnostics
- rollback-safe explainability

## Precedence Model

Pack v1 merge uses this precedence order:

1. Core Runtime policy
2. repo policy
3. first-party pack
4. community pack

Tie-break rule:

```text
higher precedence source wins.
```

Another hard rule:

```text
deny beats allow.
```

If two sources are otherwise equal and produce a non-identical contribution under the same stable id, Runtime must record mismatch diagnostics instead of silently picking an arbitrary winner.

## Protected Root Rule

Protected roots are not part of Pack self-governance.

Protected roots are always denied by Runtime effective policy.

Hard rule:

```text
protected roots are subtracted after all pack merges.
no pack may re-include them.
```

This applies regardless of:

- source precedence
- capability family
- include rule order
- recipe order

## Merge Semantics By Capability Family

### 1. Project Understanding Recipe

Project understanding contributions include:

- repo signals
- include rules
- exclude rules
- context priority rules
- framework hints

#### Include / exclude rules

Merge rule:

```text
include rules: union
exclude rules: union
effective includes: includes minus excludes minus protected roots
```

If a lower-precedence pack tries to re-include something already excluded by higher-precedence policy, the exclusion stands.

If any pack attempts to re-include a protected root, Runtime records mismatch diagnostics and keeps the protected root excluded.

#### Repo signals

Merge rule:

```text
repo signals are additive.
```

Signals with the same stable id are deduped by stable id.

If the same stable id carries materially different content from different sources, the higher-precedence source becomes effective and the divergence may be surfaced through mismatch diagnostics.

#### Framework hints

Merge rule:

```text
framework hints are additive with source attribution.
```

If two hints share the same stable id and differ materially, higher precedence wins and divergence is eligible for mismatch diagnostics.

#### Context priority rules

Merge rule:

```text
priority rules merge by stable id.
higher precedence source wins.
equal precedence + divergent body => mismatch_detected.
```

Priority ties from different stable ids remain ordered by Runtime policy, not by pack-local free-form ordering.

### 2. Verification Recipe

Verification contributions include:

- recipe definitions
- command definitions
- required or optional posture
- command-level permission posture

#### Same command id

Merge rule:

```text
same command id => highest-precedence source wins.
```

If the same command id appears with materially different command body, cwd, env posture, secrets posture, write posture, or command kind, Runtime must emit mismatch diagnostics.

#### Same command body with different ids

Merge rule:

```text
dedupe by normalized executable + args + cwd when semantics are identical.
```

Attribution must preserve all contributing sources even when execution dedupes to one effective command.

#### Required vs optional

Merge rule:

```text
required beats optional.
```

Lower-precedence sources cannot weaken a required command into optional.

#### Permission posture

Merge rule:

```text
stricter posture wins.
```

Examples:

- `network=false` beats `network=true`
- `env=none` beats broader env allowance
- narrower write scope beats broader write scope
- protected-root deny is absolute

If two definitions for the same stable id disagree on command kind, cwd, or permission posture in a way that is not a strict narrowing, Runtime must record mismatch diagnostics.

### 3. Review Rubric

Review rubric contributions include:

- checklist items
- risk hints
- evidence expectations
- review questions

#### Checklist items

Merge rule:

```text
append and dedupe by stable id.
```

If the same checklist id appears with materially different text or severity, higher precedence becomes effective and divergence is eligible for mismatch diagnostics.

#### Risk hints

Merge rule:

```text
append with source attribution.
dedupe by stable id when equivalent.
```

#### Evidence expectations

Merge rule:

```text
union with deny-first Runtime policy.
```

Lower-precedence packs cannot relax a higher-precedence evidence expectation.

#### Forbidden outputs

Review rubric merge may not create:

- review verdicts
- gate mutations
- task status transitions
- merge authority

Those are outside Pack v1 scope and remain Runtime-owned.

## Mismatch Diagnostics Triggers

Conflict resolution must surface deterministic mismatch diagnostics when any of the following occurs:

- same stable id with divergent non-equivalent body
- same verification command id with different command kind
- same verification command id with conflicting cwd
- same verification command id with conflicting env posture
- same verification command id with conflicting secrets posture
- same verification command id with conflicting write posture that is not a strict narrowing
- attempt to re-include a protected root
- equal-precedence conflicting priority rule under the same stable id
- rubric or checklist id reused with materially different content

Mismatch diagnostics explain divergence.

Mismatch diagnostics do not replace admission truth or selection truth.

## Attribution Rule

The effective merge result must remain attributable.

Hard rule:

```text
dedupe does not erase source attribution.
```

Task-level attribution must be able to explain:

- which pack contributed the winning effective item
- which lower-precedence item was superseded
- which command or rubric ids were actually used
- whether mismatch diagnostics were present for the run

## Relation To Existing Runtime Surfaces

Phase 5 does not introduce a new Pack conflict ledger.

The effective result must map back to existing Runtime-local surfaces:

- selection remains visible through `inspect runtime-pack-selection`
- explainability remains visible through `inspect runtime-pack-task-explainability <task-id>`
- audit remains visible through `inspect runtime-pack-execution-audit`
- divergence remains visible through `inspect runtime-pack-mismatch-diagnostics`

Conflict resolution semantics must support those surfaces.

They may not bypass them.

## Non-Goals

This phase does not define:

- registry conflict resolution
- remote channel rollout
- code adapter override rules
- worker backend arbitration
- truth writeback rights

## Phase 5 Exit Criteria

Phase 5 is complete when:

- merge semantics are defined per Pack v1 capability family
- stable ids are mandatory for all mergeable items
- precedence order and `deny > allow` are explicit
- protected-root subtraction is explicit
- mismatch trigger conditions are explicit
- conflict resolution is positioned as input to existing Runtime surfaces, not a second truth root
