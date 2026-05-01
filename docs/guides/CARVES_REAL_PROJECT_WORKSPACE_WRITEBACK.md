# CARVES Real Project Workspace Writeback

This guide records the Phase 8 real external project managed workspace writeback path.

Use it after Phase 7 has issued a task-bound workspace and the worker has changed only the paths declared by the active lease.

## Target

The current pilot target is:

```text
D:\Projects\CARVES.AI\CARVES.AgentCoach
```

## Precondition

The active task is:

```text
T-CARD-001-001
```

The managed workspace is:

```text
D:\Projects\CARVES.AI\.carves-worktrees\CARVES.AgentCoach\T-CARD-001-001
```

The allowed writable paths are:

- `PROJECT.md`
- `docs/agentcoach-first-slice.md`

The workspace evidence commit is:

```text
e81fbbf3ee31c0fdd8704b26db21e9c2459423f3
```

## Command Sequence

From the target repo, submit the workspace output into Runtime review:

```powershell
carves --cold plan submit-workspace T-CARD-001-001 "submitted phase 8 managed workspace result"
```

Inspect mutation policy before approval:

```powershell
carves --cold inspect runtime-workspace-mutation-audit T-CARD-001-001
```

Approve the review and let Runtime materialize official target files:

```powershell
carves --cold review approve T-CARD-001-001 "approved phase 8 managed workspace writeback"
```

Verify that the accepted workspace lease is no longer active:

```powershell
carves --cold inspect runtime-managed-workspace
```

## Expected Readback

Expected submit readback:

- `Submitted managed workspace result for T-CARD-001-001; task is now REVIEW.`
- `PROJECT.md`
- `docs/agentcoach-first-slice.md`

Expected mutation-audit readback:

- `Changed paths: 2`
- `Can proceed to writeback: True`

Expected approve readback:

- `Approved review`
- `Materialized 2 approved file(s)`
- `Released 1 managed workspace lease(s)`

Expected managed-workspace readback after approval:

- `Overall posture: planning_lineage_closed_no_active_workspace`
- `Mode D hardening state: closed_no_active_workspace`
- `Active leases: 0`

Expected target repo files after approval:

- `PROJECT.md`
- `docs/agentcoach-first-slice.md`
- `.ai/tasks/nodes/T-CARD-001-001.json`
- `.ai/artifacts/reviews/T-CARD-001-001.json`

## Commit Hygiene

After approval, commit only official target truth and approved files.

Do not commit target-local runtime live state unless explicitly intended:

- `.ai/runtime/live-state/`

The review/writeback path may remove the disposable managed workspace after approval. That is expected because the official files have been materialized into the target repo.

## Non-Claims

This guide does not claim:

- the target repo commit has been pushed
- workspace mutation is an OS sandbox
- `.ai/runtime/live-state/` belongs in product truth
- Mode E brokered execution is required for this path
- ACP/MCP transports are complete
