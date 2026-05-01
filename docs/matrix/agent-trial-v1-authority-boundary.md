# CARVES Agent Trial V1 Authority Boundary

Status: CARD-891 authority boundary freeze for Agent Trial V1 hardening.

This document freezes the authority and eligibility language for Agent Trial V1. It does not implement collector pinning, hosted issuance, submit APIs, receipts, leaderboards, signing, model identity verification, semantic correctness proof, certification, or operating-system sandboxing.

## Goal

Agent Trial V1 must keep local evidence, Matrix verification, and leaderboard eligibility separate.

It answers:

```text
Which actor is allowed to define the task authority, which local files are only candidate evidence, and which result modes can ever be considered for official leaderboards?
```

It does not answer:

```text
Did the user-controlled machine run honestly?
Is the agent binary or model identity verified?
Is the result certification-grade?
```

## Authority Modes

V1 names three authority modes:

| Mode | Authority Source | Eligible For Official Leaderboard | Meaning |
| --- | --- | --- | --- |
| `local_only` | Local dry-run pack metadata and local workspace files | no | Useful for local diagnosis, comparison, and bundle verification, but not public ranking. |
| `server_issued` | Server-issued challenge bound to registered user, official pack, prompt version, task version, and expiry | not by itself | Submission can be validated, but visibility, receipt, anti-gaming, and scoring gates still decide leaderboard use. |
| `leaderboard_eligible` | Accepted public server-issued result with receipt and current eligibility gates satisfied | yes | Result may enter non-certification leaderboard aggregation for matching prompt, pack, task, and scoring versions. |

The authority mode is not the same as Matrix verification status. A local bundle can be Matrix verified and still remain `local_only`.

## Root Authority Rules

For non-local flows, task authority must come from outside the mutable test workspace.

Root authority chain:

```text
official suite -> immutable pack version -> issued challenge -> expected task contract hash -> local candidate task contract bytes
```

Rules:

- workspace-local `.carves/trial/pack.json`, `.carves/trial/challenge.json`, and `.carves/trial/task-contract.json` are candidate evidence;
- the local collector must not treat workspace-local task rules as root authority for `server_issued` or `leaderboard_eligible` flows;
- a task contract can define allowed paths, forbidden paths, and required commands only after its bytes match the expected authority hash for the active challenge;
- if the expected authority cannot be established, the result remains `local_only` or fails closed;
- local dry-run packs can define a local-only authority posture through explicit expected task contract hash metadata, but that posture must keep `official_leaderboard_eligible=false`.

CARD-892 implements local dry-run task contract pinning. That implementation improves local tamper detection, but it does not make workspace-owned metadata sufficient for leaderboard eligibility.

## Responsibility Split

| Component | Owns | Must Not Claim |
| --- | --- | --- |
| Matrix verifier | Manifest, artifact bytes, hashes, public schema, summary consistency, and trust-chain readback | That a local machine executed honestly or that a result is leaderboard eligible. |
| Local collector | Local workspace evidence, agent self-report comparison, command evidence summary, local-only result envelope | That the workspace metadata was authoritative for a server-issued challenge unless pinning proves it. |
| Submit API | Registered user ownership, server-issued challenge binding, artifact revalidation, receipts, visibility, and eligibility gates | That source code semantics or model identity are certified. |
| Leaderboard job | Accepted public result aggregation by exact prompt, pack, task, and scoring versions | That one local high score proves general agent safety. |

## Eligibility Rules

`official_leaderboard_eligible=true` requires all of the following:

- authority mode is `leaderboard_eligible`;
- result was accepted through a server-issued challenge;
- server receipt exists for the exact submitted summary artifacts;
- Matrix verification was rerun or equivalently enforced over submitted bytes;
- trial artifacts were manifest-covered and schema-valid;
- prompt version, pack version, task version, scoring profile, collector version, and verifier version are comparable for the target leaderboard;
- visibility is `public`;
- anti-gaming gates do not reject or quarantine the result.

Local collector output must keep `official_leaderboard_eligible=false`.

## Non-Claims

Agent Trial V1 does not claim:

- tamper-proof local execution;
- verified model identity;
- verified agent binary identity;
- semantic correctness proof for generated code;
- certification of a project, model, agent, or user;
- hosted remote sandboxing unless a future product phase explicitly adds it.

## Phase Acceptance Mapping

CARD-891 is satisfied by this document together with updates to the boundary, collector, result, task contract, and submit API docs:

- authority modes are named as `local_only`, `server_issued`, and `leaderboard_eligible`;
- local collector output remains `official_leaderboard_eligible=false`;
- task contract, pack, and challenge authority are external to mutable workspace files for non-local flows;
- Matrix verifier, local collector, submit API, and leaderboard responsibilities are separated.
