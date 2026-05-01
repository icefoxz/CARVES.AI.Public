# CARVES Agent Problem Follow-Up Planning Gate

Use this guide after `carves pilot follow-up-intake --json` reports accepted planning items.

The planning gate is a read-only decision surface. It combines the accepted follow-up planning intake with formal planning posture so an agent can tell whether it should run `carves intent draft`, run `carves plan init <candidate>`, or stop because an active planning slot already exists.

## Commands

```powershell
carves pilot follow-up-gate
carves pilot follow-up-gate --json
carves pilot follow-up-planning-gate --json
carves pilot problem-follow-up-gate --json
carves inspect runtime-agent-problem-follow-up-planning-gate
carves api runtime-agent-problem-follow-up-planning-gate
```

## How To Read The Surface

- `overall_posture` gives the gate-level state.
- `next_governed_command` is the command the agent should follow next.
- `planning_gate_items[]` maps accepted candidates to gate status and candidate-specific next commands.
- `active_planning_slot_state` explains whether the formal planning slot is missing, ready, occupied, or blocked.
- `gaps[]` lists missing docs, planning intake blockers, or active planning slot conflicts.

## Agent Behavior

- If posture is `agent_problem_follow_up_planning_gate_no_accepted_records`, no follow-up planning work is open.
- If posture is `agent_problem_follow_up_planning_gate_waiting_for_intent_draft`, run `carves intent draft`, then rerun the gate.
- If posture is `agent_problem_follow_up_planning_gate_waiting_for_intent_draft_candidate_projection`, rerun `carves intent draft` so accepted follow-up inputs become active guided-planning candidates, resolve guided-planning decisions, then rerun the gate.
- If posture is `agent_problem_follow_up_planning_gate_ready_to_plan_init`, run the candidate `next_governed_command`; the candidate id must have been projected into the active intent draft by the previous `carves intent draft` read/write step.
- If posture is `agent_problem_follow_up_planning_gate_blocked_by_active_planning_slot`, run `carves plan status` and continue or close the existing slot before opening another candidate.
- If posture is blocked by planning intake or surface gaps, resolve the named gaps before planning.

## Boundaries

- Accepted decision records are planning inputs only.
- The gate does not create card truth, task truth, acceptance-contract truth, review truth, or writeback authority.
- Agents must not edit `.ai/cards/`, `.ai/tasks/`, graph files, reviews, acceptance contracts, or `.carves-platform/` directly to bypass this surface.
- Exactly one active planning card is allowed.
