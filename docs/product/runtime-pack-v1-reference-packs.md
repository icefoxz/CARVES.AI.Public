# CARVES Pack v1 Reference Packs

## Purpose

This document freezes the Phase 7 Pack v1 first-party reference-pack set.

Phase 7 exists to prove that the Pack v1 contracts from Phase 1 through Phase 6 are concrete enough to author bounded declarative pack artifacts.

These artifacts are:

- dogfood inputs
- schema examples
- acceptance-supporting references

They are not:

- Runtime admission truth
- Runtime selection truth
- Runtime execution truth
- a second control plane

## Core Rule

Hard rule:

```text
Reference packs are example Pack v1 artifacts.
They do not become active just because they exist in the repo.
```

Another hard rule:

```text
Reference packs support dogfood and implementation acceptance.
They do not replace Runtime admission, assignment, command admission, attribution, audit, or mismatch diagnostics.
```

## Reference-Pack Set

Phase 7 freezes the following first-party reference packs:

1. `docs/product/reference-packs/runtime-pack-v1-dotnet-webapi.json`
2. `docs/product/reference-packs/runtime-pack-v1-node-typescript.json`
3. `docs/product/reference-packs/runtime-pack-v1-security-review.json`

## Coverage

Together, the Phase 7 reference packs cover all three Pack v1 capability families:

- `project_understanding_recipe`
- `verification_recipe`
- `review_rubric`

### Dotnet Web API

Purpose:

- prove a bounded project-understanding recipe
- prove a bounded verification recipe using `known_tool_command`

This pack is not a custom CodeGraph engine.

It does not request:

- network
- env
- secrets
- truth write

### Node TypeScript

Purpose:

- prove a second bounded project-understanding recipe
- prove a second bounded verification recipe
- keep `package_manager_script` out of the first dogfood set

This pack is intentionally conservative.

It demonstrates the schema shape without opening elevated-risk command posture.

### Security Review

Purpose:

- prove a bounded `review_rubric`
- supply stable review checklist ids
- show that review guidance can be authored without claiming verdict authority

This pack does not provide:

- review approval
- review rejection
- merge readiness
- task status mutation

## Why Three Packs

Phase 7 uses three smaller reference packs instead of one combined pack for two reasons:

1. it keeps capability boundaries visible
2. it gives later conflict and selection testing more than one source artifact

This is still a bounded first-party set.

It is not a public ecosystem claim.

## Runtime Boundary

Reference packs remain subject to all earlier Pack v1 rules:

- manifest validation through `docs/contracts/runtime-pack-v1.schema.json`
- Runtime-owned effective policy
- Runtime-owned command admission
- Runtime-owned task attribution
- Runtime-owned mismatch diagnostics

Reference packs cannot self-authorize:

- protected-root writes
- truth mutation
- shell execution
- repo-script execution by default

## Dogfood Position

These artifacts are non-normative dogfood support.

They exist so that implementation work can validate:

- schema acceptance
- capability coverage
- stable id requirements
- command taxonomy usage
- review rubric shape

They do not, by themselves, prove:

- Runtime admission implementation
- Runtime selection implementation
- command admission execution
- task attribution persistence
- conflict resolution execution

## Phase 7 Exit Criteria

Phase 7 is complete when:

- at least one first-party reference pack exists for each Pack v1 capability family
- the artifacts are written as bounded declarative manifests
- the artifacts remain non-authoritative until Runtime admission
- the repo contains a clear human-readable map of what each reference pack is for
