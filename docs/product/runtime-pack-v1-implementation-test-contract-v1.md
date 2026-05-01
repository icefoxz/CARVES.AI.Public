# CARVES Pack v1 Implementation Test Contract

## Purpose

This document freezes the Phase 8 Pack v1 implementation test contract.

Phase 6 froze the engineering acceptance gate.

Phase 7 froze first-party reference packs that can act as bounded positive examples.

Phase 8 converts those acceptance requirements into a stable implementation test set.

This document is normative for:

- implementation acceptance test coverage
- stable test ids
- pass or fail expectations
- positive and negative control posture

It is not Runtime implementation by itself.

## Core Rule

Hard rule:

```text
Pack v1 may not claim implementation closure without passing the required implementation test set.
```

Another hard rule:

```text
Implementation tests must validate Runtime-governed behavior.
They may not introduce a second pack truth root or a second pack control plane.
```

## Scope

This phase defines the Pack v1 test contract for:

- manifest validation
- Runtime admission boundary
- command admission boundary
- task attribution persistence
- conflict resolution determinism
- Runtime surface alignment
- reference-pack validation coverage

The required Pack v1 capability coverage remains:

- `project_understanding_recipe`
- `verification_recipe`
- `review_rubric`

This phase does not define:

- code adapter tests
- marketplace or registry tests
- rollout tests
- remote distribution trust tests
- Worker or Tool adapter tests
- truth mutation tests for pack-owned authority

## Normative Inputs

Phase 8 test design depends on:

- `docs/product/runtime-pack-v1-product-spec.md`
- `docs/contracts/runtime-pack-v1.schema.json`
- `docs/product/runtime-pack-v1-surface-mapping.md`
- `docs/contracts/runtime-pack-task-attribution.schema.json`
- `docs/product/runtime-pack-task-attribution-v1.md`
- `docs/contracts/runtime-pack-command-admission.schema.json`
- `docs/product/runtime-pack-command-admission-v1.md`
- `docs/product/runtime-pack-conflict-resolution-v1.md`
- `docs/product/runtime-pack-v1-engineering-acceptance-v1.md`
- `docs/product/runtime-pack-v1-reference-packs.md`

## Test Set Structure

Each Pack v1 implementation test must have:

- a stable test id
- one scope statement
- one expected result
- one evidence requirement

Positive and negative controls are both required.

## Required Test Set

### A. Manifest Validation

#### `packv1.manifest.reject_unknown_top_level`

Scope:

- a manifest with an unknown top-level property

Expected result:

- validation fails closed

Evidence:

- validator output identifies unknown property rejection

#### `packv1.manifest.reject_unknown_capability_kind`

Scope:

- a manifest with a capability kind outside the Pack v1 allowlist

Expected result:

- validation fails closed

Evidence:

- validator output identifies unknown capability rejection

#### `packv1.manifest.reject_truth_write_request`

Scope:

- a manifest requesting `truthWrite=true`

Expected result:

- validation fails closed

Evidence:

- validator output identifies `requestedPermissions.truthWrite` violation

#### `packv1.manifest.reject_network_env_or_secrets_request`

Scope:

- a manifest requesting `network=true`, `env=true`, or `secrets=true`

Expected result:

- validation fails closed

Evidence:

- validator output identifies the denied permission posture

#### `packv1.manifest.reject_missing_stable_id`

Scope:

- a manifest whose mergeable item omits its required stable id

Expected result:

- validation fails closed

Evidence:

- validator output identifies the missing required stable id field

### B. Runtime Admission Boundary

#### `packv1.admission.manifest_does_not_self_certify_hash`

Scope:

- an admitted pack artifact

Expected result:

- artifact hash is recorded by Runtime admission or provenance truth, not self-certified by the manifest

Evidence:

- admission or provenance record shows the hash
- manifest remains free of self-certifying hash authority

#### `packv1.admission.protected_roots_remain_runtime_denied`

Scope:

- a pack artifact that otherwise validates

Expected result:

- Runtime effective policy still denies protected-root mutation

Evidence:

- admission or effective-permission output shows protected-root denial

### C. Verification Command Admission

#### `packv1.command.known_tool_candidate`

Scope:

- a `known_tool_command`

Expected result:

- the command is eligible for Runtime admission

Evidence:

- command admission decision record exists with non-rejected verdict

#### `packv1.command.package_manager_script_elevated_risk`

Scope:

- a `package_manager_script`

Expected result:

- the command is not treated as low-risk by default

Evidence:

- decision record marks elevated-risk posture

#### `packv1.command.repo_script_blocked_default`

Scope:

- a `repo_script`

Expected result:

- the command is blocked by default

Evidence:

- decision record shows blocked verdict under default posture

#### `packv1.command.shell_command_rejected`

Scope:

- a `shell_command`

Expected result:

- the command is rejected by default

Evidence:

- decision record shows rejected verdict

#### `packv1.command.reject_pipe_to_shell_or_dynamic_generation`

Scope:

- a shell-wrapper, pipe-to-shell, or dynamically generated command body

Expected result:

- admission fails closed

Evidence:

- decision record or validator output explains the hard reject

### D. Task Attribution Persistence

#### `packv1.attribution.snapshot_records_identity_and_contributions`

Scope:

- one task run using one or more admitted pack capabilities

Expected result:

- attribution records task identity, run identity, pack selection, pack identity, and contribution refs

Evidence:

- task attribution record conforms to `runtime-pack-task-attribution.schema.json`

#### `packv1.attribution.rollback_does_not_rewrite_history`

Scope:

- a task executed before a later pin, unpin, disable posture, or rollback

Expected result:

- historical attribution remains unchanged

Evidence:

- before and after task explainability reads the same historical attribution snapshot

#### `packv1.attribution.explain_reads_snapshot_not_current_selection`

Scope:

- a task whose current pack selection differs from the selection used during the run

Expected result:

- explainability reads the historical attribution snapshot, not the current selection alone

Evidence:

- explainability output remains stable after selection change

### E. Conflict Resolution

#### `packv1.conflict.deterministic_same_id_resolution`

Scope:

- two contributions under the same stable id

Expected result:

- merge result is deterministic under precedence order

Evidence:

- same inputs produce same effective output and same mismatch posture

#### `packv1.conflict.deny_beats_allow`

Scope:

- one contribution narrows or denies what another allows

Expected result:

- effective result preserves deny-first posture

Evidence:

- effective merged posture shows deny-first behavior

#### `packv1.conflict.protected_root_reinclude_rejected`

Scope:

- a lower-precedence pack tries to re-include a protected root

Expected result:

- protected root remains excluded

Evidence:

- merged output excludes the protected root
- mismatch diagnostics records the attempted re-inclusion

#### `packv1.conflict.review_rubric_dedupe_by_stable_id`

Scope:

- two rubric items with the same stable id

Expected result:

- dedupe is deterministic and attributable

Evidence:

- merged rubric output and mismatch diagnostics explain the result

### F. Runtime Surface Alignment

#### `packv1.surface.alias_does_not_create_second_truth_root`

Scope:

- any Pack-oriented UX alias added on top of Runtime pack surfaces

Expected result:

- the alias delegates to existing Runtime surfaces only

Evidence:

- no second admission ledger, selection ledger, attribution root, or review authority is written

#### `packv1.surface.mismatch_visible_through_existing_surface`

Scope:

- a conflicting or divergent pack situation

Expected result:

- divergence is visible through existing mismatch diagnostics surface

Evidence:

- `runtime-pack-mismatch-diagnostics` or equivalent existing Runtime-owned output explains the issue

### G. Reference-Pack Positive Controls

#### `packv1.reference.dotnet_webapi_valid`

Scope:

- `docs/product/reference-packs/runtime-pack-v1-dotnet-webapi.json`

Expected result:

- schema validation succeeds

Evidence:

- validator passes against `docs/contracts/runtime-pack-v1.schema.json`

#### `packv1.reference.node_typescript_valid`

Scope:

- `docs/product/reference-packs/runtime-pack-v1-node-typescript.json`

Expected result:

- schema validation succeeds

Evidence:

- validator passes against `docs/contracts/runtime-pack-v1.schema.json`

#### `packv1.reference.security_review_valid`

Scope:

- `docs/product/reference-packs/runtime-pack-v1-security-review.json`

Expected result:

- schema validation succeeds

Evidence:

- validator passes against `docs/contracts/runtime-pack-v1.schema.json`

## Minimum Pass Set

Phase 8 passes only when:

- all manifest-validation rejection tests pass
- all command-taxonomy boundary tests pass
- task attribution history tests pass
- deterministic conflict tests pass
- Runtime surface alignment tests pass
- all first-party reference packs validate successfully

## Failure Interpretation

Any failing required test means:

```text
Pack v1 implementation is not ready to claim closure.
```

This includes cases where:

- a validator accepts unknown fields
- Runtime allows protected-root posture to drift
- a shell command is treated as admissible
- explainability re-infers history from current selection
- an alias writes a second truth root

## Exit Criteria

Phase 8 is complete when:

- the required implementation test set is frozen in writing
- each test has a stable id
- pass or fail expectations are explicit
- positive controls and rejection controls are both covered
- the contract remains aligned to existing Runtime pack surfaces

Phase 8 completion does **not** yet mean:

- the implementation tests already exist in source
- Pack v1 Runtime implementation is closed
- Pack v1 dogfood has passed under full Runtime execution
