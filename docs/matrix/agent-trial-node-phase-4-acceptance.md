# Agent Trial Node Phase 4 Acceptance

Status: accepted for Phase 4.

Date: 2026-04-20

Related commit:

- `1e8fa8c5 Implement Node starter pack trial UX fixes`

This document closes the narrow Phase 4 follow-up from:

- `docs/matrix/agent-trial-node-phase-2-friction-report.md`
- `docs/matrix/agent-trial-node-public-entry-decision.md`

It does not publish a release artifact, make Node the default public entry,
claim hosted verification, claim certification, claim leaderboard eligibility,
or replace the existing local trial entry.

## Decision

Phase 4 is accepted as a CLI and trial UX integration fix.

The Node Windows playable path is ready to enter Phase 5 as a parallel official
local trial candidate. It is not yet the default public local trial entry.

## Fixed Phase 2 Blockers

### Repeat SCORE

Previous behavior:

```text
Running SCORE.cmd after a successful score printed the previous result, then
fell through into a batch-label error and exited 1.
```

Accepted behavior:

```text
Running SCORE.cmd after a successful score shows the previous local result,
tells the user to run RESET.cmd before another agent, and exits 0.
```

Coverage:

- Windows `SCORE.cmd` clean smoke runs first score, repeat score, and checks
  that no stale batch-label or missing-result text appears.
- POSIX package smoke covers repeated `score.sh` readback.

### Missing Node.js

Previous behavior:

```text
Missing Node.js could look like a collected trial failure with
required_command_failed.
```

Accepted behavior:

```text
SCORE.cmd and score.sh detect missing Node.js before collection and tell the
user to install Node.js or put node/node.exe on PATH.
```

Coverage:

- Windows smoke runs a missing-Node path and verifies dependency copy.
- POSIX package smoke runs a missing-Node path and verifies it is not reported
  as `Result: collection_failed`.

### Package-Local Verify Copy

Previous behavior:

```text
The score output could imply that the user needs a global carves command.
```

Accepted behavior:

```text
When a package-local scorer exists, developer verify copy points at
tools/carves/carves.exe on Windows or ./tools/carves/carves on POSIX.
```

Coverage:

- Clean playable package smoke checks the package-local POSIX verify command.
- Windows playable scorer manifest now declares `test verify`.
- Scorer-root validation rejects roots that do not support `test verify`.

### Windows Command Launcher Stability

Additional Phase 4 fix:

```text
Generated .cmd launchers are written with CRLF line endings so Windows cmd.exe
can resolve labels reliably after cross-platform package generation.
```

Coverage:

- Launcher diagnostics test checks CRLF around `:run_carves` and `:done`.
- Windows `SCORE.cmd` smoke passed against a freshly generated win-x64 package.

## Verification Evidence

### Targeted Matrix Tests

Command:

```text
dotnet test tests/Carves.Matrix.Tests/Carves.Matrix.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~MatrixPortableScoreLauncherDiagnosticsTests|FullyQualifiedName~MatrixTrialCleanPlayablePackageSmokeTests|FullyQualifiedName~MatrixPortableScorerBundleContractTests|FullyQualifiedName~MatrixTrialWindowsPlayablePackageAssemblerTests"
```

Result:

```text
Passed: 14
Failed: 0
Skipped: 0
```

### Fresh Windows Playable Build

Command:

```text
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/matrix/build-windows-playable-package.ps1 -OutputRoot /tmp/carves-phase4-node-fix-release -Configuration Release -BuildLabel phase4-node-fix -Force
```

Result:

```text
Windows playable package root: /tmp/carves-phase4-node-fix-release/package/carves-agent-trial-pack-win-x64
Windows playable zip: /tmp/carves-phase4-node-fix-release/carves-agent-trial-pack-win-x64.zip
Package-local scorer: /tmp/carves-phase4-node-fix-release/package/carves-agent-trial-pack-win-x64/tools/carves/carves.exe
```

The generated zip was validation evidence only. It is not the official release
artifact.

### Windows SCORE.cmd Clean Smoke

Input:

```text
/tmp/carves-phase4-node-fix-release/carves-agent-trial-pack-win-x64.zip
```

Result:

```text
Windows SCORE.cmd clean smoke passed.
```

Covered paths:

- success from fresh extraction;
- repeated `SCORE.cmd` after success;
- missing package-local scorer;
- missing Node.js.

### Windows Path Smoke

Input:

```text
/tmp/carves-phase4-node-fix-release/carves-agent-trial-pack-win-x64.zip
```

Result:

```text
Windows SCORE.cmd path smoke passed.
```

Covered path shape:

```text
C:\Users\icefo\AppData\Local\Temp\CARVES Phase4 path smoke with spaces and non-ASCII text
```

The local path smoke summary stayed in the temporary Windows work root and is
not a product artifact.

### Diff Hygiene

Command:

```text
git diff --check
```

Result:

```text
exit code 0
```

PowerShell files may still show repository line-ending normalization warnings
when touched by Git. No whitespace errors remained.

## Phase 4 Acceptance

Phase 4 is accepted with these boundaries:

- accepted: Node Windows playable CLI and trial UX fixes;
- accepted: package-local verify copy for playable package output;
- accepted: scorer manifest support for `test verify`;
- accepted: smoke coverage for repeat score, missing Node, missing scorer, and
  path handling;
- not accepted as Phase 4 scope: official release zip publication;
- not accepted as Phase 4 scope: default public-entry promotion;
- not accepted as Phase 4 scope: Linux/macOS playable product commitment;
- not accepted as Phase 4 scope: hosted verification, certification,
  leaderboard, signing, anti-cheat, or tamper-proof local execution claims.

## Phase 5 Entry Packet

Phase 5 can start.

Recommended card:

```text
CARD-NODE-STARTER-PHASE-5
Publish Node starter-pack trial docs and release artifact
```

Recommended Phase 5 scope:

- regenerate the official Windows playable release zip from the accepted code;
- record release artifact path, size, and SHA256;
- run fresh extraction user smoke from that exact zip;
- run Windows `SCORE.cmd` clean smoke and path smoke from that exact zip;
- update handoff, quickstart, and release docs to present Node as a parallel
  official local trial entry;
- keep old/default local trial entry intact unless a separate promotion
  decision is approved;
- preserve local-only non-claims in all user-facing docs.

Phase 5 must not treat the Phase 4 `/tmp` validation zip as the release
artifact.

## Phase 5 Entry Criteria

Before public/default promotion, Phase 5 must prove:

1. the official release zip was generated from accepted source;
2. the zip SHA256 is recorded in docs;
3. score, result, reset, repeat score, missing Node, and missing scorer paths
   pass from a fresh extraction;
4. docs do not imply certification, hosted verification, leaderboard
   eligibility, signing, anti-cheat, or tamper-proof execution;
5. default-entry promotion remains a separate decision.
