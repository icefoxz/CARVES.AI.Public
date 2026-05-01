# CARVES Real Project Workspace

This guide records the Phase 7 real external project workspace path.

Use it after Phase 6 has produced approved card/task truth and `plan status` reports an execution-bound line with a managed workspace next action.

## Target

The current pilot target is:

```text
D:\Projects\CARVES.AI\CARVES.AgentCoach
```

## Command Sequence

From the target repo, issue the task-bound workspace:

```powershell
carves --cold plan issue-workspace T-CARD-001-001
```

Verify the managed workspace surface:

```powershell
carves --cold inspect runtime-managed-workspace
carves --cold api runtime-managed-workspace
```

Expected readback:

- `Runtime document root mode: attach_handshake_runtime_root`
- `Overall posture: task_bound_workspace_active`
- `Mode D hardening state: active`
- `Path policy enforcement: active`
- `Validation valid: True`

The lease should name:

- `T-CARD-001-001`
- `PROJECT.md`
- `docs/agentcoach-first-slice.md`
- `host_routed_review_and_writeback_required`

## Workspace Rule

The agent may edit only the lease workspace, not the target repo official truth directly.

For the current pilot, the workspace is:

```text
D:\Projects\CARVES.AI\.carves-worktrees\CARVES.AgentCoach\T-CARD-001-001
```

The allowed writable paths are:

- `PROJECT.md`
- `docs/agentcoach-first-slice.md`

If the agent needs another path, stop and replan instead of editing outside the lease.

## Current Workspace Evidence

The current Phase 7 workspace evidence commit is:

```text
e81fbbf3ee31c0fdd8704b26db21e9c2459423f3
```

It contains only:

- `PROJECT.md`
- `docs/agentcoach-first-slice.md`

This commit is evidence in the detached task workspace. It is not official target repo truth until the review/writeback path accepts it.

## Official Truth Rule

The workspace is not official truth.

After file changes, return the result through the Runtime review/writeback path. Do not copy files manually into the target repo to bypass review.

The Phase 8 writeback guide is:

- `docs/guides/CARVES_REAL_PROJECT_WORKSPACE_WRITEBACK.md`

The next governed command is:

```powershell
carves --cold plan submit-workspace T-CARD-001-001 "submitted phase 8 managed workspace result"
```

## Non-Claims

This guide does not claim:

- workspace changes have already been reviewed
- workspace changes have already been written back
- `.ai/runtime/live-state/` should be committed
- the managed workspace is an OS sandbox
- Mode E brokered execution is complete
