# Agent Trial Node Public Entry Decision

Status: hold default-public promotion.

Date: 2026-04-20

Decision:

```text
Keep the Node-based Windows playable Agent Trial as an official local testing
candidate. Do not promote it to the default public local trial entry yet.
```

This decision follows the Phase 1 user-path smoke and the Phase 2 friction
report:

- `docs/matrix/agent-trial-node-phase-2-friction-report.md`
- `docs/matrix/agent-trial-windows-playable-handoff.md`

## Plain-Language Outcome

The Node starter pack direction is accepted.

The current Windows playable package is good enough for continued local testing
and operator review. It is not clean enough to become the first/default public
entry for new users.

The reason is not the Node migration itself. The reason is user friction in the
Windows playable flow:

- repeated `SCORE.cmd` after a successful score has a broken branch;
- missing Node.js is not explained plainly as a setup dependency problem;
- one developer-oriented verify command can imply a global `carves` command is
  needed even though the package-local scorer is present.

## Current Product Posture

| Question | Decision |
| --- | --- |
| Is the Node starter-pack direction worth continuing? | yes |
| Is the Node Windows playable path usable for local testing? | yes |
| Should it replace the current default public local entry now? | no |
| Should README / quickstart promote it as the default first step now? | no |
| Should release or default-entry docs change before P1 fixes? | no |
| Should work continue through narrow follow-up fixes? | yes |

## Approved Next Scope

Only narrow follow-up work should proceed before default-public promotion.

### P1: Fix Repeat Score

Target behavior:

```text
After a successful score, running SCORE.cmd again shows the previous result and
exits 0 without batch-label errors or contradictory missing-result text.
```

Acceptance:

- first `SCORE.cmd` returns verified local score;
- second `SCORE.cmd` prints the previous result;
- second `SCORE.cmd` exits `0`;
- output does not print `The system cannot find the batch label specified`;
- output does not claim the previous result card is missing after printing it;
- regression test covers this exact branch.

### P1: Add Node Dependency Diagnostic

Target behavior:

```text
If Node.js is missing or not on PATH, SCORE.cmd tells the user to install Node.js
or put node.exe on PATH before presenting agent-quality scoring language.
```

Acceptance:

- Windows score path checks or classifies missing `node.exe` before user-facing
  score copy can mislead a beginner;
- output names Node.js directly;
- local-only non-claims remain visible;
- regression test covers missing Node.

### P2: Make Verify Copy Package-Local

Target behavior:

```text
Normal user output points to RESULT.cmd, RESET.cmd, and package-local files
first. Any developer verify command should not imply global carves is required.
```

Acceptance:

- score output does not make a normal user think they must install global
  `carves`;
- developer verification uses package-local `tools\carves\carves.exe` or moves
  to documentation;
- local-only boundaries remain clear.

## Default-Public Promotion Gate

The Node Windows playable path may be reconsidered as the default public local
trial entry only after:

1. repeat-score branch is fixed and tested;
2. missing Node.js diagnostic is clear and tested;
3. package-local verify/readback copy no longer suggests a global dependency;
4. fresh extraction smoke still passes;
5. score, result, reset, and repeated score all pass from the package root;
6. docs preserve non-claims: no certification, no leaderboard, no hosted
   verification, no anti-cheat, no tamper-proof local execution.

## Non-Decisions

This decision does not:

- publish the Windows playable package;
- make Node the default public entry;
- claim Linux or macOS playable support;
- regenerate release artifacts;
- replace existing `carves test demo` / `carves test agent` source-checkout
  flows;
- authorize direct `.ai` truth edits;
- legalize any previous unauthorized prototype diff.

## Next Phase

Phase 4 should not update public/default docs yet.

The practical next phase is narrow implementation:

```text
fix repeat-score path
fix missing-Node diagnostic
then rerun Phase 1 and Phase 2 checks
```

Only after those pass should a later decision promote the Node Windows playable
path as the default public local trial entry.
