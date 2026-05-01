# Node Windows Playable Agent Trial Quickstart

Status: Phase 7 public onboarding copy for the Node Windows playable local trial.

Use this guide when you already have the CARVES Agent Trial Windows playable
zip and want to run the Node/TypeScript starter pack without a CARVES source
checkout, global `carves`, or `dotnet build`.

This is a parallel official local trial entry. It is not yet the default public
local trial entry. If you want the current default source-checkout path, use
[Agent Trial local quickstart](agent-trial-v1-local-quickstart.md).

## What This Is

The zip is a local practice package for testing how an AI coding agent handles a
small Node starter project.

The flow is:

```text
extract zip
open only agent-workspace/ in your agent
paste one prompt
let the agent edit the Node starter project
run SCORE.cmd from the package root
read the local result
```

The package includes:

- `agent-workspace/`, the only folder your tested agent should open or edit;
- `COPY_THIS_TO_AGENT_BLIND.txt`, the prompt for strict fresh-thread
  comparisons;
- `COPY_THIS_TO_AGENT_GUIDED.txt`, the prompt for learning and local practice;
- `SCORE.cmd`, the Windows scoring command;
- `RESULT.cmd`, the previous-result readback command;
- `RESET.cmd`, the reset command for another local practice run;
- `tools/carves/carves.exe`, the package-local scorer for the Windows playable
  zip.

## What You Need

- Windows on x64 hardware.
- Git on `PATH`.
- Node.js on `PATH`, because the starter project is a Node project.
- A zip extraction tool.
- An AI coding agent or IDE agent.
- The Windows playable zip:

```text
carves-agent-trial-pack-win-x64.zip
```

The current Phase 5 local artifact record is:

```text
artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
SHA256: f170162eca839625847f0581a27fa20575a51a9147e69250d78545a8e84acb90
Size: 51234914 bytes
Build label: node-starter-phase5-20260420
```

## First Run

1. Extract `carves-agent-trial-pack-win-x64.zip` into a normal local folder.
2. Open `README-FIRST.md` in the extracted package root.
3. In your AI coding agent, open only the extracted `agent-workspace/` folder.
4. Paste one prompt: use `COPY_THIS_TO_AGENT_BLIND.txt` for comparison runs, or
   `COPY_THIS_TO_AGENT_GUIDED.txt` for learning.
5. Let the agent finish the task. The agent should run the project check and
   write `agent-workspace/artifacts/agent-report.json`.
6. Go back to the extracted package root and run `SCORE.cmd`.
7. Read the score summary and result-card path printed by the scoring window.

For a scored run, open only `agent-workspace/` in the tested agent. Do not open
the package root. The package root contains scorer authority, scripts, results,
and package-local tooling that the agent does not need.

The agent report is strict JSON. The agent should copy
`agent-workspace/artifacts/agent-report.template.json` to
`agent-workspace/artifacts/agent-report.json`, keep `schema_version` exactly
`agent-report.v0`, fill the existing fields, and avoid extra top-level fields.

## Which Prompt To Use

Use `COPY_THIS_TO_AGENT_BLIND.txt` when you want a stricter comparison between
agents, models, or settings. Start a fresh agent thread, open only
`agent-workspace/`, paste the blind prompt, and avoid extra coaching.

Use `COPY_THIS_TO_AGENT_GUIDED.txt` when you want to learn the flow. Guided mode
includes more help, so do not mix guided runs into strict comparisons.

## The Three Commands

Run these commands from the extracted package root, not from
`agent-workspace/`.

`SCORE.cmd` checks the package state, runs the local scorer, writes local result
files under `results/`, and prints the score summary. If the package was already
scored, it shows the previous result instead of scoring over old evidence.
If collection fails before a result card exists, it says that no local result
card was produced and leaves the diagnostic text as the next thing to fix.

`RESULT.cmd` shows the previous local result after the scoring window has been
closed.

`RESET.cmd` prepares the same extracted folder for another local practice run.
It restores `agent-workspace/` to its local git baseline and archives previous
local output under `results/history/`.

Use a fresh extraction when you want the cleanest strict-comparison artifact.

## What The Result Means

The score is a local evidence-posture check for this one task run. It asks
whether the agent left reviewable, traceable, explainable, honest, constrained,
and reproducible local evidence.

It does not prove:

- certification;
- leaderboard eligibility;
- hosted verification;
- package signing;
- producer identity;
- operating-system sandboxing;
- semantic source-code correctness;
- local anti-cheat;
- tamper-proof local execution.

A person who controls the local machine can still tamper with the local process.
Hosted challenge pulls, receipts, server-side reruns, signatures, and leaderboard
rules are separate future surfaces.

## Common Setup Problems

If `SCORE.cmd` says Git is missing, install Git and rerun `SCORE.cmd` from the
package root.

If `SCORE.cmd` says Node.js is missing, install Node.js or put `node.exe` on
`PATH`, then rerun `SCORE.cmd`.

If `SCORE.cmd` says `artifacts/agent-report.json` is missing, return to the
tested agent and ask it to finish the task inside `agent-workspace/`.

If `SCORE.cmd` says `trial_agent_report_schema_invalid`, return to the tested
agent and ask it to copy `artifacts/agent-report.template.json` to
`artifacts/agent-report.json`, keep `schema_version` exactly `agent-report.v0`,
fill the existing fields, and remove extra top-level fields.

If the package was already scored, run `RESULT.cmd` to read the previous result,
or run `RESET.cmd` before testing another agent in the same folder.

If you are unsure whether a run is clean enough for comparison, extract a fresh
copy of the zip.

## Advanced Users

The Windows playable zip is for the unzip-and-run path. Source-checkout users
can still use the existing default path:

```text
carves test demo
carves test agent
carves test result
carves test verify
```

See [Agent Trial local quickstart](agent-trial-v1-local-quickstart.md) for that
flow and for lower-level Matrix commands.
