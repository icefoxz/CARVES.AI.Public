# Agent Trial Node Phase 5 Release Artifact

Status: release artifact prepared for parallel official local trial entry.

Date: 2026-04-20

Source code commit used for the artifact build:

- `ca132b88 Document Node starter pack phase 4 acceptance`

Build label:

- `node-starter-phase5-20260420`

Artifact:

- Path: `artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip`
- Size: `51234914` bytes
- SHA256: `f170162eca839625847f0581a27fa20575a51a9147e69250d78545a8e84acb90`
- Platform gate: Windows `win-x64`

The artifact is a local release output under `artifacts/`. That directory is
not tracked by Git in this repository. The durable repository record is this
document plus the handoff and quickstart docs.

## Product Decision

The Node Windows playable zip is accepted as a parallel official local Agent
Trial entry.

It is not the default public local trial entry. Default-entry promotion remains
a separate decision.

## Generated Artifact Command

Command:

```text
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/matrix/build-windows-playable-package.ps1 -OutputRoot artifacts/release/windows-playable -Configuration Release -BuildLabel node-starter-phase5-20260420 -Force
```

Result:

```text
Windows playable package root: <artifact-root>/release/windows-playable/package/carves-agent-trial-pack-win-x64
Windows playable zip: <artifact-root>/release/windows-playable/carves-agent-trial-pack-win-x64.zip
Package-local scorer: <artifact-root>/release/windows-playable/package/carves-agent-trial-pack-win-x64/tools/carves/carves.exe
```

Minimum zip shape was inspected and includes:

- `README-FIRST.md`;
- `COPY_THIS_TO_AGENT_BLIND.txt`;
- `COPY_THIS_TO_AGENT_GUIDED.txt`;
- `SCORE.cmd`;
- `RESULT.cmd`;
- `RESET.cmd`;
- `score.sh`;
- `result.sh`;
- `reset.sh`;
- `.carves-pack/state.json`;
- `agent-workspace/AGENTS.md`;
- `tools/carves/carves.exe`;
- `tools/carves/scorer-manifest.json`.

## Verification Evidence

### Windows SCORE.cmd Clean Smoke

Input:

```text
artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
```

Result:

```text
Windows SCORE.cmd clean smoke passed.
```

Covered paths:

- fresh extraction success path;
- repeated `SCORE.cmd` after success;
- missing package-local scorer;
- missing Node.js dependency diagnostic.

### Windows SCORE.cmd Path Smoke

Input:

```text
artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
```

Result:

```text
Windows SCORE.cmd path smoke passed.
```

Covered path shape:

```text
Windows temporary work root containing spaces and non-ASCII path text.
```

### Fresh Extraction User-Path Smoke

Input:

```text
artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
```

Result:

```text
Phase 5 user-path smoke passed.
Node stdout: bounded-fixture tests passed.
Score excerpt: Score: 100/100 (scored)
Result excerpt: Score: 100/100 (scored)
Reset excerpt: Status: reset
```

Covered user flow:

```text
extract zip -> edit agent-workspace -> node tests/bounded-fixture.test.js -> SCORE.cmd -> RESULT.cmd -> RESET.cmd
```

The smoke was scripted from a fresh extraction to pin the artifact behavior. It
is still local validation evidence, not hosted verification.

## Non-Claims

This artifact does not claim:

- hosted verification;
- server submission;
- server receipt;
- package signing;
- clean-machine certification;
- leaderboard eligibility;
- agent certification;
- producer identity;
- anti-cheat;
- operating-system sandboxing;
- tamper-proof local execution;
- Linux/macOS playable support.

## Phase 5 Acceptance

Phase 5 is accepted for the Windows playable artifact and docs handoff.

Accepted:

- official local artifact path recorded;
- artifact SHA256 recorded;
- artifact size recorded;
- clean `SCORE.cmd` smoke passed from the exact zip;
- path smoke passed from the exact zip;
- user-path score/result/reset smoke passed from the exact zip;
- docs present Node Windows playable as a parallel official local trial entry;
- default-entry promotion remains out of scope.

Not accepted as Phase 5 scope:

- public hosting;
- release signing;
- replacing the existing default source-checkout quickstart;
- Linux/macOS playable release;
- hosted verification or leaderboard workflow.

## Next Decision

The next decision is not technical implementation. It is product positioning:

```text
Should the Node Windows playable zip become the default public local trial entry?
```

That decision should only be made after operator review of the Phase 5 artifact
and docs.
