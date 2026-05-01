# CARVES Agent Trial V1 Registration

Status: Phase 1 minimum registration freeze for the proposed Agent Trial V1 product line.

This document freezes the V1 user registration boundary. It does not implement authentication, account storage, billing, organization management, submissions, receipts, or leaderboards.

## Goal

V1 registration exists for four narrow reasons:

- own trial submissions;
- own receipt history;
- control result visibility;
- make public leaderboards usable without anonymous spam.

V1 registration does not prove legal identity, enterprise identity, model identity, agent binary identity, or compliance status.

## Public Rule

Use this public language:

```text
Create an account to submit Agent Trial results, receive receipts, manage visibility, and appear on non-certification leaderboards.
```

Do not use this language:

```text
Verify your identity.
Certify your agent.
Register a certified model.
Create a compliance identity.
```

## Access Modes

V1 has two access modes.

Anonymous users can:

- inspect public trial documentation;
- inspect public official trial pack metadata;
- fetch or download public trial materials when policy allows;
- run local trials without submitting.

Registered users can:

- request server-issued challenges;
- create AgentProfiles;
- submit trial results;
- receive TrialReceipts;
- choose result visibility;
- appear in public leaderboards when eligible.

Result submission requires registration.

## Minimum Registration Fields

V1 account fields:

| Field | Required | Public | Purpose |
| --- | --- | --- | --- |
| `user_id` | yes | no | Stable platform identity key |
| `username` | yes | yes when profile public | Profile URL and leaderboard identity |
| `display_name` | yes | yes when profile public | Human-readable display name |
| `email` | yes | no | account recovery, notifications, abuse response |
| `oauth_provider` | yes | no | login provider record |
| `oauth_subject` | yes | no | provider-side stable subject |
| `public_profile` | yes | yes | profile visibility control |
| `terms_accepted_at` | yes | no | terms/privacy acceptance |
| `created_at` | yes | no | account creation audit |

GitHub OAuth is the preferred V1 login method for developer audiences. Equivalent OAuth can be added later, but V1 should not require complex identity verification.

## Username And Display Rules

`username` rules:

- stable enough for URLs;
- unique in the platform;
- not an email address;
- can be changed only under a controlled account setting;
- old public result URLs should remain resolvable through stable ids.

`display_name` rules:

- can be shown on leaderboards;
- can be changed;
- does not affect receipts;
- must not be used as a stable identity key.

## Email Rules

Email is private in V1.

Allowed uses:

- account recovery;
- security notification;
- trial result notification;
- abuse or duplicate-submission review;
- terms/privacy communication.

Forbidden public uses:

- leaderboard display;
- public profile display by default;
- exported leaderboard snapshots.

## Terms And Privacy Gate

Submitting a result requires terms and privacy acceptance.

The acceptance copy must state:

- uploads are summary-only by default;
- public submissions can appear on non-certification leaderboards;
- agent/model identity is self-reported unless explicitly verified by a later system;
- results do not certify safety, semantic correctness, or model identity;
- users should not upload secrets, credentials, private source, raw diffs, prompt transcripts, model response transcripts, customer payloads, or private file payloads in V1 default submissions.

## Visibility Controls

Every TrialResult has a visibility:

- `private`;
- `unlisted`;
- `public`.

Registration must allow users to manage visibility.

Rules:

- `private` results do not appear on public leaderboards.
- `unlisted` results do not appear on public leaderboards.
- `public` results can enter public leaderboards only after verification and anti-gaming gates.
- Changing visibility does not rewrite TrialReceipt history.
- Receipts remain bound to original submitted hashes.

## Registration And Submission Boundary

Submission eligibility requires:

- registered `user_id`;
- accepted terms/privacy;
- active or reusable AgentProfile;
- server-issued TrialChallenge;
- submitted TrialResult;
- visibility choice.

Leaderboard eligibility additionally requires:

- `public` visibility;
- Matrix verification pass;
- server-issued TrialReceipt;
- non-duplicate result posture;
- minimum verified run count when required by leaderboard rules.

## Abuse Controls For V1

V1 registration should support basic abuse controls:

- one account owns each challenge;
- one challenge can produce at most one accepted public result;
- duplicate result hashes can be flagged;
- per-user submit rate can be limited;
- public leaderboard entries can be excluded without deleting private history;
- account-level public profile can be disabled for abuse or policy reasons.

These controls are anti-spam and leaderboard hygiene. They are not model identity verification.

## Data Retention Boundary

TrialReceipts are immutable records of accepted submissions. Visibility changes should not alter receipt inputs.

Deletion or privacy requests should distinguish:

- public profile visibility;
- public leaderboard visibility;
- private account data;
- immutable receipt history needed for anti-replay and audit.

The exact deletion policy belongs in product privacy terms before launch.

## Example Registration Readback

```json
{
  "schema_version": "agent-trial-user.v0",
  "user_id": "usr_01",
  "username": "alice",
  "display_name": "Alice",
  "public_profile": true,
  "oauth_provider": "github",
  "terms_accepted": true
}
```

Email and provider subject are intentionally omitted from public readback.

## Phase 1 Acceptance Mapping

TRIAL-V1-010 is satisfied by this document:

- anonymous users can fetch or inspect public trial material;
- registered users can submit trial results;
- email is private and not displayed on leaderboards;
- public profile visibility is user-controlled;
- registration establishes result ownership without claiming verified real-world identity.
