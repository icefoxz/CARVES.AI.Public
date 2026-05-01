# Agent Trial Local Hardening Plan

Status: planned follow-up after strict Matrix Agent Trial review.

This plan separates three trust layers instead of treating every local artifact as official proof.

- Matrix verifier: verifies manifest, hashes, summaries, schema, and trust-chain readback.
- Agent Trial local collector: collects local workspace evidence and projects local-only safety posture.
- Submit and leaderboard entry: must require server-issued authority, receipts, and versioned scoring before public comparison.

## Current Critical Gaps

1. The local collector currently treats the workspace task contract as authority. A tested agent can mutate the contract and then be judged by mutated rules.
2. Workspace evidence based only on `git diff` can miss untracked files, staged-only state, and files changed by required commands after the first diff snapshot.
3. Runtime trial artifact verification now enforces embedded public JSON schemas and cross-artifact identity consistency for the five local trial artifacts.
4. Trial verification mode is now explicit for submit-gate usage, while ordinary verification keeps non-trial Matrix compatibility and reports loose trial files as readback.
5. Trust-chain output now exposes a dedicated trial-artifacts gate, and comparability work now records explicit scoring, prompt, pack, collector, and verifier version posture.

## Non-Claims

- Local results remain local evidence, not certification.
- Local collector output remains `official_leaderboard_eligible=false` unless a later server-issued flow accepts it through a separate submit gate.
- This plan does not implement hosted verification, signing, transparency logs, remote sandboxing, or model identity verification.

## Execution Order

1. `CARD-891`: Freeze authority, eligibility, and local-only boundaries. Completed by `docs/matrix/agent-trial-v1-authority-boundary.md`.
2. `CARD-892`: Pin pack, challenge, and task contract authority before collector policy evaluation. Completed by collector pin enforcement, expected/actual hash result fields, and mismatch regression coverage.
3. `CARD-893`: Add an adversarial fixture harness so each later hardening card has a concrete attack case. Completed by named collect/verify/posture fixture cases for self-edited contracts, untracked/staged/post-command forbidden files, schema extra fields, cross-artifact mismatch, and loose trial files outside manifest coverage.
4. `CARD-894`: Replace one-shot diff evidence with complete pre/post workspace snapshots. Completed by pre/post `git status --porcelain=v1 -z` snapshot evidence, generated-artifact classification, and adversarial expectations for untracked, staged, and post-command forbidden files.
5. `CARD-895`: Split collector status, verification status, authority mode, and leaderboard eligibility semantics. Completed by local-only result fields, schema constraints, verifier eligibility checks, and tests that reject local result leaderboard claims.
6. `CARD-896`: Enforce strict runtime schema validation for trial artifacts. Completed by embedded public schemas, a bounded runtime schema validator, `trial_artifact_schema_invalid` reason codes with instance paths, and verifier tests for extra fields, wrong consts, wrong enums, wrong types, invalid relative paths, and local leaderboard claims.
7. `CARD-897`: Enforce cross-artifact consistency across task, report, diff, tests, and result. Completed by task-contract-anchored checks for suite, pack, task, prompt, and challenge identity wherever those fields exist, plus mismatch tests for challenge id, task version, and prompt version.
8. `CARD-898`: Add explicit trial verification mode and submit-gate semantics. Completed by `verify --trial` / `--require-trial`, strict five-artifact coverage, loose-file compatibility readback, and submit-gate documentation.
9. `CARD-899`: Add a dedicated trial-artifacts trust-chain gate. Completed by the `trial_artifacts` gate, non-trial compatibility reason text, trial failure gate propagation, and hash/missing regression coverage.
10. `CARD-900`: Freeze scoring/version comparability fields and tests. Completed by local trial result scoring profile fields, version comparability readback, schema/runtime verification, and same-version versus trend-only documentation.

## Target End State

A local Agent Trial run should answer: did the agent stay inside the task boundary, are its claims consistent with collected evidence, did required commands run, and is the resulting summary bundle tamper-evident?

A leaderboard entry should require a stricter server-issued chain and must not be inferred from local collection alone.
