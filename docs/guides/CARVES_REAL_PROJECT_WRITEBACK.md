# CARVES Real Project Writeback

This guide records the Phase 6 real external project writeback path.

Use it after a real target repo has completed the Phase 5 pilot flow and `plan status` reports `active_planning_card_fill_state=ready_to_export`.

## Target

The current pilot target is:

```text
<target-project>
```

## Command Sequence

From the target repo, export the active planning card:

```powershell
carves --cold plan export-card drafts\phase6-plan-card.json
```

Create and approve the official card:

```powershell
carves --cold create-card-draft drafts\phase6-plan-card.json
carves --cold approve-card CARD-001 "approved for phase 6 real project pilot card writeback"
```

Create and approve the first taskgraph draft:

```powershell
carves --cold create-taskgraph-draft drafts\phase6-taskgraph-draft.json
carves --cold approve-taskgraph-draft TG-CARD-001-20260411-112753 "approved for phase 6 real project pilot task truth"
```

Verify the line:

```powershell
carves --cold plan status
carves --cold inspect task T-CARD-001-001
```

## Expected Readback

Expected card readback:

- `CARD-001`
- lifecycle state: `approved`
- planning context contains the active planning card id and plan handle

Expected task readback:

- `T-CARD-001-001`
- status: `Pending`
- dispatch state: `dispatchable`
- scope:
  - `PROJECT.md`
  - `docs/agentcoach-first-slice.md`
- acceptance contract gate:
  - status: `projected`
  - contract id: `AC-T-CARD-001-001`
  - lifecycle status: `Compiled`

Expected plan status:

- `formal_planning_state=execution_bound`
- `packet_briefing.next_action_posture=execution_follow_through`
- `packet_briefing.acceptance_binding_state=task_truth_bound`
- `managed_workspace_next_action` points to `plan issue-workspace T-CARD-001-001`

## Wrapper Fallback

If the source-tree wrapper's staged temp assembly is blocked by Windows Application Control, `carves.ps1` should fall back to source project execution and continue the same command.

The fallback message is:

```text
CARVES CLI staged temp assembly was blocked by Windows Application Control; falling back to source project execution.
```

This is a source-tree dogfood fallback, not a substitute for signed release packaging.

## Next Governed Action

Do not run the task directly from the open repo after Phase 6.

The next governed action is:

```powershell
carves --cold plan issue-workspace T-CARD-001-001
```

That moves the line into task-bound workspace execution before any agent writes `PROJECT.md` or `docs/agentcoach-first-slice.md`.

The Phase 7 workspace guide is:

- `docs/guides/CARVES_REAL_PROJECT_WORKSPACE.md`

The Phase 8 workspace writeback guide is:

- `docs/guides/CARVES_REAL_PROJECT_WORKSPACE_WRITEBACK.md`

## Non-Claims

This guide does not claim:

- the first AgentCoach task has been implemented
- a managed workspace lease has already been issued by Phase 6 itself
- Mode E brokered execution has completed
- global distribution is finished
- arbitrary agents are hard-sandboxed
