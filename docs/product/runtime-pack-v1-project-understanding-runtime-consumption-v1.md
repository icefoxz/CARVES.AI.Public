# Pack v1 Project Understanding Runtime Consumption v1

## Purpose

This phase records the bounded Runtime integration for `project_understanding_recipe`.

The goal is narrow:

- consume the currently selected declarative Pack v1 manifest through existing Runtime-owned selection truth
- apply project-understanding include, exclude, priority, repo-signal, and framework-hint rules to existing context shaping
- keep CodeGraph, impact analysis, semantic dependency analysis, verification execution, and review gate authority Runtime-owned

## Runtime-owned entry

The Runtime reads the current selected pack from existing selection truth:

- `inspect runtime-pack-selection`

If the current selection is a Pack v1 manifest bridge artifact with:

- `assignment_mode = overlay_assignment`
- `assignment_ref = <runtime-pack-v1-manifest-path>`

Runtime may resolve that manifest and consume only the declared `project_understanding_recipe` items.

## Bounded effect

This phase changes only existing context shaping paths.

The selected declarative pack may now influence:

- bounded candidate file selection for `ContextPackService`
- include and exclude filtering for pack-added candidate files
- priority ordering for pack-added candidate files
- framework-hint and repo-signal prompt hints
- context-pack expandable references back to the selected declarative manifest

This phase does **not**:

- replace CodeGraph
- perform symbol-level ranking
- add impact analysis
- add test affinity analysis
- execute verification commands
- modify review verdicts
- create a second Runtime truth root

## Current implementation scope

Runtime work completed in this phase is limited to:

- selected declarative manifest resolution through existing selection truth
- `project_understanding_recipe` consumption inside `ContextPackService`
- operator-visible context-pack projection of the consumed shaping hints

## Validation evidence

Focused evidence for this phase is expected through:

- `tests/Carves.Runtime.Application.Tests/ContextPackServiceTests.cs`
- `tests/Carves.Runtime.IntegrationTests/RuntimePackHostContractTests.cs`

## Closure statement

Accurate closure for this phase is:

```text
Pack v1 project_understanding_recipe runtime consumption: completed
```

This does **not** mean:

```text
Pack v1 full Runtime integration: completed
```

The following remain outside this phase:

- `verification_recipe` execution admission
- `review_rubric` runtime projection into review preparation
- unified declarative-pack attribution and mismatch closure
