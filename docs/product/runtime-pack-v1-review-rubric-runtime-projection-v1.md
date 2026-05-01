# Pack v1 Review Rubric Runtime Projection v1

## Scope

This phase closes bounded Runtime projection for `review_rubric` within Pack v1.

The work in this phase is limited to:

- resolving the currently selected declarative Pack v1 manifest through existing Runtime-owned selection truth
- projecting declared review rubrics into existing task inspection and task-scoped pack explainability surfaces
- keeping review verdict, review gate mutation, and merge authority Runtime-owned

This phase does **not**:

- open Adapter Host
- open Marketplace or registry distribution
- grant Pack-owned truth mutation
- grant Review verdict or Merge authority
- close the full Runtime integration line

## Runtime projection result

Selected declarative Pack v1 manifests can now contribute bounded review-preparation material through existing Runtime-owned surfaces.

The projection path is:

1. a declarative manifest is admitted through the Pack v1 manifest bridge
2. a selected declarative pack is resolved from existing Runtime pack selection truth
3. `review_rubric` entries are projected by `RuntimePackReviewRubricProjectionService`
4. `task inspect` exposes `runtime_pack_review_rubric`
5. `pack explain --task <task-id>` exposes the current bounded rubric summary and checklist items

## Boundary posture

- review rubric projection is read-only and derived from the current selected declarative manifest
- review rubric projection does not approve, reject, reopen, or merge anything
- review gate authority remains Runtime-owned
- no second control plane or second truth root is created

## Acceptance summary

This phase is accepted when all of the following are true:

- a selected declarative `review_rubric` pack projects rubric metadata through Runtime-owned inspect surfaces
- task-scoped pack explainability can describe the current selected review rubric projection without recomputing historical run attribution
- checklist items remain bounded to declarative rubric content and do not mutate review verdict authority

## Closure wording

The accurate closure wording for this phase is:

```text
Pack v1 review_rubric runtime projection: completed
```

The inaccurate closure wording is:

```text
Pack v1 full Runtime integration: completed
```
