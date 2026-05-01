# Agent Trial Node Phase 2 Friction Report

Status: completed with release-impact findings.

Date: 2026-04-20

Package under test:

- `artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip`
- SHA256: `995a3ef340960a0bef6f7f45b4426192c930a9074f3fe9d9dbf1d90c0ea7479c`

This report covers Phase 2 of the Node Agent Trial public-entry decision path:
collect user friction and failure points after the Phase 1 user-path smoke.

It does not claim public release readiness, cross-platform playable support,
certification, hosted verification, leaderboard eligibility, package signing,
anti-cheat, or tamper-proof execution.

## Summary

The main Node Windows playable path is usable, but it should not be promoted to
the default public entry until two friction items are addressed.

Highest-impact findings:

1. Re-running `SCORE.cmd` after a successful score exits `1` and prints a batch
   label error even though it also shows the previous result.
2. Missing Node.js is not surfaced as a plain dependency problem. The current
   output appears as a scored failed trial with `required_command_failed`.

Non-blocking observations:

1. `RESULT.cmd` before scoring gives a clear next step.
2. `SCORE.cmd` before `agent-report.json` gives a clear next step.
3. `RESET.cmd` before any attempt succeeds and keeps the package ready.
4. Missing Git is surfaced clearly.
5. The success path does not need a global `carves.exe`.

## Checks Run

### Phase 1 Baseline

Result: pass.

The user-path smoke completed from a fresh extraction:

```text
edit agent-workspace
node tests/bounded-fixture.test.js
SCORE.cmd
RESULT.cmd
RESET.cmd
```

Observed result:

```text
Result: verified
Score: 100/100
Verification: verified
```

The package-local scorer at `tools/carves/carves.exe` was used. A global
`carves.exe` was not required.

### Result Before Score

Result: pass.

`RESULT.cmd` exits non-zero and says there is no previous result card, then
points the user to run `SCORE.cmd` after the agent writes
`agent-workspace/artifacts/agent-report.json`.

User impact: acceptable.

### Score Before Agent Report

Result: pass.

`SCORE.cmd` exits non-zero and says:

```text
Missing agent report: agent-workspace\artifacts\agent-report.json
First open only agent-workspace\ in your AI agent.
Paste COPY_THIS_TO_AGENT_BLIND.txt for a strict comparison, or COPY_THIS_TO_AGENT_GUIDED.txt for practice.
```

User impact: acceptable.

### Reset Before Agent Report

Result: pass.

`RESET.cmd` exits `0`, reports `Status: reset`, archives no previous attempt,
and leaves `.carves-pack/state.json` at `ready_for_agent`.

User impact: acceptable.

### Missing Git

Result: pass.

When Windows `PATH` excludes Git, `SCORE.cmd` exits non-zero and says:

```text
Missing dependency: Git is required for baseline and diff evidence.
Install Git, then run this scorer again from the package root.
```

User impact: acceptable.

### Missing Node.js

Result: needs improvement.

When Windows `PATH` excludes Node.js but keeps Git and the package-local scorer,
`SCORE.cmd` exits non-zero, but the visible result is:

```text
Result: collection_failed
Collection reasons: required_command_failed
Score: 30/100
```

The output explains that a required command failed, but it does not plainly tell
the user that Node.js is missing or unavailable on `PATH`.

User impact: medium. A beginner can misread an environment problem as an agent
quality failure.

Recommended fix: add a Node preflight in the Windows score path or map failed
`node tests/bounded-fixture.test.js` process start into a dependency diagnostic
before presenting the score.

### Repeat Score After Success

Result: release blocker for default-public promotion.

After a successful first `SCORE.cmd`, a second `SCORE.cmd` should show the
previous local result and tell the user to run `RESET.cmd` before another agent.

Observed behavior:

```text
Package already scored. Showing the previous local result.
To test another agent in this same folder, run RESET.cmd first.
...
The system cannot find the batch label specified - done
Previous result card was not found at results\local\matrix-agent-trial-result-card.md.
Inspect results\submit-bundle\ or run RESET.cmd before another local run.
```

The command exits `1`.

User impact: high. The repeated-score path contradicts itself, looks broken, and
weakens the "rerun score and view previous result" user promise.

Recommended fix: create a narrow execution card to fix the repeated-score branch
in `SCORE.cmd` and add a regression test that scores once, runs `SCORE.cmd`
again, expects exit `0`, and verifies the previous result card is shown without
fall-through.

### Verify Command Copy

Result: polish issue.

The score output includes:

```text
Verify again: carves test verify <bundle> --trial --json
```

The Windows playable path is designed not to require a global `carves.exe`.
This command can confuse a normal user even though the package-local scorer is
present.

User impact: low to medium.

Recommended fix: prefer a package-local command in Windows playable output, or
hide the developer-oriented verify command behind documentation.

## Release Impact

Recommended decision for the next phase:

```text
hold default-public promotion until repeat-score and missing-Node friction are fixed
```

The package remains suitable for local testing and continued validation. It is
not yet clean enough to make Node Agent Trial the default public local entry.

## Proposed Follow-Up Cards

### P1: Fix Windows Repeat Score Path

Goal:

```text
Running SCORE.cmd after a successful score shows the previous result and exits 0.
```

Acceptance:

- first `SCORE.cmd` returns verified local score;
- second `SCORE.cmd` prints the previous result;
- second `SCORE.cmd` exits `0`;
- output does not print `The system cannot find the batch label specified`;
- output does not claim the previous result card is missing after printing it;
- regression test covers the branch.

### P1: Add Node Dependency Diagnostic

Goal:

```text
Missing Node.js is reported as a dependency/setup problem, not primarily as agent behavior.
```

Acceptance:

- Windows playable score path checks for `node.exe` before collecting;
- missing Node output tells the user to install Node.js or put it on `PATH`;
- the message preserves local-only non-claims;
- regression test covers missing Node.

### P2: Make Verify-Again Output Package-Local

Goal:

```text
Windows playable output should not imply that a global carves command is required.
```

Acceptance:

- normal user output points to `RESULT.cmd`, `RESET.cmd`, and package-local
  files first;
- any developer verify command uses package-local `tools\carves\carves.exe` or
  is moved to docs;
- local-only boundaries remain visible.
