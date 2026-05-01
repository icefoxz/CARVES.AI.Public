# Session Gateway Operator Proof Contract

## Purpose

This document makes the private-alpha operator obligation surface explicit.

It exists so Session Gateway can say, in Runtime-owned truth, when a human must act, what proof source is currently in play, what evidence counts, and where the system must stop when real-world proof is still missing.

## Proof Sources

Session Gateway private alpha distinguishes these proof sources:

- `synthetic_fixture`
- `repo_local_proof`
- `operator_run_proof`
- `external_user_proof`

Current private-alpha posture starts at `repo_local_proof`.

That means:

- Runtime-owned tests, dogfood, and docs are real and valuable
- but they do not count as `operator_run_proof`
- and they definitely do not count as `external_user_proof`

## Blocking States

The operator-proof contract uses these explicit blocking states:

- `WAITING_OPERATOR_SETUP`
- `WAITING_OPERATOR_RUN`
- `WAITING_OPERATOR_EVIDENCE`
- `WAITING_OPERATOR_VERDICT`

Current private-alpha handoff starts in `WAITING_OPERATOR_SETUP`.

## Contract Events

The bounded contract now names these operator-proof events:

- `operator_action_required`
- `operator_project_required`
- `operator_evidence_required`
- `real_world_proof_missing`

These events are not a second control plane.

They are Runtime-owned projections that explain why the lane cannot be treated as real-world complete yet.

## Stage Exit Contracts

### setup

- blocking state: `WAITING_OPERATOR_SETUP`
- accepted proof sources: `operator_run_proof`, `external_user_proof`
- operator must do:
  - select or create a real project repository
  - record the real repo path
  - record the startup command
- AI may do:
  - explain the required repo shape
  - project current Runtime readiness
  - draft the exact startup steps
- required evidence:
  - `repo_path`
  - `startup_command`
  - `initial_startup_log`
- does not count:
  - synthetic fixture only
  - repo-local mock repo
  - agent assertion without a real repo

### run

- blocking state: `WAITING_OPERATOR_RUN`
- accepted proof sources: `operator_run_proof`, `external_user_proof`
- operator must do:
  - run the bounded scenario on the declared real project
  - capture the actual command path and outcome
- AI may do:
  - explain the next bounded run
  - interpret Runtime-owned state after the operator run
- required evidence:
  - `run_command`
  - `run_log_excerpt`
  - `result_or_failure_artifact`
- does not count:
  - repo-local unit test only
  - synthetic smoke without the real project

### evidence

- blocking state: `WAITING_OPERATOR_EVIDENCE`
- accepted proof sources: `operator_run_proof`, `external_user_proof`
- operator must do:
  - attach logs, result files, and operation identifiers
  - state whether the real-world outcome passed, failed, or blocked
- AI may do:
  - list the expected evidence bundle
  - summarize gaps between provided evidence and the contract
- required evidence:
  - `session_id`
  - `operation_id`
  - `event_stream_capture`
  - `runtime_or_app_log_excerpt`
  - `result_or_failure_artifact`
- does not count:
  - verbal claim without artifacts
  - screenshots without Runtime identifiers

### verdict

- blocking state: `WAITING_OPERATOR_VERDICT`
- accepted proof sources: `operator_run_proof`, `external_user_proof`
- operator must do:
  - provide the human verdict
  - explicitly say passed / failed / blocked
- AI may do:
  - summarize evidence
  - draft replan or recovery suggestions
- required evidence:
  - `operator_verdict_summary`
  - `pass_fail_or_blocked_decision`
- does not count:
  - agent self-certification
  - repo-local green tests only

## Current Bounded Meaning

`private_alpha_deliverable_ready` means the handoff surface and Runtime lane are ready to be used.

It does **not** mean:

- operator-run proof already exists
- external-user proof already exists
- the system may silently treat repo-local proof as real-world completion

Until a human completes the exit contracts above, Runtime must remain explicit about the missing proof.
