# CARVES Pack v1 Engineering Acceptance

## Purpose

This document freezes the Phase 6 Pack v1 engineering acceptance gate.

Phase 1 through Phase 5 already froze:

- product definition
- pack artifact schema
- Runtime surface mapping
- task attribution contract
- verification command admission contract
- conflict resolution semantics

Phase 6 does **not** yet implement Pack v1.

Phase 6 defines the minimum engineering pass/fail contract that must be satisfied before Pack v1 implementation work may claim readiness.

## Core Rule

Hard rule:

```text
Pack v1 is not implementation-ready just because the product direction is frozen.
Pack v1 becomes implementation-ready only when its bounded engineering acceptance gate is explicit and testable.
```

Another hard rule:

```text
Engineering acceptance must validate Runtime-owned governance.
It may not create a second pack control plane or a second truth root.
```

## Scope

This phase defines:

- required engineering acceptance categories
- required rejection cases
- minimum task-attribution and audit expectations
- non-normative dogfood expectations

This phase does not define:

- new Runtime commands
- registry or marketplace distribution
- code adapters
- Worker or Tool adapters
- auto-update or rollout
- truth mutation by pack

## Normative Inputs

Phase 6 engineering acceptance depends on these Pack v1 artifacts:

- `docs/product/runtime-pack-v1-product-spec.md`
- `docs/contracts/runtime-pack-v1.schema.json`
- `docs/product/runtime-pack-v1-surface-mapping.md`
- `docs/contracts/runtime-pack-task-attribution.schema.json`
- `docs/product/runtime-pack-task-attribution-v1.md`
- `docs/contracts/runtime-pack-command-admission.schema.json`
- `docs/product/runtime-pack-command-admission-v1.md`
- `docs/product/runtime-pack-conflict-resolution-v1.md`

## Acceptance Categories

### A. Pack Artifact Validation

Implementation must prove that Pack v1 manifest validation is a closed allowlist boundary.

Minimum required checks:

- unknown top-level property is rejected
- unknown `capabilityKinds` value is rejected
- missing required identity field is rejected
- `requestedPermissions.network=true` is rejected
- `requestedPermissions.env=true` is rejected
- `requestedPermissions.secrets=true` is rejected
- `requestedPermissions.truthWrite=true` is rejected
- recipe arrays that contradict declared `capabilityKinds` are rejected
- mergeable items without stable ids are rejected

### B. Runtime Admission And Effective Policy

Implementation must prove that Pack manifests request permissions, while Runtime computes effective policy.

Minimum required checks:

- manifest acceptance does not self-certify artifact hash
- manifest acceptance does not self-certify protected-root denial
- Runtime admission record binds the admitted artifact to Runtime-owned provenance
- protected roots remain denied by Runtime effective policy
- admitted pack state maps to existing Runtime admission surfaces only

### C. Verification Command Admission

Implementation must prove that verification recipes remain declarative and Runtime-governed.

Minimum required checks:

- `known_tool_command` can enter Runtime admission
- `package_manager_script` is elevated-risk, not silently treated as low risk
- `repo_script` is blocked by default
- `shell_command` is rejected by default
- free-form shell wrapper is rejected
- pipe-to-shell posture is rejected
- dynamic command generation is rejected
- command posture writing protected roots is rejected
- admitted command decision records include effective permission posture and evidence expectations

### D. Task Attribution And History Safety

Implementation must prove that Pack usage is recorded as immutable historical truth for explainability and audit.

Minimum required checks:

- each attributed task run records `taskId`, `runId`, and `packSelectionId`
- each attributed pack records identity, version, channel, and capability usage
- each attributed contribution is referenced by stable id
- admitted command refs are snapshot into task attribution when used
- historical attribution is not recalculated from current selection
- rollback or pin changes do not rewrite historical attribution

### E. Conflict Resolution And Diagnostics

Implementation must prove that Pack merges are deterministic and mismatch-safe.

Minimum required checks:

- duplicate stable ids are handled deterministically
- `deny > allow` remains effective
- protected roots remain excluded after merge
- equal-precedence divergent rules surface mismatch diagnostics
- same verification command id with divergent kind/cwd/posture surfaces mismatch diagnostics
- review rubric dedupe uses stable ids
- effective merge result remains attributable to source packs

### F. Runtime Surface Alignment

Implementation must prove that Pack v1 remains aligned to existing Runtime-owned surfaces.

Minimum required checks:

- Pack validation maps to existing validation surfaces
- admission maps to existing Runtime admission surfaces
- assignment maps to existing Runtime selection surfaces
- pin and rollback map to existing Runtime switch-policy and selection surfaces
- explainability reads Runtime task attribution surfaces
- execution audit reads existing Runtime execution-audit surfaces
- mismatch reads existing Runtime mismatch-diagnostics surfaces

Hard rule:

```text
No Pack v1 implementation may introduce a second lifecycle ledger, attribution root, or review authority.
```

## Required Rejection Cases

The following cases must fail closed:

1. manifest with unknown top-level property
2. manifest with unknown capability kind
3. manifest requesting non-v1 capability semantics through hidden fields
4. manifest attempting truth-write posture
5. manifest attempting protected-root writes
6. verification command using shell wrapper
7. verification command using pipe-to-shell
8. verification command generated dynamically by Pack content
9. lower-precedence include rule attempting to re-include protected roots
10. task explainability that re-infers pack history from current selection instead of reading attribution

## Dogfood Rule

Dogfood is encouraged during implementation, but dogfood artifacts are not themselves the Pack v1 contract.

Dogfood may start before all acceptance categories are fully automated.

However, Pack v1 may not claim engineering closure unless dogfood covers all three capability families at least once:

- `project_understanding_recipe`
- `verification_recipe`
- `review_rubric`

This may be demonstrated through one combined first-party reference pack or through multiple smaller packs.

Recommended but non-normative dogfood coverage:

- one project-understanding-heavy pack
- one verification-heavy pack using bounded `known_tool_command`
- one review-rubric-heavy pack

## Engineering Acceptance Evidence

Phase 6 engineering acceptance must produce evidence that can be audited later.

At minimum, implementation acceptance must be able to point to:

- manifest validation results
- Runtime admission results
- command admission decision records
- task attribution records
- mismatch diagnostic output when conflicts occur
- pack explainability output for at least one attributed task

## Exit Criteria

Phase 6 is complete when:

- engineering pass/fail categories are frozen in writing
- required rejection cases are explicit
- Runtime surface alignment remains explicit
- no second truth root is introduced
- dogfood is positioned as supporting evidence, not as the normative contract

Phase 6 completion does **not** yet mean:

- Pack v1 implementation already exists
- public pack ecosystem is ready
- code adapters are opened
- remote registry or rollout is authorized
- Pack can mutate Runtime truth
