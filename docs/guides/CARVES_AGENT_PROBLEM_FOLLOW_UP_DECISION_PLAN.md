# CARVES Agent Problem Follow-Up Decision Plan

This guide records the Phase 37 read-only decision plan for agent problem follow-up candidates.

## When To Use

Use this after one or more agents have reported problems, the triage ledger has been read, and follow-up candidates have been projected:

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
carves pilot problem-follow-up-plan --json
carves pilot triage-follow-up-plan --json
carves inspect runtime-agent-problem-follow-up-decision-plan
carves api runtime-agent-problem-follow-up-decision-plan
carves inspect runtime-agent-problem-follow-up-decision-record
carves api runtime-agent-problem-follow-up-decision-record
```

## What The Surface Shows

The surface shows:

- decision plan id
- candidate surface posture
- recorded problem count
- candidate count
- governed candidate count
- watchlist candidate count
- operator-review item count
- watchlist item count
- recommended decision per candidate
- decision options per candidate
- planning entry hint
- required acceptance evidence
- boundary rules

## Decision Postures

```text
operator_review_required
wait_for_more_evidence
```

`operator_review_required` means the candidate is repeated or blocking enough for an operator to decide accept, reject, or wait.

`wait_for_more_evidence` means the candidate remains visible, but should not become governed work unless the operator explicitly overrides the watchlist posture.

## Decision Options

For governed candidates:

```text
accept_as_governed_planning_input
reject_as_target_project_only
wait_for_more_evidence
```

For watchlist candidates:

```text
wait_for_more_evidence
accept_as_governed_planning_input_after_operator_override
reject_as_noise_or_target_only
```

## Required Operator Discipline

Use the decision plan to decide whether a friction pattern deserves governed work.

Before accepting a candidate, attach:

- related problem ids
- related evidence ids
- operator decision reason
- acceptance contract for the proposed fix
- readback command that proves the candidate is resolved

Accepted candidates still enter the normal planning lane. Use intent and plan commands; do not edit card or task truth directly.

When the operator makes the decision, record it with:

```powershell
carves pilot record-follow-up-decision <decision> [--all] [--candidate <candidate-id>...] --reason <text> [--acceptance-evidence <text>] [--readback <command>] --json
```

Run `carves pilot follow-up-record --json` before and after recording so the missing/stale/invalid/conflicting decision state is visible. Run `carves pilot follow-up-intake --json` after accepted records are clean and committed so planning inputs are explicit.

## Non-Authority

This surface does not:

- create cards
- create tasks
- record durable decisions
- approve reviews
- resolve problem records
- authorize blocked changes
- edit protected truth roots
- edit `.gitignore`
- mutate runtime dist binding
- stage, commit, tag, pack, or release

The only durable follow-up route is still the governed planning lane.

Phase 38 adds durable decision records, and Phase 39 projects accepted records as planning intake, but neither replaces the governed planning lane.
