# CARVES Pack v1 Dogfood Validation

## Purpose

This document records the bounded Phase 11 dogfood validation for Pack v1.

Phase 7 froze the first-party reference packs.

Phase 9 added the Runtime-owned Pack v1 manifest validator.

Phase 10 added thin `pack` UX aliases over existing Runtime-owned pack surfaces.

Phase 11 proves that the current Pack v1 product surface is usable for bounded dogfood without expanding authority.

## Core Rule

Hard rule:

```text
Phase 11 is dogfood validation, not implementation closure.
```

Another hard rule:

```text
Dogfood must use Runtime-owned validation and inspect surfaces.
It may not create a second pack control plane or a second truth root.
```

## Scope

This phase validates the bounded product-facing path:

- `pack validate <json-path>`
- existing Runtime-owned inspect surfaces reached through Pack UX aliases

This phase covers the three first-party reference packs:

1. `docs/product/reference-packs/runtime-pack-v1-dotnet-webapi.json`
2. `docs/product/reference-packs/runtime-pack-v1-node-typescript.json`
3. `docs/product/reference-packs/runtime-pack-v1-security-review.json`

Together they cover:

- `project_understanding_recipe`
- `verification_recipe`
- `review_rubric`

## Dogfood Evidence

Phase 11 requires evidence that:

- all three first-party reference packs validate successfully through the Pack UX alias
- validation still resolves to the Runtime-owned `runtime_pack_v1` validator
- Pack UX stays routed through existing Runtime pack surfaces

Implementation evidence is carried by focused integration tests, not by a new Pack ledger.

Primary source:

- `tests/Carves.Runtime.IntegrationTests/RuntimePackHostContractTests.cs`

## Non-Claims

Phase 11 does **not** claim:

- Pack v1 manifests can be admitted directly as Runtime pack truth
- Pack v1 manifests can be assigned directly as Runtime selection truth
- verification recipes are executed from the Pack manifest itself
- task attribution is written from Pack-manifest execution
- conflict resolution is fully closed
- Pack v1 implementation is complete

## Exit Criteria

Phase 11 is complete when:

- bounded dogfood covers all three Pack v1 capability families at least once
- all first-party reference packs validate through `pack validate`
- no new authority surface is introduced
- no second truth root is introduced

Phase 11 completion does **not** yet mean:

- Pack v1 engineering closure
- Pack v1 full Runtime execution closure
- command-admission execution from Pack manifests
- manifest-to-admission conversion exists
