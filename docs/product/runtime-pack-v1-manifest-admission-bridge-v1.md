# CARVES Pack v1 Manifest Admission Bridge v1

## Purpose

This document records the bounded Runtime integration result for:

```text
Phase 1: declarative manifest -> Runtime admission bridge
```

The goal of this phase is narrow:

- accept a validated Pack v1 declarative manifest,
- compile it into a bounded runtime pack artifact plus runtime pack attribution pair,
- and delegate admission to the existing Runtime-owned `runtime admit-pack` truth.

This phase does **not** open:

- pack-owned admission truth,
- automatic assignment,
- verification execution closure,
- review gate mutation,
- Adapter Host authority,
- marketplace or registry rollout,
- or pack-owned truth mutation.

## Commands

Runtime-owned bridge surface:

```text
runtime admit-pack-v1 <runtime-pack-v1-manifest-path> [--channel <channel>] [--published-by <principal>] [--source-line <line>]
```

Product-facing alias:

```text
pack admit <runtime-pack-v1-manifest-path> [--channel <channel>] [--published-by <principal>] [--source-line <line>]
```

The alias remains thin.

It does not create a second control plane.

## Bridge Behavior

The bridge performs these steps:

1. validate the declarative manifest with `validate runtime-pack-v1`
2. compile a bounded pack artifact plus runtime pack attribution pair under `.ai/artifacts/packs/`
3. validate the generated pair against existing Runtime contracts
4. delegate to existing `runtime admit-pack` truth
5. expose the result through `inspect runtime-pack-admission`

The generated pair is support material for Runtime admission.

It is not a new truth root.

## Bounded Defaults

Because Pack v1 does not own execution authority, the bridge uses bounded Runtime-local defaults for the generated pair:

- `packType = runtime_pack`
- `channel = stable` unless explicitly overridden
- `policyPreset = core-default`
- `gatePreset = strict`
- `validatorProfile = default-validator`
- `environmentProfile = workspace`
- `routingProfile = connected-lanes`
- `assignmentMode = overlay_assignment`

The generated provenance and digest are Runtime-generated bridge evidence, not self-certified manifest truth.

## What This Phase Completes

This phase completes:

- declarative manifest to Runtime-local admission conversion
- Pack-facing admission over existing Runtime pack truth
- bridge-generated artifact and attribution persistence as support artifacts

## What This Phase Still Does Not Complete

This phase does **not** complete:

- assignment or selection from Pack v1 manifests
- verification recipe execution closure
- review rubric gate integration
- task-level attribution over live declarative-pack consumption
- Adapter Host opening
- marketplace / registry / rollout

## Exit Posture

The truthful closure statement for this phase is:

```text
Pack v1 declarative manifest -> Runtime admission bridge: completed
```

This phase must **not** be read as:

```text
Pack v1 full Runtime integration: completed
```
