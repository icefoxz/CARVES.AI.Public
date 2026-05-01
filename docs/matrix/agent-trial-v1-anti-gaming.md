# CARVES Agent Trial V1 Anti-Gaming

Status: Phase 8 anti-gaming gate freeze for the proposed Agent Trial V1 product line.

This document defines the minimum V1 abuse-resistance and leaderboard hygiene controls. It does not implement authentication, rate limiting, storage, hosted reruns, identity verification, certification, semantic correctness proof, or operating-system sandboxing.

## Goal

V1 anti-gaming should block low-cost replay, duplicate submission, leaderboard spam, and one-time high-score domination without pretending to prove that a local environment cannot be manipulated.

It answers:

```text
Which controls must exist before public leaderboards can use accepted Agent Trial results?
```

It does not answer:

```text
Can a determined local attacker never fake a self-consistent bundle?
Is the model identity verified?
Is the generated code semantically correct?
Is the result certified safe?
```

## Minimum Controls

V1 requires these controls before public ranking:

| Control | Required | Purpose |
| --- | --- | --- |
| Login required for submit | yes | Bind submissions to a platform user |
| Terms/privacy acceptance | yes | Make upload and non-claim boundaries explicit |
| Server-issued challenge id | yes | Prevent anonymous reusable public bundles |
| Challenge expiry | yes | Limit stale challenge reuse |
| AgentProfile snapshot binding | yes | Preserve self-reported profile at challenge time |
| One accepted public result per challenge | yes | Prevent farming one challenge |
| Duplicate hash detection | yes | Flag replayed results and reused bundles |
| Matrix server-side verification | yes | Recheck uploaded summary artifacts |
| Receipt chain | yes | Bind accepted result history |
| Per-user submit limits | yes | Reduce spam and brute-force leaderboard attempts |
| Minimum verified run count | yes | Keep single-run luck off main leaderboards |
| Median or rolling ranking | yes | Avoid best-score ranking |

These controls are necessary but not sufficient for strong identity or provenance claims.

## Eligibility Gate

A result can enter a main public leaderboard only if all are true:

- `result_mode=accepted`;
- `visibility=public`;
- server receipt exists;
- challenge source is `server_issued`;
- challenge is not expired at submission time;
- user owns the challenge;
- AgentProfile snapshot matches the issued challenge;
- Matrix verify or equivalent server-side validation passed;
- trial artifacts are manifest-covered;
- required summary artifact hashes match;
- privacy flags are summary-only;
- result is not duplicate/replay excluded;
- leaderboard minimum verified run count is satisfied where required.

If any item fails, the result can remain in private history but must not rank on the main public leaderboard.

## Replay And Duplicate Rules

The server should flag or reject:

- same `trial_result_sha256` submitted more than once;
- same Matrix manifest hash submitted under different challenges;
- same Matrix proof summary hash reused across incompatible challenges;
- result hash submitted by a different user;
- result for an expired challenge;
- second accepted public result for one challenge;
- mismatched previous receipt in a continuity chain;
- summary artifacts whose hashes do not match the submitted TrialResult.

Recommended labels:

- `duplicate_result_hash`;
- `replayed_manifest_hash`;
- `challenge_expired`;
- `challenge_already_public_accepted`;
- `previous_receipt_mismatch`;
- `artifact_hash_mismatch`;
- `server_verify_failed`.

Labels should describe evidence posture without making accusations about intent.

## One-Time High Score Handling

The main leaderboard must not rank by historical best score.

Recommended approach:

- rank by median or rolling verified posture;
- show verified run count;
- label low-sample rows as `experimental_sample`;
- compute `initial_spike_rate`;
- prefer current or rolling performance over best-ever performance;
- display result trends when continuity receipts exist.

Example:

```text
Day 1: G9.H9.A9 = 100
Day 2: G4.H2.A1 = 30
Day 3: G4.H2.A1 = 30
Trend: initial_spike
Version scope: same prompt/pack/task/scoring profile
```

This is a reproducibility signal. It is not a cheating accusation. If prompt, pack, task, scoring profile, collector, or verifier posture changes between rows, the display must be treated as trend-only unless a future server normalization process explicitly marks the versions comparable.

## Rate Limits

V1 should support basic submit limits:

- per-user challenge issue limit;
- per-user submit limit;
- per-challenge submit attempt limit;
- public accepted result limit per challenge;
- burst limit for failed submissions;
- abuse-review hold for repeated duplicate/replay flags.

Exact thresholds are product operations settings and are not frozen here. The launch gate must confirm they exist before public leaderboards are enabled.

## Public Ranking Exclusion

Suspicious results can be excluded from public ranking without deleting private history.

Allowed actions:

- keep result private to the user;
- mark result as rejected;
- mark result as accepted but not public-rankable;
- hide public row while preserving receipt history;
- attach duplicate/replay reason codes;
- disable a user's public profile for abuse review.

Forbidden actions:

- silently rewriting receipts;
- changing accepted hashes after the fact;
- making private data public as punishment;
- claiming model identity fraud when the system only detected a replay or inconsistency.

## Stronger Future Controls

V1 does not require these stronger controls:

- hosted rerun;
- official remote runner;
- signed local runner provenance;
- hardware attestation;
- transparency log;
- verified model identity;
- hidden task probes;
- source upload;
- raw diff upload.

They can be future phases. V1 must not imply they already exist.

## Phase 8 Acceptance Mapping

TRIAL-V1-080 is satisfied by this document:

- replayed results are flagged or rejected;
- one-time high scores do not dominate main leaderboards;
- suspicious results can be excluded from public ranking without deleting private user history.
