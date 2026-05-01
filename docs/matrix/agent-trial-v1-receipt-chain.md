# CARVES Agent Trial V1 Receipt Chain

Status: Phase 6 receipt chain contract freeze for the proposed Agent Trial V1 product line.

This document defines the V1 server receipt chain for accepted summary-only Agent Trial submissions. It does not implement signing, key management, persistence, hosted verification, leaderboards, model identity verification, certification, semantic correctness proof, or operating-system sandboxing.

## Goal

The receipt chain gives users and the platform an immutable server-issued record for accepted submissions.

It answers:

```text
Which user, challenge, prompt version, task pack version, Matrix hashes, Shield hashes, trial result hash, and previous receipt did the server accept?
```

Receipts make result history tamper-evident and replay-aware. They do not prove that the local environment was impossible to manipulate.

## Receipt Schema

The receipt schema marker is:

```text
agent-trial-receipt.v0
```

Minimum fields:

| Field | Required | Meaning |
| --- | --- | --- |
| `schema_version` | yes | Must be `agent-trial-receipt.v0` |
| `receipt_id` | yes | Server receipt id |
| `receipt_sha256` | yes | Hash of canonical receipt bytes |
| `receipt_input_sha256` | yes | Hash of canonical receipt input |
| `result_id` | yes | Accepted TrialResult id |
| `user_id` | yes | Registered user id |
| `challenge_id` | yes | Server-issued challenge id |
| `agent_profile_snapshot_sha256` | yes | AgentProfile snapshot hash |
| `suite_id` | yes | Suite id |
| `pack_id` | yes | Pack id |
| `pack_version` | yes | Exact pack version |
| `task_id` | yes | Task id |
| `task_version` | yes | Exact task version |
| `prompt_id` | yes | Prompt id |
| `prompt_version` | yes | Exact prompt version |
| `matrix_manifest_sha256` | yes | Accepted Matrix manifest hash |
| `matrix_proof_summary_sha256` | yes | Accepted Matrix proof summary hash |
| `shield_evidence_sha256` | yes | Accepted Shield evidence hash |
| `shield_evaluation_sha256` | yes | Accepted Shield evaluation hash |
| `trial_result_sha256` | yes | Accepted TrialResult hash |
| `previous_receipt_sha256` | yes | Previous receipt hash or null |
| `visibility_at_acceptance` | yes | `private`, `unlisted`, or `public` |
| `issued_at` | yes | UTC timestamp |
| `server_key_id` | yes | Signing key id or receipt issuer key marker |
| `signature` | yes | Server signature when signing is implemented |

If signing is not implemented in the first internal prototype, `signature` can be an explicit placeholder only in non-public environments. Public V1 accepted receipts should be signed.

## Receipt Input

The canonical receipt input must include:

- user id;
- challenge id;
- AgentProfile snapshot hash;
- suite id;
- pack id and version;
- task id and version;
- prompt id and version;
- Matrix manifest hash;
- Matrix proof summary hash;
- Shield evidence hash;
- Shield evaluation hash;
- trial result hash;
- previous receipt hash when present;
- visibility at acceptance;
- issued timestamp.

The server signs or hashes the canonical input, not ad hoc prose.

## Example

```json
{
  "schema_version": "agent-trial-receipt.v0",
  "receipt_id": "rcpt_01",
  "receipt_sha256": "sha256:...",
  "receipt_input_sha256": "sha256:...",
  "result_id": "tr_01",
  "user_id": "usr_01",
  "challenge_id": "mch_01",
  "agent_profile_snapshot_sha256": "sha256:...",
  "suite_id": "official-agent-dev-safety",
  "pack_id": "official-agent-dev-safety-v1",
  "pack_version": "1.0.0",
  "task_id": "official-v1-task-003-test-discipline",
  "task_version": "1.0.0",
  "prompt_id": "official-v1-test-discipline",
  "prompt_version": "1.0.0",
  "matrix_manifest_sha256": "sha256:...",
  "matrix_proof_summary_sha256": "sha256:...",
  "shield_evidence_sha256": "sha256:...",
  "shield_evaluation_sha256": "sha256:...",
  "trial_result_sha256": "sha256:...",
  "previous_receipt_sha256": null,
  "visibility_at_acceptance": "public",
  "issued_at": "2026-04-16T00:00:00Z",
  "server_key_id": "srv_key_01",
  "signature": "sig:..."
}
```

## Chain Rules

Receipt chaining rules:

- accepted submissions receive receipts;
- receipts are immutable;
- old receipts cannot be silently rewritten;
- visibility changes do not alter the original receipt;
- a new accepted continuity challenge should reference the previous accepted receipt when continuity is required;
- missing or mismatched previous receipt breaks continuity posture;
- duplicate or replayed hashes are flagged.

The receipt chain can support a future display such as:

```text
Day 1: G9.H9.A9 = 100
Day 2: G4.H2.A1 = 30
Trend: initial_spike
Version scope: same prompt/pack/task/scoring profile
```

That display is a reproducibility signal, not a cheating accusation or certification. If the prompt, pack, task, scoring profile, collector, or verifier posture differs between days, the row comparison is trend-only unless a later server normalization process marks the versions comparable.

## Duplicate And Replay Flags

The server should flag:

- same `trial_result_sha256` submitted twice;
- same Matrix manifest hash reused under a different challenge;
- same receipt input attempted with changed visibility only;
- previous receipt hash that does not belong to the same user or continuity chain;
- public result attempt after the challenge already has an accepted public result.

Flags should be preserved for review and leaderboard hygiene.

## Verification Boundary

A receipt means:

- the server accepted the exact summary hashes listed in the receipt;
- the result was bound to a registered user and server-issued challenge;
- the accepted prompt and pack versions are recorded;
- replay and duplicate checks had a recorded outcome at acceptance time.

A receipt does not mean:

- model identity was verified;
- source code was semantically correct;
- the local environment was tamper-proof;
- the result is a safety certification;
- future behavior is guaranteed.

## Phase 6 Acceptance Mapping

TRIAL-V1-062 is satisfied by this document:

- accepted submissions receive a receipt;
- old receipts cannot be silently rewritten;
- duplicate or replayed result hashes are flagged.
