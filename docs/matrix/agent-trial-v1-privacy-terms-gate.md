# CARVES Agent Trial V1 Privacy And Terms Gate

Status: Phase 8 privacy and terms gate freeze for the proposed Agent Trial V1 product line.

This document defines the minimum privacy, upload, and public-copy gates required before Agent Trial V1 public launch. It does not implement legal terms, consent storage, deletion workflows, billing, hosted verification, certification, model identity verification, semantic correctness proof, or operating-system sandboxing.

## Goal

The privacy and terms gate makes the default upload boundary explicit before users submit results.

It answers:

```text
What can V1 collect by default, what is forbidden by default, and what must public pages say before launch?
```

## Default Upload Posture

V1 default submission is summary-only.

Allowed default upload:

- account and profile ids needed for ownership;
- self-reported AgentProfile snapshot;
- challenge id and challenge metadata;
- suite, pack, task, and prompt versions;
- `carves-agent-trial-result.v0`;
- `matrix-artifact-manifest.v0`;
- `matrix-proof-summary.v0`;
- `matrix-verify.v0`;
- task contract metadata;
- agent report summary;
- diff scope summary;
- test evidence summary;
- Shield evidence and evaluation summary artifacts;
- artifact hashes;
- bounded reason codes and issue codes;
- receipt metadata.

Forbidden default upload:

- private source files;
- raw diff text;
- prompt response transcript;
- model response transcript;
- full stdout/stderr logs;
- secrets;
- credentials;
- customer payloads;
- private file payloads;
- local absolute filesystem paths in public output.

## Non-Default Evidence

Future rich-evidence modes must be opt-in.

Any mode that uploads source, raw diffs, prompt/model transcripts, or full logs must be:

- separately labeled;
- disabled by default;
- excluded from V1 default leaderboard posture unless product policy explicitly changes;
- preceded by clear user consent;
- reviewed for secret and credential handling.

V1 public launch does not require rich-evidence mode.

## Terms Acceptance

Submitting a result requires current terms and privacy acceptance.

The acceptance copy must state:

- uploads are summary-only by default;
- public results can appear on non-certification leaderboards;
- private and unlisted results are not public leaderboard entries;
- agent/model identity is self-reported unless a future verification layer says otherwise;
- results do not certify safety, semantic correctness, model identity, or production compliance;
- users must not upload secrets, credentials, private source, raw diffs, prompt transcripts, model response transcripts, customer payloads, or private file payloads in V1 default submissions;
- receipt history binds accepted hashes and may remain for anti-replay and audit purposes.

The submit API must reject submissions when the user has not accepted the current required terms/privacy version.

## Visibility Copy

V1 supports:

- `private`;
- `unlisted`;
- `public`.

Required user-facing meaning:

- `private`: visible to the submitting user only;
- `unlisted`: visible by direct link when supported, not public leaderboard eligible;
- `public`: eligible for public leaderboards after validation and anti-gaming gates.

Changing visibility must not rewrite receipt history.

## Public Non-Claims

Every public page for V1 must preserve these statements:

- results are not certification;
- leaderboards are non-certification analytics;
- agent/model labels are self-reported unless explicitly verified by a later layer;
- Matrix verification checks submitted summary artifact consistency, not semantic correctness;
- a local user may still manipulate a local environment;
- receipt chains are tamper-evident submission history, not tamper-proof execution proof.

Avoid these phrases:

- certified safe;
- verified model identity;
- AI model safety benchmark;
- production compliance rating;
- semantic correctness proof;
- tamper-proof proof.

## Data Handling Minimums

Before public launch, product implementation should define:

- where summary artifacts are stored;
- how private and unlisted visibility is enforced;
- how public profile visibility affects leaderboard display;
- how email is kept private;
- how deletion or privacy requests interact with immutable receipts;
- how abuse flags affect public ranking without exposing private data;
- how logs and rejected submissions are retained.

This document does not freeze a legal retention policy. It freezes the product gate that such a policy must exist before launch.

## Phase 8 Acceptance Mapping

TRIAL-V1-081 is satisfied by this document:

- upload defaults are summary-only;
- source, raw diff, prompt response, model response, secrets, and credentials are non-default;
- users must accept terms/privacy before submit;
- public pages must state results are not certification or verified model identity.
