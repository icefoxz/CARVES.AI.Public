# CARVES Agent Problem Triage Ledger

This guide records the Phase 35 read-only friction ledger for external agent problem reports.

## When To Use

Use this after one or more agents have reported problems through:

```powershell
carves pilot report-problem <json-path> --json
```

Then run:

```powershell
carves pilot triage --json
carves pilot follow-up --json
carves pilot follow-up-plan --json
carves pilot follow-up-record --json
carves pilot follow-up-intake --json
```

Equivalent read-only aliases:

```powershell
carves pilot problem-triage --json
carves pilot friction-ledger --json
carves inspect runtime-agent-problem-triage-ledger
carves api runtime-agent-problem-triage-ledger
```

Follow-up candidate readbacks:

```powershell
carves pilot follow-up --json
carves pilot problem-follow-up --json
carves pilot triage-follow-up --json
carves pilot follow-up-plan --json
carves pilot problem-follow-up-plan --json
carves pilot triage-follow-up-plan --json
carves pilot follow-up-record --json
carves inspect runtime-agent-problem-follow-up-candidates
carves api runtime-agent-problem-follow-up-candidates
carves inspect runtime-agent-problem-follow-up-decision-plan
carves api runtime-agent-problem-follow-up-decision-plan
```

## What The Ledger Shows

The ledger shows:

- recorded problem count
- blocking problem count
- problem-kind grouping
- severity grouping
- current-stage grouping
- newest review queue items
- recommended triage lanes
- operator actions
- follow-up candidate entry point

## Triage Lanes

The lane is a hint for where the operator should look first:

```text
command_contract_or_runtime_surface_review
protected_truth_root_policy_review
acceptance_contract_ingress_review
managed_workspace_or_path_lease_review
pilot_next_or_stage_status_review
dist_binding_or_attach_review
agent_bootstrap_or_constraint_ladder_review
operator_triage_required
```

## Required Operator Discipline

Use the ledger to group friction before opening work.

Use `carves pilot follow-up --json` to convert repeated or blocking ledger patterns into operator-review candidates. Then use `carves pilot follow-up-plan --json` to see accept/reject/wait choices, `carves pilot follow-up-record --json` to see durable operator decision records, and `carves pilot follow-up-intake --json` to see accepted planning inputs. These readbacks do not create governed work.

Do not let one problem report automatically become a task. First decide whether it is:

- target-specific confusion
- agent misunderstanding
- missing bootstrap instruction
- missing Runtime surface
- real product closure blocker
- operator choice outside Runtime scope

## Non-Claims

The ledger does not:

- create cards
- create tasks
- approve reviews
- resolve problem records
- authorize blocked changes
- edit protected truth roots
- edit `.gitignore`
- mutate runtime dist binding
- commit or push

The only durable follow-up route is still the governed planning lane.
