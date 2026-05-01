# CARVES Agent Trial V1 Object Model

Status: Phase 0 object model freeze for the proposed Agent Trial V1 product line.

This document defines the first platform objects before implementation starts. It does not implement persistence, APIs, authentication, submissions, receipts, or leaderboards.

## Object List

V1 defines ten objects:

- User
- AgentProfile
- TrialSuite
- TrialPack
- TrialTask
- TrialPrompt
- TrialChallenge
- TrialResult
- TrialReceipt
- LeaderboardSnapshot

## Object Rules

Every object must define:

- owner;
- identity key;
- versioning rule;
- visibility rule.

No V1 object may imply public certification, verified model identity, semantic correctness proof, or hosted verification.

## Object Table

| Object | Owner | Identity Key | Versioning Rule | Visibility Rule |
| --- | --- | --- | --- | --- |
| User | Platform account system | `user_id` | Account record is mutable, audit history retained for submissions | Public only through selected profile fields |
| AgentProfile | User | `agent_profile_id` | Mutable profile, immutable snapshot copied into each result | Public only when attached to public result/profile |
| TrialSuite | CARVES official publisher | `suite_id` | Immutable major versions, additive patch metadata allowed | Public |
| TrialPack | CARVES official publisher | `pack_id` + `pack_version` | Immutable once published | Public |
| TrialTask | TrialPack publisher | `task_id` + `task_version` | Immutable once published | Public in official packs |
| TrialPrompt | TrialPack publisher or server challenge issuer | `prompt_id` + `prompt_version` | Immutable once used by accepted results | Public or redacted by policy, version always public |
| TrialChallenge | Server | `challenge_id` | Immutable after issue, expires by policy | Private to user until result visibility allows disclosure |
| TrialResult | User submission, server accepted | `trial_result_id` | Immutable after acceptance; visibility can change | `private`, `unlisted`, or `public` |
| TrialReceipt | Server | `receipt_id` or `receipt_sha256` | Immutable | Same or narrower than linked result |
| LeaderboardSnapshot | Platform leaderboard job | `leaderboard_id` + `snapshot_id` | Immutable snapshot, superseded by later snapshots | Public when leaderboard is public |

## User

Purpose:

```text
Own submissions, receipts, visibility settings, and AgentProfiles.
```

Minimum V1 fields:

- `user_id`
- `username`
- `display_name`
- `email`
- `oauth_provider`
- `oauth_subject`
- `public_profile`
- `terms_accepted_at`
- `created_at`

Rules:

- Email is private.
- Username/display name can appear on public leaderboards if public profile is enabled.
- Registration is required for result submission.
- Registration is not required to inspect public trial material.

## AgentProfile

Purpose:

```text
Record a reusable self-reported agent/model/tool profile for submissions.
```

Minimum V1 fields:

- `agent_profile_id`
- `user_id`
- `agent_label`
- `model_label`
- `reasoning_depth`
- `tool_profile`
- `permission_profile`
- `os_summary`
- `self_reported`
- `created_at`
- `updated_at`

Snapshot fields copied into TrialResult:

- `agent_label`
- `model_label`
- `reasoning_depth`
- `tool_profile`
- `permission_profile`
- `os_summary`
- `self_reported`

Rules:

- `self_reported` is always `true` in V1.
- A public result must label agent/model identity as self-reported.
- Updating an AgentProfile does not rewrite old TrialResults.

## TrialSuite

Purpose:

```text
Group official trial packs under a named product surface.
```

Example:

```text
suite_id = official-agent-dev-safety
```

Minimum V1 fields:

- `suite_id`
- `suite_version`
- `publisher`
- `status`
- `public_claim_boundary`

Rules:

- V1 official leaderboards use official suites only.
- Community suites are reserved for later phases.

## TrialPack

Purpose:

```text
Define the downloadable workspace, constraints, tasks, prompts, and expected local evidence shape.
```

Minimum V1 fields:

- `pack_id`
- `pack_version`
- `suite_id`
- `publisher`
- `license`
- `workspace_shape`
- `task_ids`
- `prompt_ids`
- `constraints_hash`
- `created_at`

Rules:

- Pack versions are immutable after accepted public results exist.
- Results must include `pack_id` and `pack_version`.
- Official pack artifacts must not contain secrets, customer data, or private payloads.

## TrialTask

Purpose:

```text
Define the assigned local development task and boundary.
```

Minimum V1 fields:

- `task_id`
- `task_version`
- `pack_id`
- `objective`
- `allowed_paths`
- `forbidden_paths`
- `required_commands`
- `expected_evidence`
- `failure_posture`

Rules:

- A TrialResult must bind to exactly one task.
- Task versions are immutable after use.
- The task contract is hashed into the result.

## TrialPrompt

Purpose:

```text
Define the test instruction text or instruction class used by a challenge.
```

Minimum V1 fields:

- `prompt_id`
- `prompt_version`
- `suite_id`
- `pack_id`
- `task_id`
- `instruction_class`
- `prompt_text_hash`
- `prompt_text_included`
- `created_at`

Rules:

- Prompt version is always visible in result and leaderboard grouping.
- Prompt text does not need to be uploaded back by users.
- If prompt text is redacted from public display, the version and hash remain public.
- Results from different prompt versions must not be silently mixed.

## TrialChallenge

Purpose:

```text
Bind a user, AgentProfile snapshot, official task/prompt version, expiry, and optional previous receipt before local execution.
```

Minimum V1 fields:

- `challenge_id`
- `user_id`
- `agent_profile_snapshot`
- `suite_id`
- `pack_id`
- `pack_version`
- `task_id`
- `task_version`
- `prompt_id`
- `prompt_version`
- `previous_receipt_sha256`
- `issued_at`
- `expires_at`
- `status`

Rules:

- One challenge can produce at most one accepted public result.
- Expired challenges can be rejected or marked ineligible for leaderboard use.
- Challenge ids are server-issued.

## TrialResult

Purpose:

```text
Record the submitted summary-only result for one challenge.
```

Minimum V1 fields:

- `trial_result_id`
- `user_id`
- `challenge_id`
- `suite_id`
- `pack_id`
- `pack_version`
- `task_id`
- `task_version`
- `prompt_id`
- `prompt_version`
- `agent_profile_snapshot`
- `visibility`
- `standard_label`
- `lite_score`
- `safety_posture`
- `matrix_verify_status`
- `matrix_manifest_sha256`
- `matrix_proof_summary_sha256`
- `shield_evidence_sha256`
- `shield_evaluate_sha256`
- `task_contract_sha256`
- `expected_task_contract_sha256`
- `actual_task_contract_sha256`
- `agent_report_sha256`
- `diff_scope_summary_sha256`
- `test_evidence_sha256`
- `trial_result_sha256`
- `submitted_at`

Rules:

- TrialResult is immutable after acceptance.
- Visibility can change without changing the receipt.
- Agent/model fields remain self-reported in V1.
- A result cannot be official-leaderboard eligible unless Matrix verification passed and required hashes are present.

## TrialReceipt

Purpose:

```text
Give the user a server-issued record that binds the accepted result to exact submitted hashes and previous receipt state.
```

Minimum V1 fields:

- `receipt_id`
- `receipt_sha256`
- `trial_result_id`
- `user_id`
- `challenge_id`
- `previous_receipt_sha256`
- `receipt_input_hash`
- `issued_at`
- `server_key_id`

Receipt input must include:

- user id;
- challenge id;
- suite id;
- pack version;
- task version;
- prompt version;
- Matrix manifest hash;
- proof summary hash;
- Shield evidence hash;
- Shield evaluation hash;
- trial result hash;
- previous receipt hash when present.

Rules:

- Receipts are immutable.
- Receipt history must not be rewritten when result visibility changes.
- Receipts do not imply certification.

## LeaderboardSnapshot

Purpose:

```text
Materialize stable leaderboard read models from accepted public results.
```

Minimum V1 fields:

- `leaderboard_id`
- `snapshot_id`
- `leaderboard_type`
- `suite_id`
- `pack_version`
- `prompt_version`
- `generated_at`
- `ranking_rule`
- `minimum_verified_runs`
- `entries`

V1 leaderboard types:

- prompt version leaderboard;
- self-reported agent/profile leaderboard;
- task difficulty leaderboard.

Rules:

- Ranking uses median or rolling verified score, not historical best score.
- Entries must show verified run count.
- Entries must display self-reported agent/model identity labels when applicable.
- Official leaderboards use official packs only.

## Relationship Summary

```text
User
  -> AgentProfile
  -> TrialChallenge
  -> TrialResult
  -> TrialReceipt

TrialSuite
  -> TrialPack
  -> TrialTask
  -> TrialPrompt

LeaderboardSnapshot
  -> accepted public TrialResults
```

## Required TrialResult Bindings

Every accepted TrialResult must bind to:

- registered `user_id`;
- `agent_profile_snapshot`;
- `challenge_id`;
- `suite_id`;
- `pack_id`;
- `pack_version`;
- `task_id`;
- `task_version`;
- `prompt_id`;
- `prompt_version`;
- `matrix_manifest_sha256`;
- `matrix_proof_summary_sha256`;
- `shield_evidence_sha256`;
- `shield_evaluate_sha256`.

These bindings are the minimum needed for prompt version leaderboards, agent/profile leaderboards, task difficulty leaderboards, receipt chains, and duplicate-result detection.

## Phase 0 Acceptance Mapping

TRIAL-V1-002 is satisfied by this document:

- each V1 object has an owner, identity key, versioning rule, and visibility rule;
- AgentProfile fields are explicitly self-reported in V1;
- TrialResult binds prompt version, task pack version, challenge id, and Matrix hashes.

TRIAL-V1-001 is satisfied by `docs/matrix/agent-trial-v1-boundary.md`.
