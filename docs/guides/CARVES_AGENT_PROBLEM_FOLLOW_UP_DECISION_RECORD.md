# CARVES Agent Problem Follow-Up Decision Record

This guide records the Phase 38 durable decision-record layer for agent problem follow-up candidates.

## When To Use

Use this after one or more agents have reported problems, the triage ledger has been read, follow-up candidates have been projected, and the decision plan has been reviewed:

```powershell
carves pilot problem-intake --json
carves pilot triage --json
carves pilot follow-up --json
carves pilot follow-up-plan --json
carves pilot follow-up-record --json
carves pilot follow-up-intake --json
```

Equivalent read-only aliases:

```powershell
carves pilot follow-up-decision-record --json
carves pilot problem-follow-up-record --json
carves inspect runtime-agent-problem-follow-up-decision-record
carves api runtime-agent-problem-follow-up-decision-record
```

## Recording A Decision

Use:

```powershell
carves pilot record-follow-up-decision <decision> [--all] [--candidate <candidate-id>...] --reason <text> [--operator <name>] [--plan-id <id>] [--acceptance-evidence <text>] [--readback <command>] [--json]
```

Allowed decisions:

```text
accept_as_governed_planning_input
accept_as_governed_planning_input_after_operator_override
reject_as_target_project_only
reject_as_noise_or_target_only
wait_for_more_evidence
```

Short aliases:

```text
accept
reject
wait
```

`--all` records the required operator-review candidates when the current plan has required decisions. If no decision is required, `--all` records all current decision items.

Accepted decisions require:

- `--acceptance-evidence`
- `--readback`

After accepted records are clean and committed, run `carves pilot follow-up-intake --json` before opening formal planning. The planning-intake readback does not create cards or tasks; it only tells the agent to carry the accepted decision through `carves intent draft` and one `carves plan init [candidate-card-id]`.

## What The Surface Shows

The surface shows:

- decision plan id
- decision plan posture
- whether a decision is required
- required, recorded, and missing candidate ids
- current-plan records
- stale records
- invalid and malformed record paths
- conflicting candidate ids
- dirty, untracked, and uncommitted record paths
- boundary rules
- recommended next action

## Record Boundary

Records are written under:

```text
.ai/runtime/agent-problem-follow-up-decisions/
```

The record is target runtime evidence. After recording it, run target commit planning and commit the record through normal target closure before claiming it as durable proof.

## Non-Authority

This surface does not:

- create cards
- create tasks
- create acceptance contracts
- approve reviews
- write back files
- resolve problem records
- authorize blocked changes
- edit protected truth roots
- edit `.gitignore`
- mutate runtime dist binding
- stage, commit, tag, pack, or release

Accepted candidates still enter the normal intent and planning lane via `carves pilot follow-up-intake --json`. Rejected or waiting candidates remain visible evidence.
