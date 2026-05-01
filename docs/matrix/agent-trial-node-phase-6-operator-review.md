# Agent Trial Node Phase 6 Operator Review

Status: accepted for operator review.

Date: 2026-04-20

Reviewed artifact:

- Path: `artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip`
- SHA256: `f170162eca839625847f0581a27fa20575a51a9147e69250d78545a8e84acb90`
- Size: `51234914` bytes
- Build label: `node-starter-phase5-20260420`

Related records:

- `docs/matrix/agent-trial-node-phase-4-acceptance.md`
- `docs/matrix/agent-trial-node-phase-5-release-artifact.md`
- `docs/matrix/agent-trial-windows-playable-handoff.md`

This review checks whether a maintainer/operator can take the Phase 5 zip and
follow the intended local trial shape without relying on CARVES source-checkout
execution, global `carves`, or `dotnet build`.

It is not an external user study, not public hosting, not certification, not a
leaderboard result, not hosted verification, not package signing, not
anti-cheat, and not proof of tamper-proof local execution.

## Decision

Phase 6 is accepted.

The Node Windows playable package remains a parallel official local trial
entry. It is ready for Phase 7 public onboarding copy work and Phase 8 small
user trial planning.

It should not yet be promoted to the default public local trial entry.

## Operator Review Checklist

### Package Discovery

Expected:

- operator can identify the zip path;
- SHA256 is available in docs;
- artifact is described as Windows `win-x64`;
- artifact is described as local-only.

Result: pass.

Evidence:

```text
artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
SHA256: f170162eca839625847f0581a27fa20575a51a9147e69250d78545a8e84acb90
```

### Extracted Package Shape

Expected package paths:

- `README-FIRST.md`;
- `COPY_THIS_TO_AGENT_BLIND.txt`;
- `COPY_THIS_TO_AGENT_GUIDED.txt`;
- `SCORE.cmd`;
- `RESULT.cmd`;
- `RESET.cmd`;
- `agent-workspace/AGENTS.md`;
- `agent-workspace/README.md`;
- `tools/carves/carves.exe`;
- `tools/carves/scorer-manifest.json`.

Result: pass.

### Human Instructions

Expected:

- package instructions tell the user to open only `agent-workspace/`;
- package instructions point to `SCORE.cmd`;
- package instructions preserve local-only non-claims;
- blind prompt tells the tested agent to use only `agent-workspace/`;
- workspace instructions identify the folder as a local Agent Trial starter
  pack.

Result: pass.

### User Path

Executed path:

```text
extract zip
read package instructions
inspect blind prompt
open/edit only agent-workspace
node tests/bounded-fixture.test.js
SCORE.cmd
SCORE.cmd again
RESULT.cmd
RESET.cmd
```

Result:

```text
Phase 6 operator-review path passed.
Package instructions: readable
Workspace boundary: agent-workspace only
Node test: bounded-fixture tests passed
Score: 100/100 (scored)
Repeat score: previous result readback
Result: 100/100 readback
Reset: ready_for_agent
```

### Dependency Posture

Observed:

- package-local scorer is present under `tools/carves/carves.exe`;
- normal scoring did not require global `carves`;
- task command uses Node.js as expected for this starter pack;
- `SCORE.cmd` output includes package-local developer verify copy.

Result: pass.

### Reset And Reuse

Expected:

- repeated `SCORE.cmd` after success reads the previous result;
- `RESULT.cmd` reads the previous result;
- `RESET.cmd` returns the package to `ready_for_agent`.

Result: pass.

## Findings

No Phase 6 blocker was found.

Minor residual risks:

- the review was performed by the operator/maintainer path, not by an external
  first-time user;
- public onboarding copy still needs to be shortened for new users;
- Linux/macOS playable packages remain out of scope;
- default-entry promotion still needs a separate product decision.

## Go / No-Go

Recommendation:

```text
GO for Phase 7 public onboarding copy.
GO for Phase 8 small user trial planning.
NO-GO for default public local trial promotion until Phase 7 and Phase 8
evidence exists.
```

Rationale:

- The Phase 5 artifact can be found, extracted, scored, read back, repeated,
  and reset through the intended Windows local path.
- The package-local scorer boundary is visible and functional.
- The user-facing local-only non-claims remain present.
- The remaining question is onboarding clarity with real first-time users, not
  package execution capability.

## Phase 7 Entry Packet

Recommended next card:

```text
CARD-NODE-STARTER-PHASE-7
Draft public onboarding copy for Node Windows playable trial
```

Recommended scope:

- write a short beginner-facing section that does not require CARVES internal
  vocabulary;
- explain what the zip is;
- explain prerequisites: Windows, Git, Node.js, zip extraction;
- explain the first run in 5 to 7 steps;
- preserve local-only non-claims;
- point advanced users to the existing source-checkout quickstart;
- keep default-entry promotion out of scope.

Recommended acceptance:

- a new user can identify which folder to open in the agent;
- a new user can identify which command to run after the agent finishes;
- docs say what `SCORE.cmd`, `RESULT.cmd`, and `RESET.cmd` do;
- docs do not imply certification, hosted verification, leaderboard entry,
  anti-cheat, signing, or tamper-proof execution;
- docs still describe this as a parallel official local trial entry.

## Phase 8 Entry Packet

Recommended next card after Phase 7:

```text
CARD-NODE-STARTER-PHASE-8
Run small user trial for Node Windows playable onboarding
```

Recommended sample:

- 2 to 3 users or near-user reviewers;
- at least one Node/Web developer;
- at least one AI coding agent user who has not read CARVES internals.

Recommended observations:

- did they find the zip;
- did they open only `agent-workspace/`;
- did they understand blind vs guided prompt;
- did the agent write `artifacts/agent-report.json`;
- did `SCORE.cmd`, `RESULT.cmd`, and `RESET.cmd` make sense;
- where they got stuck;
- whether they understood that the result is local-only.

Default-entry promotion should wait for this evidence.
