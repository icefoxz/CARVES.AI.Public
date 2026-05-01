# CARVES Agent Trial V1 Boundary

Status: Phase 0 boundary freeze for the proposed Agent Trial V1 product line.

This document freezes the V1 public promise, terminology, leaderboard boundary, and non-claims before implementation starts. It does not implement registration, trial pack download, result submission, receipts, leaderboards, hosted verification, public certification, model safety benchmarking, semantic correctness proof, or operating-system sandboxing.

## V1 Public Promise

CARVES Agent Trial V1 is an AI-assisted development safety posture trial.

The V1 promise is:

```text
A registered user can fetch an official CARVES trial pack, run their own AI agent in a standard local test workspace, submit summary-only trial results, receive a server receipt for accepted results, and appear in non-certification leaderboards when eligible.
```

The result answers a bounded question:

```text
Under this official task pack, prompt version, permission profile, and local evidence bundle, how well did this AI-assisted development workflow stay reviewable, recorded, explainable, truthfully reported, constrained, and reproducible?
```

Authority and leaderboard eligibility are frozen separately in `docs/matrix/agent-trial-v1-authority-boundary.md`.

It does not answer:

```text
Is this AI model safe in general?
Is this agent identity verified?
Is the generated source code semantically correct?
Is this project certified safe?
```

## Qualified Product Language

Use these phrases:

- AI-assisted development safety posture trial;
- official CARVES Agent Trial pack;
- summary-only trial result;
- non-certification leaderboard;
- self-reported agent profile;
- Matrix-verified local evidence bundle;
- server receipt for accepted summary evidence;
- continuous reproducibility signal.

Avoid these phrases:

- certified safe;
- verified model identity;
- AI model safety benchmark;
- public certification;
- semantic correctness proof;
- tamper-proof proof;
- agent obedience certification;
- production compliance rating.

## V1 Result Meaning

A V1 accepted result means:

- the result was submitted by a registered user;
- the result was bound to a server-issued challenge id;
- the result declared an official suite, task pack version, prompt version, and AgentProfile snapshot;
- the submitted Matrix-related hashes matched the accepted summary artifacts;
- the server issued a receipt for that exact accepted summary result;
- leaderboard inclusion rules accepted the result if it is public.

A V1 accepted result does not mean:

- the user's agent/model identity was independently verified;
- the user could not have used a modified local toolchain;
- the task result is semantically correct;
- the result is a certification;
- the result applies to arbitrary user projects;
- the result proves future behavior.

## Authority Mode Boundary

V1 uses three authority modes:

- `local_only`: local dry-run evidence only; useful for diagnosis, never official leaderboard eligible;
- `server_issued`: bound to a server-issued challenge, but not automatically leaderboard eligible;
- `leaderboard_eligible`: accepted public server-issued result with receipt and current eligibility gates satisfied.

Matrix verification status is not an authority mode. A local bundle can be Matrix-verified and still remain `local_only`.

Workspace-local `.carves/trial/pack.json`, `.carves/trial/challenge.json`, and `.carves/trial/task-contract.json` are candidate evidence. For non-local flows, the authority for pack, challenge, and task contract identity must come from the server-issued challenge or another external immutable authority, not from mutable workspace files.

## Official And Community Boundary

V1 has only official trial packs.

Official leaderboards use only:

- official suite id;
- official trial pack id;
- official task pack version;
- official prompt id and prompt version;
- accepted public results.

Community trial packs are reserved for a later product phase. Future community pack results must be displayed separately from official leaderboards unless a pack is promoted through a documented official adoption process.

Leaderboard categories in V1:

- prompt version leaderboard;
- self-reported agent/profile leaderboard;
- task difficulty leaderboard.

Each leaderboard must display the prompt version and task pack version. Results from incompatible versions must not be silently mixed.

## Identity Boundary

V1 registration establishes a platform user account for result ownership, visibility control, abuse limits, and receipt history.

V1 registration does not establish:

- real-world legal identity;
- model vendor identity;
- model binary identity;
- agent binary identity;
- enterprise compliance identity.

AgentProfile fields are self-reported unless a later verification layer says otherwise. Public UI must label self-reported fields plainly.

## Upload Boundary

V1 default upload posture is summary-only.

Allowed default upload categories:

- trial ids and versions;
- AgentProfile self-report snapshot;
- operating system/runtime summary;
- standard label and Lite score;
- safety posture projection;
- Matrix verify summary;
- artifact hashes;
- diff scope summary;
- test evidence summary;
- agent report summary;
- receipt chain metadata.

Forbidden by default:

- private source files;
- raw diff text;
- prompt response transcript;
- model response transcript;
- secrets;
- credentials;
- customer payloads;
- private file payloads.

Any future rich-evidence mode must be opt-in, separately labeled, and excluded from the V1 default public promise.

## Visibility Boundary

V1 result visibility has three levels:

- `private`: visible to the submitting user only;
- `unlisted`: visible by direct link, not eligible for public leaderboards;
- `public`: eligible for public leaderboards after verification and anti-gaming gates.

Changing visibility must not rewrite receipt history. A private result can be hidden from public surfaces, but its receipt must remain bound to the original submitted hashes.

## Ranking Boundary

V1 leaderboards should rank by stable verified behavior, not historical best score.

Default ranking posture:

- use median or rolling verified score;
- require a minimum verified run count for main leaderboards;
- label low-sample results as experimental;
- show verified run count;
- show duplicate or replay flags when relevant;
- show self-reported identity labels.

One-time high scores should not dominate official leaderboards.

## Phase 0 Acceptance Mapping

TRIAL-V1-001 is satisfied by this document:

- V1 is described as an AI-assisted development safety posture trial.
- V1 explicitly rejects certification, model safety benchmarking, semantic correctness proof, and verified model identity claims.
- Official trial leaderboards are distinguished from future community pack leaderboards.

TRIAL-V1-002 is satisfied together with `docs/matrix/agent-trial-v1-object-model.md`.
