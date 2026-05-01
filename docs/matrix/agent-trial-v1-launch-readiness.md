# CARVES Agent Trial V1 Launch Readiness

Status: Phase 8 launch readiness gate freeze for the proposed Agent Trial V1 product line.

This document defines the minimum V1 release checklist. It does not implement pack distribution, local runner commands, server APIs, receipts, leaderboards, anti-gaming controls, legal terms, certification, semantic correctness proof, model identity verification, or operating-system sandboxing.

## Goal

The launch readiness gate prevents Agent Trial V1 from going public with only partial planning or unverifiable claims.

It answers:

```text
What must work end to end before official Agent Trial V1 can launch?
```

## Launch Decision States

Recommended launch states:

- `not_ready`: blockers remain;
- `internal_alpha`: internal end-to-end path works with test users only;
- `private_beta`: invited registered users can submit private/unlisted results;
- `public_beta`: public leaderboards are visible with beta labeling;
- `v1_ready`: public V1 launch criteria are satisfied.

Do not use `v1_ready` until every required gate below passes.

## Required End-To-End Flow

V1 launch requires this flow:

```text
register user
create AgentProfile
request server-issued challenge
fetch/init official pack
run local agent on official task
collect local summary evidence
run Matrix proof/verify with trial artifact coverage
submit summary-only result
server validates challenge/result/artifacts
server issues receipt
accepted public results render in leaderboards
public pages preserve non-claims
```

Each step must have a deterministic success path and a structured failure path.

## Pack And Local Runner Gate

Required:

- official pack can be fetched or downloaded;
- `carves-trial init` or equivalent can materialize the workspace;
- `AGENTS.md` and `CLAUDE.md` are present;
- five official tasks are present;
- `.carves/trial/pack.json` is present;
- `.carves/trial/challenge.json` is present for server-issued challenges;
- `.carves/trial/task-contract.json` uses `matrix-agent-task.v0`;
- workspace contains no secrets, credentials, customer payloads, or private package dependencies;
- local execution does not require network by default.

## Local Evidence Gate

Required:

- `agent-report.v0` can be produced or missing status is recorded;
- `diff-scope-summary.v0` can be produced without raw diff upload;
- `test-evidence.v0` can be produced without full log upload;
- Guard, Handoff, Audit, Shield, and Matrix artifacts are produced for the trial flow;
- `carves-agent-trial-result.v0` can be collected;
- missing required artifacts fail closed;
- all required artifact hashes are recorded.

## Matrix Verification Gate

Required:

- trial artifacts are Matrix manifest-covered when trial mode is enabled;
- Matrix verify detects missing trial artifacts;
- Matrix verify detects modified trial artifacts;
- Matrix verify preserves existing non-trial bundle compatibility;
- server-side validation reruns Matrix verify or equivalent verified-byte checks;
- Shield evaluation remains bound to Shield evidence hash;
- summary-only privacy flags are enforced.

## API And Receipt Gate

Required:

- registered users can request server-issued challenges;
- challenge includes exact pack, task, and prompt versions;
- challenge expiry is enforced;
- one challenge can produce at most one accepted public result;
- submit API validates user, challenge, expiry, result schema, versions, artifacts, hashes, Matrix posture, and visibility;
- accepted submissions receive `agent-trial-receipt.v0`;
- duplicate or replayed hashes are flagged;
- old receipts cannot be rewritten by visibility changes.

## Leaderboard Gate

Required:

- prompt version leaderboard renders from accepted public results;
- agent/profile leaderboard renders from accepted public results;
- task difficulty leaderboard renders from accepted public results;
- private and unlisted results are excluded from public leaderboards;
- self-reported identity is labeled;
- prompt version and task pack version are displayed;
- incompatible versions are not silently mixed;
- main ranking uses median or rolling verified posture, not historical best;
- minimum verified run count is enforced for main profile leaderboard rows;
- duplicate/replay excluded results do not affect public ranking.

## Privacy And Public Copy Gate

Required:

- terms/privacy acceptance is required before submit;
- upload defaults are summary-only;
- source, raw diff, prompt response, model response, secrets, credentials, and customer payloads are non-default;
- public pages state results are not certification;
- public pages state model/agent identity is self-reported unless a future verification layer exists;
- public pages state Matrix verification is artifact-consistency verification, not semantic correctness proof;
- local absolute paths are not displayed in public results.

## Operational Gate

Before public beta or V1 ready, product operations must define:

- rate limits for challenge issue and submit;
- abuse-review workflow for duplicate/replay flags;
- visibility-change workflow;
- receipt key management or explicit signed-receipt rollout plan;
- data retention and deletion posture;
- support workflow for failed submissions;
- monitoring for submit failure families;
- rollback plan for disabling public leaderboards without deleting private history.

## Launch Blockers

Any of these block public V1 launch:

- no server-issued challenge path;
- no summary-only submit validation;
- no receipt issuance;
- no Matrix server-side verification or equivalent;
- trial artifacts not covered by Matrix manifest;
- public leaderboard ranks by best score only;
- public pages imply certification or verified model identity;
- source/raw diff/prompt/model transcripts are required by default;
- duplicate/replay flags do not exist;
- private/unlisted visibility leaks into public leaderboards;
- terms/privacy acceptance missing before submit.

## Phase 8 Acceptance Mapping

TRIAL-V1-082 is satisfied by this document:

- official pack fetch/init readiness is listed;
- five official tasks local execution is included;
- trial result collect and submit readiness is listed;
- server receipt issuance is required for valid results;
- Matrix verification is required for accepted submissions;
- prompt, agent/profile, and task leaderboards must render from accepted public results;
- public docs must preserve non-claims.
