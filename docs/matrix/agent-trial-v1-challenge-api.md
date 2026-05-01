# CARVES Agent Trial V1 Challenge Issue API

Status: Phase 6 challenge API contract freeze for the proposed Agent Trial V1 product line.

This document defines the V1 server-issued challenge contract. It does not implement authentication, persistence, hosted verification, submissions, receipts, leaderboards, model identity verification, certification, semantic correctness proof, or operating-system sandboxing.

## Goal

The challenge issue API lets a registered user request a fresh official Agent Trial challenge.

It answers:

```text
Which registered user, AgentProfile snapshot, official pack version, prompt version, task, expiry, and previous receipt is this local run bound to?
```

A server-issued challenge makes submissions comparable and replay-resistant. It does not prove that the local machine, local tools, or self-reported model identity were honest.

## Endpoint Shape

Proposed V1 endpoint:

```text
POST /v1/agent-trial/challenges
```

Authentication is required.

The user must have:

- registered account;
- accepted terms and privacy;
- reusable AgentProfile or an AgentProfile snapshot submitted with the request;
- permission to request the selected official suite.

## Request Fields

Minimum request fields:

| Field | Required | Meaning |
| --- | --- | --- |
| `suite_id` | yes | Official suite id, such as `official-agent-dev-safety` |
| `pack_version` | no | Exact pack version or omitted for current official version |
| `prompt_version` | yes | Exact prompt version or `latest` alias |
| `task_id` | no | Optional requested task; server can assign if omitted |
| `agent_profile_id` | yes | Registered user's AgentProfile id |
| `requested_visibility` | no | Initial preferred result visibility |
| `previous_receipt_sha256` | no | Previous accepted receipt hash for continuity |
| `client_nonce` | no | Optional client-generated nonce for diagnostics |

`latest` must resolve to an exact prompt version in the response. The accepted result must store the exact version, not the alias.

## Response Fields

The response schema marker is:

```text
carves-trial-challenge.v0
```

Minimum response fields:

| Field | Required | Meaning |
| --- | --- | --- |
| `schema_version` | yes | Must be `carves-trial-challenge.v0` |
| `challenge_id` | yes | Server-issued challenge id |
| `challenge_source` | yes | Must be `server_issued` |
| `user_id` | yes | Registered user id |
| `agent_profile_snapshot` | yes | Immutable self-reported AgentProfile snapshot |
| `agent_profile_snapshot_sha256` | yes | Hash of the snapshot used for this challenge |
| `suite_id` | yes | Official suite id |
| `pack_id` | yes | Official pack id |
| `pack_version` | yes | Exact pack version |
| `task_pack_version` | yes | Exact task pack version |
| `task_id` | yes | Assigned task id |
| `task_version` | yes | Exact task version |
| `prompt_id` | yes | Prompt id |
| `prompt_version` | yes | Exact prompt version |
| `prompt_sha256` | yes | Hash of issued prompt material or prompt descriptor |
| `expected_task_contract_sha256` | yes | Hash of the issued task contract bytes the collector must pin before policy evaluation |
| `challenge_nonce` | yes | Server-generated binding nonce |
| `previous_receipt_sha256` | yes | Previous receipt hash or null |
| `issued_at` | yes | UTC timestamp |
| `expires_at` | yes | Challenge expiry |
| `submission_deadline` | yes | Last accepted submission time |
| `max_accepted_public_results` | yes | V1 value is `1` |
| `privacy` | yes | Summary-only posture |

`challenge_nonce` is an anti-replay binding input. It is not a magic secret that prevents a fully controlled local environment from cheating. It makes stale or copied bundles easier to reject when combined with the challenge id and receipt chain.

## Example Response

```json
{
  "schema_version": "carves-trial-challenge.v0",
  "challenge_id": "mch_01",
  "challenge_source": "server_issued",
  "user_id": "usr_01",
  "agent_profile_snapshot": {
    "agent_profile_id": "ap_01",
    "agent_label": "User reported agent",
    "model_label": "User reported model",
    "reasoning_depth": "high",
    "self_reported": true
  },
  "agent_profile_snapshot_sha256": "sha256:...",
  "suite_id": "official-agent-dev-safety",
  "pack_id": "official-agent-dev-safety-v1",
  "pack_version": "1.0.0",
  "task_pack_version": "official-v1",
  "task_id": "official-v1-task-003-test-discipline",
  "task_version": "1.0.0",
  "prompt_id": "official-v1-test-discipline",
  "prompt_version": "1.0.0",
  "prompt_sha256": "sha256:...",
  "expected_task_contract_sha256": "sha256:...",
  "challenge_nonce": "nonce_opaque_server_value",
  "previous_receipt_sha256": null,
  "issued_at": "2026-04-16T00:00:00Z",
  "expires_at": "2026-04-17T00:00:00Z",
  "submission_deadline": "2026-04-17T00:30:00Z",
  "max_accepted_public_results": 1,
  "privacy": {
    "summary_only": true,
    "source_upload_required": false,
    "raw_diff_upload_required": false,
    "prompt_response_upload_required": false,
    "model_response_upload_required": false
  }
}
```

## Binding Rules

The server must bind each challenge to:

- user id;
- AgentProfile snapshot;
- official suite id;
- pack id and version;
- task id and version;
- prompt id and exact prompt version;
- challenge nonce;
- previous receipt hash when present;
- issue and expiry timestamps.

The local task contract must copy the same challenge id, task id/version, prompt id/version, and pack version. A submitted result with mismatched values is ineligible.

## Single Accepted Public Result Rule

One challenge can produce at most one accepted public result.

Allowed server states:

- `issued`;
- `expired`;
- `submitted`;
- `accepted_private`;
- `accepted_unlisted`;
- `accepted_public`;
- `rejected`;
- `superseded_private_visibility_change`.

V1 should reject a second public accepted result for the same challenge. Private resubmission policy can be stricter, but public leaderboard behavior must not allow one challenge to be farmed repeatedly.

## Expiry Rules

The server should reject official public submissions when:

- challenge id is unknown;
- challenge belongs to another user;
- challenge expired before submission;
- prompt version or pack version no longer matches the issued challenge;
- previous receipt does not match the user's latest accepted continuity state when continuity is required.

Expired challenges can still be shown in user history, but they must not become public leaderboard entries.

## Privacy Boundary

Challenge responses are metadata only.

Allowed:

- ids and versions;
- AgentProfile self-report snapshot;
- prompt hash or prompt descriptor hash;
- challenge nonce;
- previous receipt hash;
- expiry and submission deadline.

Forbidden:

- private source;
- raw diff;
- prompt response transcript;
- model response transcript;
- secrets;
- credentials;
- customer payloads.

## Phase 6 Acceptance Mapping

TRIAL-V1-060 is satisfied by this document:

- the server-issued challenge includes suite id, task pack version, prompt version, challenge id, expiry, and previous receipt reference when present;
- one challenge can produce at most one accepted public result;
- the challenge is bound to a registered user and AgentProfile snapshot.
