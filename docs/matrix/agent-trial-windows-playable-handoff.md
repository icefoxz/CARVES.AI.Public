# CARVES Agent Trial Windows Playable Handoff

Status: Phase 5 release artifact prepared for parallel official local trial entry.

This package is for trying the Agent Trial on one Windows computer. It checks the extracted folder locally, writes a local score, and lets the user view or reset the local attempt.

It does not upload anything. It does not create a server receipt, leaderboard entry, certification, hosted verification, package signature, clean-machine proof, OS sandbox, anti-cheat result, or tamper-proof result.

## Current Package

- Zip: `artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip`
- Size: `51234914` bytes
- SHA256: `f170162eca839625847f0581a27fa20575a51a9147e69250d78545a8e84acb90`
- Build label: `node-starter-phase5-20260420`
- Platform gate: Windows `win-x64`
- Status: exact-zip smoke passed

The zip includes the package-local scorer at:

```text
tools/carves/carves.exe
```

So a normal user should not need a global `carves` command, a CARVES source checkout, or `dotnet build` to run the Windows playable path.

This is a parallel official local trial entry. It does not replace the existing
source-checkout `carves test demo` / `carves test agent` path, and it is not yet
the default public local trial entry.

## User Flow

1. Extract the zip to a normal Windows folder.
2. Open only `agent-workspace/` in the AI agent being tested.
3. Paste `COPY_THIS_TO_AGENT_BLIND.txt` for a strict comparison, or `COPY_THIS_TO_AGENT_GUIDED.txt` for practice.
4. Let the agent finish the task and write `agent-workspace/artifacts/agent-report.json`.
5. Run `SCORE.cmd` from the package root.
6. Run `RESULT.cmd` later to see the last local result again.
7. Run `RESET.cmd` before testing another agent or model in the same folder.

Use a fresh extraction for the cleanest comparison between agents. Use reset for local practice and repeated tests in the same folder.

## What Passed

The current zip passed the Windows clean `SCORE.cmd` smoke from a fresh extraction outside the source checkout.

The smoke checked:

- package-local `tools/carves/carves.exe` is used;
- the run does not depend on a global `carves.exe`;
- Git and Node are available for the local task evidence path;
- `SCORE.cmd` produces a local-only verified score;
- repeated `SCORE.cmd` after success shows the previous result and exits `0`;
- `results/submit-bundle/` artifacts are written;
- hiding the package-local scorer produces the expected missing-scorer diagnostic;
- hiding Node.js from PATH produces a direct Node dependency diagnostic.

Related evidence:

- `docs/matrix/agent-trial-node-phase-4-acceptance.md`
- `docs/matrix/agent-trial-node-phase-5-release-artifact.md`

Additional Phase 5 checks passed from the exact zip:

- Windows `SCORE.cmd` path smoke with spaces and non-ASCII text in the work
  root;
- fresh extraction user-path smoke:

```text
extract zip -> edit agent-workspace -> node tests/bounded-fixture.test.js -> SCORE.cmd -> RESULT.cmd -> RESET.cmd
```

## Still Out Of Scope

Linux and macOS playable scorer bundles are not complete in this handoff.

This handoff also does not claim:

- server submission;
- server receipt;
- leaderboard entry;
- certification;
- hosted verification;
- package signing;
- clean-machine certification;
- OS sandboxing;
- local anti-cheat;
- tamper-proof execution.

## Next Recommended Check

Run one operator review smoke:

```text
extract zip -> open agent-workspace/ in a real agent -> paste blind prompt -> run SCORE.cmd -> run RESULT.cmd -> run RESET.cmd
```

That check validates the human workflow with a real agent, not just the
scripted package smoke.
