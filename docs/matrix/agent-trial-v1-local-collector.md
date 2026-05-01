# CARVES Agent Trial V1 Local Collector

Status: CARD-910 local collector command surface is available through `carves-matrix trial collect`.

This document defines the V1 local collection command behavior. It does not implement collection, scoring, submission, receipts, hosted verification, or leaderboards.

## Goal

The local collector turns a completed local workspace into a summary-only trial result bundle.

It answers:

```text
Which local evidence artifacts exist, which are missing, which hashes bind them, and can the result be considered locally collectable?
```

The collector must not upload source code, raw diff text, prompt responses, model responses, secrets, credentials, or private payloads.

The collector is a local evidence producer. It does not grant official leaderboard eligibility. `local_collection_status=collectable` means only that the local collector produced the expected summary artifacts. It does not mean Matrix verified the bundle, the server accepted it, or any leaderboard gate passed. Local collector output remains `authority_mode=local_only`, `verification_status=not_matrix_verified_by_collector`, and `leaderboard_eligibility.official_leaderboard_eligible=false` unless a later server-issued submit flow validates the exact summary artifacts and issues a receipt.

Authority mode rules are frozen in `docs/matrix/agent-trial-v1-authority-boundary.md`.

## Command Shape

Current command shape:

```text
carves-matrix trial collect --workspace ./carves-agent-trial --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle
```

Optional shape:

```text
carves-matrix trial local --workspace ./carves-agent-trial --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle --json
```

`trial collect` writes local evidence and the Matrix trial bundle. `trial local` runs the same collection step and then strict Matrix verification against the bundle. The default workspace for planning is the current directory, and the default local evidence root is `artifacts/` under the workspace.

## Required Inputs

The collector reads local files only.

These files are local candidate evidence. For `server_issued` or `leaderboard_eligible` flows, `.carves/trial/pack.json`, `.carves/trial/challenge.json`, `.carves/trial/instruction-pack.json`, and `.carves/trial/task-contract.json` must be checked against authority outside the mutable workspace before their task rules are trusted. The local dry-run collector now requires explicit expected task contract and instruction pack hashes in local pack/challenge metadata and fails closed before policy or prompt/instruction comparability evaluation when the candidate bytes do not match those pins.

Required local inputs:

| Artifact | Default path | Schema |
| --- | --- | --- |
| Pack metadata | `.carves/trial/pack.json` | `carves-trial-pack.v0` |
| Challenge metadata | `.carves/trial/challenge.json` | `carves-trial-challenge.v0` |
| Instruction pack metadata | `.carves/trial/instruction-pack.json` | `carves-agent-instruction-pack.v0` |
| Task contract | `.carves/trial/task-contract.json` | `matrix-agent-task.v0` |
| Agent report | `artifacts/agent-report.json` | `agent-report.v0` |
| Diff scope summary | `artifacts/diff-scope-summary.json` | `diff-scope-summary.v0` |
| Test evidence | `artifacts/test-evidence.json` | `test-evidence.v0` |
| Guard decision output | `artifacts/guard/decisions.jsonl` | Guard decision store |
| Handoff packet | `artifacts/handoff/handoff.json` | Handoff packet |
| Audit evidence | `artifacts/audit/shield-evidence.json` | `shield-evidence.v0` |
| Shield evaluation | `artifacts/shield/shield-evaluate.json` | Shield evaluation JSON |
| Matrix proof summary | `artifacts/matrix/matrix-proof-summary.json` | `matrix-proof-summary.v0` |
| Matrix manifest | `artifacts/matrix/matrix-artifact-manifest.json` | `matrix-artifact-manifest.v0` |
| Matrix verify output | `artifacts/matrix/matrix-verify.json` | `matrix-verify.v0` |

The final implementation can allow path overrides, but V1 eligibility logic must still bind every required artifact by role and hash.

Diff scope collection records a pre-command workspace snapshot, runs required commands, then records a post-command workspace snapshot. The summary includes tracked, staged, unstaged, untracked, deleted, renamed, and command-generated file paths without raw diff text or source contents. Generated build residue is visible but whitelisted as generated artifact output; forbidden or unknown paths remain review pressure.

## Output

The collector writes:

```text
artifacts/carves-agent-trial-result.json
```

The output schema marker is:

```text
carves-agent-trial-result.v0
```

Phase 5 defines the full safety posture projection and result contract. The CARD-910 command surface exposes the collector-owned local envelope and bundle generation, but it still does not submit anything or create leaderboard eligibility.

Minimum local envelope fields:

| Field | Required | Meaning |
| --- | --- | --- |
| `schema_version` | yes | Must be `carves-agent-trial-result.v0` |
| `suite_id` | yes | From pack/challenge/task contract |
| `pack_id` | yes | From pack metadata |
| `pack_version` | yes | Exact pack version |
| `task_id` | yes | Task identity |
| `task_version` | yes | Task version |
| `prompt_id` | yes | Prompt identity |
| `prompt_version` | yes | Exact prompt version, never `latest` |
| `instruction_pack` | yes | Instruction pack, canonical instruction file set, prompt sample identity, and pin readback |
| `challenge_id` | yes | Challenge identity |
| `challenge_source` | yes | `server_issued`, `pack_local_dry_run`, or `cache_replay` |
| `scoring_profile_id` | yes | Local scoring/profile projection identity |
| `scoring_profile_version` | yes | Local scoring/profile projection version |
| `local_score` | yes | Versioned local dimension score, aggregate score, caps, suppressions, explanations, and non-claims |
| `version_comparability` | yes | Instruction pack, prompt, pack, task, collector, verifier, and scoring version posture |
| `agent_profile_snapshot` | yes | Self-reported profile snapshot when available |
| `matrix_verify_status` | yes | Matrix verify posture |
| `local_collection_status` | yes | `collectable`, `failed_closed`, or `partial_local_only` |
| `authority_mode` | yes | `local_only` unless a later server-issued flow accepts the result |
| `verification_status` | yes | `not_matrix_verified_by_collector` for local collector output |
| `artifact_hashes` | yes | Role-to-SHA-256 binding for collected artifacts |
| `missing_required_artifacts` | yes | Required artifacts not found |
| `official_leaderboard_eligible` | yes | `false` for local collector output |
| `leaderboard_eligibility` | yes | Structured local-only eligibility posture; `status=ineligible_local_only` for collector output |
| `privacy` | yes | Summary-only privacy posture |

The collector may include preliminary posture placeholders such as `safety_posture_pending=true` until Phase 5 defines the final projection.

Local collector output records `matrix_verifier_version=unavailable_local_only` inside `version_comparability` because collection happens before Matrix verify. Direct leaderboard comparison requires matching suite, pack, task, instruction pack, prompt, scoring profile, collector, and verifier posture; otherwise rows are trend-only unless a later server normalization layer marks them comparable. If a user edits `AGENTS.md`, `CLAUDE.md`, or prompt samples for private experiments, the run can still be collected as local evidence, but it is not directly comparable to the official prompt version unless the pinned instruction pack identity and hash still match.

## Artifact Hash Rules

Every collected artifact role must be hash-bound.

Required hashes:

- `task_contract_sha256`;
- `expected_task_contract_sha256`;
- `actual_task_contract_sha256`;
- `instruction_pack_sha256`;
- `expected_instruction_pack_sha256`;
- `actual_instruction_pack_sha256`;
- `agent_report_sha256`;
- `diff_scope_summary_sha256`;
- `test_evidence_sha256`;
- `guard_decisions_sha256`;
- `handoff_packet_sha256`;
- `audit_evidence_sha256`;
- `shield_evaluation_sha256`;
- `matrix_manifest_sha256`;
- `matrix_proof_summary_sha256`;
- `matrix_verify_sha256`;
- `trial_result_sha256`.

`trial_result_sha256` is computed after writing the result envelope with a deterministic hash procedure defined by implementation. It must not include mutable filesystem paths outside the bundle.

`local_score.aggregate_score` is emitted only when prerequisite evidence is present. If collection fails closed or a dimension is unavailable, `aggregate_score` is `null` and `local_score.suppression_reasons` explains why.

## Fail-Closed Rules

The collector must fail closed when required local artifacts are missing or unreadable.

The collector must also fail closed with `task_contract_pin_mismatch` when the workspace task contract hash differs from the expected task contract hash in the active local dry-run pack/challenge metadata. In that state, the collector must not evaluate allowed paths, forbidden paths, or required commands from the mismatched task contract.

The collector must fail closed with `instruction_pack_pin_mismatch` when the workspace instruction pack metadata hash differs from the expected instruction pack hash in the active local dry-run pack/challenge metadata. In that state, the collector must not treat the workspace instruction files or prompt samples as the official comparable version.

Fail-closed means:

- no official leaderboard eligibility;
- `official_leaderboard_eligible=false`;
- `leaderboard_eligibility.official_leaderboard_eligible=false`;
- output status is `failed_closed`;
- missing roles are listed;
- no missing field is guessed from prose;
- no source, raw diff, prompt response, or model response is uploaded or embedded.

Partial local-only output can be useful for debugging, but it must be clearly ineligible.

## Evidence Comparison Rules

The collector does not decide final score in Phase 4, but it records comparison inputs:

- task contract vs diff scope allowed/forbidden posture;
- task contract vs test evidence required commands;
- agent report vs diff scope changed files;
- agent report vs test evidence claims;
- Matrix verify status vs Matrix manifest and proof summary hashes;
- Shield evaluation hash vs Matrix-covered Shield artifact.

Agent report fields are self-attestation claims. They can improve report-honesty posture only when they match collected evidence, and they can hurt posture when contradicted by diff or command evidence. They must not override required command failures, task-contract pin mismatches, missing artifacts, Matrix verify failures, or leaderboard eligibility gates.

Phase 5 consumes these comparisons to project safety posture.

## Privacy Boundary

The collector output remains summary-only by default.

Allowed:

- artifact paths relative to the trial workspace;
- artifact hashes;
- schema versions;
- task/prompt/challenge identifiers;
- status labels;
- bounded issue codes;
- self-reported agent profile snapshot.

Forbidden by default:

- source code;
- raw diff text;
- full command logs;
- prompt response text;
- model response text;
- secrets;
- credentials;
- customer payloads;
- absolute local paths in public output.

## Phase 4 Acceptance Mapping

TRIAL-V1-041 is satisfied by this document:

- the local collect command path is frozen;
- required inputs include task contract, agent report, diff scope, test evidence, Guard, Handoff, Audit, Shield, and Matrix outputs;
- `carves-agent-trial-result.v0` is the collector output envelope;
- missing required local artifacts fail closed.
