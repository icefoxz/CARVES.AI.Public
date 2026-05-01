# CARVES agent problem follow-up planning intake

Use this guide after an operator has recorded follow-up decisions with `carves pilot record-follow-up-decision`.

The planning-intake surface is read-only. It tells an external agent which accepted follow-up decisions may be carried into formal planning.

## Commands

```powershell
carves pilot follow-up-intake
carves pilot follow-up-intake --json
carves pilot follow-up-planning --json
carves pilot problem-follow-up-intake --json
carves inspect runtime-agent-problem-follow-up-planning-intake
carves api runtime-agent-problem-follow-up-planning-intake
```

## Agent rule

Do not convert accepted follow-up decisions by editing `.ai` truth directly.

The required path is:

```text
carves pilot follow-up-record --json
carves pilot follow-up-intake --json
carves intent draft
carves plan init [candidate-card-id]
```

When clean accepted follow-up decisions exist and have not already been consumed by completed or merged task truth, `carves intent draft` projects them as guided-planning candidate cards using the follow-up candidate id. This keeps the later `carves plan init <candidate-card-id>` command mechanically executable instead of asking agents to run a candidate id that the planning lane cannot recognize.

If a completed or merged task node already carries `metadata.source_candidate_card_id=<candidate-card-id>`, that accepted follow-up candidate is treated as consumed. It stays visible as evidence through consumed candidate fields, but it does not reopen planning intake and is not re-projected into `intent draft`.

Carry the following into the planning card editable fields:

- decision record id
- related problem ids
- related evidence ids
- operator reason
- acceptance evidence
- readback command

## Postures

`agent_problem_follow_up_planning_intake_ready` means at least one accepted follow-up decision is clean, committed, and ready to enter formal planning.

`agent_problem_follow_up_planning_intake_no_accepted_records` means the readback is clean, but no accepted decision currently requires planning.

`agent_problem_follow_up_planning_intake_no_open_accepted_records` means accepted decision records exist, but their candidate ids have already been consumed by completed or merged task truth.

`agent_problem_follow_up_planning_intake_waiting_for_decision_record` means the decision-record surface is not ready. Resolve `carves pilot follow-up-record --json` first.

`agent_problem_follow_up_planning_intake_blocked_by_surface_gaps` means required Runtime docs or guide anchors are missing.

## Non-authority

This surface does not create or approve:

- cards
- tasks
- task graphs
- acceptance contracts
- reviews
- writebacks
- commits
- releases
- packs
- problem reports
- decision records

It only makes the next formal planning input explicit.
