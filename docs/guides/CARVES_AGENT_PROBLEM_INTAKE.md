# CARVES Agent Problem Intake

This guide is the bounded path for an external agent to stop and report a CARVES blocker.

It is for agent-discovered problems, not for bypassing governance.

## Read The Contract

```powershell
carves pilot problem-intake --json
```

Read:

- `accepted_problem_kinds`
- `required_payload_fields`
- `optional_payload_fields`
- `payload_rules`
- `stop_and_report_triggers`
- `recommended_next_action`

## Report A Problem

Create a JSON payload outside protected truth roots, then submit it:

```powershell
carves pilot report-problem .carves-agent/problem-intake.json --json
```

Minimum payload:

```json
{
  "summary": "The next governed command failed and the agent stopped.",
  "problem_kind": "command_failed"
}
```

Recommended payload:

```json
{
  "summary": "CARVES asked for a protected truth root edit, so the agent stopped.",
  "problem_kind": "protected_truth_root_requested",
  "severity": "blocking",
  "current_stage_id": "formal_plan_init",
  "next_governed_command": "carves plan init candidate-first-slice",
  "blocked_command": "carves plan init candidate-first-slice",
  "command_exit_code": 1,
  "command_output": "Short bounded command output.",
  "stop_trigger": "the agent wants to modify a protected truth root directly",
  "affected_paths": [
    ".ai/tasks/graph.json"
  ],
  "observations": [
    "The command did not provide a safe mutation path."
  ],
  "recommended_follow_up": "Operator should decide whether this needs a new planning card or a Runtime bug."
}
```

## Inspect Records

```powershell
carves pilot list-problems
carves pilot inspect-problem <problem-id>
carves pilot list-evidence
carves pilot inspect-evidence <evidence-id>
```

## Triage Records

After submitting a problem, run:

```powershell
carves pilot triage --json
carves pilot follow-up --json
carves pilot follow-up-plan --json
carves pilot follow-up-record --json
carves pilot follow-up-intake --json
```

Equivalent aliases:

```powershell
carves pilot problem-triage --json
carves pilot friction-ledger --json
carves pilot problem-follow-up --json
carves pilot triage-follow-up --json
carves pilot problem-follow-up-plan --json
carves pilot triage-follow-up-plan --json
carves pilot follow-up-record --json
```

The triage ledger groups recorded problem intake by problem kind, severity, current stage, and recommended operator triage lane. It is read-only and does not resolve records or authorize the blocked change.

The follow-up candidates surface promotes repeated or blocking problem patterns into operator-review candidates. The follow-up decision plan projects accept/reject/wait choices for those candidates. Both are read-only and do not create cards, tasks, or approvals.

`report-problem` writes both:

- `.ai/runtime/pilot-problems/<problem-id>.json`
- `.ai/runtime/pilot-evidence/<evidence-id>.json`

## Stop Triggers

Report a problem when:

- `next_governed_command` is missing, contradictory, or conflicts with the user scope
- a CARVES command fails or returns blocked posture
- the agent wants to edit `.ai/tasks/`, `.ai/memory/`, `.ai/artifacts/reviews/`, or `.carves-platform/`
- executable work lacks an acceptance contract
- the agent would edit outside a managed workspace lease or declared writable path
- Runtime root, dist binding, or target attach state is ambiguous
- the agent is about to rationalize a CARVES warning instead of following the surfaced next action

## Non-Authority

Problem intake does not:

- create cards or tasks
- approve review
- write back files
- stage or commit
- edit `.gitignore`
- retarget Runtime manifests
- authorize the blocked change
- replace operator triage

The correct agent behavior is:

```text
detect blocker
-> read pilot problem-intake
-> submit report-problem
-> run pilot triage
-> run pilot follow-up
-> run pilot follow-up-plan
-> run pilot follow-up-record
-> run pilot follow-up-intake
-> stop and return the problem id, evidence id, triage lane, follow-up candidate status, and decision posture when available
```
