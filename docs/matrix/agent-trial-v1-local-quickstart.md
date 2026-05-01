# CARVES Agent Trial V1 Local Quickstart

Status: CARD-933 public quickstart collapsed to `carves test`.

Use this guide when you want to download or clone CARVES, run the official local Agent Trial starter pack with your own agent, produce a verified local result, and optionally compare repeated runs. This is a local-only playtest path. It does not submit data, create a server receipt, enter a leaderboard, or certify an agent.

## What You Need

- A local checkout or downloaded source package of this repository.
- A .NET SDK that can run the Runtime CLI from source, or an installed `carves` command.
- Git on `PATH`.
- An AI coding agent or IDE agent if you want to run the real agent-assisted path.

No server account, private operator setup, hosted API, source upload, raw diff upload, prompt response upload, model response upload, secret upload, or credential upload is required.

## First Run

From a source checkout, start with the automatic demo:

```bash
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test demo --json
```

If CARVES is installed, the same path is:

```bash
carves test demo
```

`carves test demo` is a fully automatic local check. It prepares the official starter pack, creates a git baseline, applies a built-in successful sample edit/report, collects evidence, runs strict Matrix trial verification, records local history, and prints the score/result-card paths.

The default output stays under:

```text
./carves-trials/
  latest.json
  history/
    runs/<run-id>.json
  run-<timestamp-or-id>/
    workspace/
    bundle/
```

`latest.json` is only a local convenience pointer. It is not part of the Matrix manifest, not a receipt, not leaderboard evidence, and not certification.

## Run Your Own Agent

After the demo works, prepare a real agent-assisted run:

```bash
carves test agent
```

From source:

```bash
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test agent
```

The command prepares a local starter workspace and prints the instruction to give your agent. Your agent works inside that generated workspace, follows its `AGENTS.md`, constraints, task contract, and prompt, runs the required command, and writes `artifacts/agent-report.json`. When the command continues, CARVES collects summary evidence, verifies the bundle, writes the result card, and updates local history.

To inspect or recheck the latest local result:

```bash
carves test result
carves test verify
```

## Windows Double-Click Launcher

Windows users can double-click this file from the repository or release folder:

```text
Start-CARVES-Agent-Test.cmd
```

The launcher opens a small local menu for `carves test demo`, `carves test agent`, `carves test result`, and `carves test verify`. It checks for a usable packaged `carves` command, repo-local `carves.cmd`, installed `carves` command, or source checkout plus `dotnet`. It pauses before closing so success output, score summary, result-card path, and failure diagnostics remain visible.

Command-line users can skip the launcher and run `carves test demo` or `carves test agent` directly. The launcher is only a convenience wrapper. It is not a sandbox, anti-cheat system, certification, benchmark, hosted verification, server receipt, or leaderboard submission.

## Node Windows Playable Zip

If you already have the Node Windows playable zip, use
[Node Windows Playable Agent Trial Quickstart](agent-trial-node-windows-playable-quickstart.md).

That guide is the beginner-facing path for:

```text
extract zip -> open only agent-workspace/ -> paste one prompt -> run SCORE.cmd -> read RESULT.cmd -> RESET.cmd before another run
```

The Node Windows playable zip is a parallel official local trial entry. It is
not yet the default public local trial entry. The source-checkout
`carves test demo` / `carves test agent` flow above remains the default
quickstart.

## Portable Package Contract

The current source-checkout quickstart is the working local path. The product-level portable package shape is frozen separately in [Portable Agent Trial pack contract](portable-agent-trial-pack.md).

To prepare the current directory-form package:

```bash
carves test package --output ./carves-agent-trial-pack
```

From source:

```bash
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test package --output ./carves-agent-trial-pack --json
```

The package writer creates `agent-workspace/` with a committed local git baseline, writes external package authority under `.carves-pack/`, and writes root score launchers. It does not require users to pass `--workspace` or `--bundle-root`.

The portable package directory shape is:

```text
carves-agent-trial-pack/
  README-FIRST.md
  COPY_THIS_TO_AGENT_BLIND.txt
  COPY_THIS_TO_AGENT_GUIDED.txt
  SCORE.cmd
  score.sh
  RESULT.cmd
  result.sh
  RESET.cmd
  reset.sh
  agent-workspace/
  .carves-pack/
  results/
    local/
    submit-bundle/
  tools/
    carves/
      carves.exe
      scorer-manifest.json
```

For a scored portable run, users should open only `agent-workspace/` in the tested agent. After the agent writes `agent-workspace/artifacts/agent-report.json`, run `carves test collect`, `./score.sh`, or `SCORE.cmd` from the package root. `.carves-pack/` is the local scorer authority area and must stay outside the agent writable workspace. `results/local/` is the human readback area. `results/submit-bundle/` is the future upload-ready local output location, not a receipt or leaderboard entry by itself.

If `./score.sh` or `SCORE.cmd` fails before a result card exists, it must say that no local result card was produced instead of printing a path to a missing card. Fix the diagnostic it printed, then rerun the scorer or reset before another agent run.

The portable first-run flow is:

```text
zip -> extract -> open agent-workspace/ -> paste one prompt -> run score.sh or SCORE.cmd -> read RESULT.cmd/result.sh later if needed
```

For the release Windows playable zip, `SCORE.cmd` must find a package-local scorer at `tools/carves/carves.exe` and diagnostic metadata at `tools/carves/scorer-manifest.json`. That playable first run must not require global `carves`, a CARVES source checkout, or `dotnet build`. A source-generated directory that lacks `tools/carves/` is a developer package until the scorer bundle is attached.

The current Phase 5 Windows playable artifact is a parallel official local trial
entry:

```text
artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
SHA256: f170162eca839625847f0581a27fa20575a51a9147e69250d78545a8e84acb90
Size: 51234914 bytes
```

It is not yet the default public local trial entry. Keep using the source
checkout `carves test demo` / `carves test agent` path when you want the
existing default quickstart.

Release assembly uses an existing Windows scorer publish output:

```bash
carves-matrix trial package \
  --output ./carves-agent-trial-pack-win-x64 \
  --windows-playable \
  --scorer-root ./artifacts/publish/carves-win-x64 \
  --zip-output ./carves-agent-trial-pack-win-x64.zip
```

Prepare that publish output first:

```powershell
pwsh ./scripts/matrix/publish-windows-playable-scorer.ps1 `
  -OutputRoot ./artifacts/publish/carves-win-x64 `
  -Configuration Release `
  -BuildLabel <build-label> `
  -Force
```

The scorer root is self-contained `win-x64` release input. The packaged `carves.exe` must run without `dotnet run`, a source checkout, or a global `carves` command.

The single release assembly wrapper is:

```powershell
pwsh ./scripts/matrix/build-windows-playable-package.ps1 `
  -OutputRoot ./artifacts/release/windows-playable `
  -Configuration Release `
  -BuildLabel <build-label> `
  -Force
```

It publishes the scorer root, assembles the Windows playable zip, verifies the zip carries `tools/carves/carves.exe`, and checks packaged scorer manifests for local absolute path leaks.

Run the Windows clean SCORE.cmd smoke with:

```powershell
pwsh ./scripts/matrix/smoke-windows-score-cmd.ps1 `
  -ZipPath ./artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
```

The smoke uses a fresh extraction outside the source checkout, isolates PATH so global `carves.exe` is not used, keeps task runtime tools such as `node.exe` available for the official Node fixture command, runs `cmd.exe /d /c SCORE.cmd`, verifies the local-only score output, repeats `SCORE.cmd` to read back the previous result, confirms hiding `tools/carves/carves.exe` produces the missing-scorer diagnostic, and confirms missing Node.js is reported as a dependency problem instead of a collected trial failure.

Run the Windows SCORE.cmd path smoke with:

```powershell
pwsh ./scripts/matrix/smoke-windows-score-cmd-paths.ps1 `
  -ZipPath ./artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
```

The path smoke wraps the clean smoke with a work root and build output root containing spaces and non-ASCII path text, then checks the same local-only score output.

If `--scorer-root` is missing or does not contain `carves.exe`, the assembler fails and does not claim the result is a Windows playable zip.

Linux/macOS playable scorer bundles are staged follow-up, not the current Windows V1 gate. Their planned first-run path is `score.sh` resolving package-local `tools/carves/carves` before any PATH fallback, with archive smoke coverage for executable permissions and LF line endings. Until that is implemented and smoke-tested, Linux/macOS users should treat scorerless or PATH-dependent packages as developer packages rather than complete zero-setup playable releases.

Use `COPY_THIS_TO_AGENT_BLIND.txt` for strict comparisons between agents or settings. Start a fresh agent thread, open only `agent-workspace/`, paste the blind prompt, and avoid extra coaching.

Use `COPY_THIS_TO_AGENT_GUIDED.txt` for learning and local practice. Guided mode gives more explicit task hints, so do not use guided runs as strict comparison results.

V1 portable packages are reusable through the packaged reset command. After a score closes, run `RESULT.cmd` or `./result.sh` from the package root to read the last local result again. Running `SCORE.cmd` or `./score.sh` again after a completed score shows the previous result instead of starting a second score on top of old evidence.

Before testing another agent or model in the same folder, run `RESET.cmd`, `./reset.sh`, or `carves test reset` from the package root. Reset restores `agent-workspace/` to its local git baseline, archives `results/local/` and `results/submit-bundle/` under `results/history/`, parks unexpected package-root files under that history folder, and marks the package ready for another local run. Reset keeps `.carves-pack/`, the package scripts, prompts, scorer tooling, and history. It does not submit anything, certify anything, create a leaderboard entry, or make the local machine tamper-proof.

If the package has failed evidence, stale files under `results/`, pre-existing judge evidence under `agent-workspace/artifacts/`, or unexpected package-root files, the scorer refuses the run and prints a plain next step. Use reset for local practice reuse. Use a fresh extraction when you want the cleanest comparison artifact.

## What The Score Measures

The local score answers one narrow question:

```text
Did this local agent run leave reviewable, traceable, explainable, honest, constrained, and reproducible summary evidence for this exact task/profile?
```

The score dimensions are:

- `reviewability`;
- `traceability`;
- `explainability`;
- `report_honesty`;
- `constraint`;
- `reproducibility`.

The aggregate is out of 100 when all prerequisite evidence exists. Missing evidence can suppress the aggregate. Failed critical dimensions can cap the score at 30. The result card is meant for humans; the source of truth remains the manifest-covered JSON evidence.

The score does not prove:

- certification;
- leaderboard eligibility;
- hosted verification;
- producer identity;
- operating-system sandboxing;
- semantic source-code correctness;
- local anti-cheat or tamper-proof execution;
- that the model is generally safe or generally intelligent.

A person who controls the local machine can still tamper with the local process. Hosted challenge pulls, receipts, server-side reruns, and leaderboard rules are separate future surfaces.

## Advanced Matrix Commands

The simple `carves test` path is a wrapper over the Matrix Agent Trial implementation. When you need debugging, custom paths, or CI-style orchestration, the lower-level commands remain available after the simple path.

Build the Matrix CLI from source:

```bash
dotnet build ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release
```

Preview the lower-level sequence:

```bash
carves-matrix trial plan --workspace ./carves-agent-trial --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle --json
```

Prepare a workspace:

```bash
carves-matrix trial prepare --workspace ./carves-agent-trial --json
```

After your agent writes `artifacts/agent-report.json`, collect and verify:

```bash
carves-matrix trial local --workspace ./carves-agent-trial --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle --json
carves-matrix trial verify --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle --json
```

Record and compare repeated local runs:

```bash
carves-matrix trial record --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle --history-root ./carves-agent-trial-history --run-id day-1 --json
carves-matrix trial compare --history-root ./carves-agent-trial-history --baseline day-1 --target day-2 --json
```

Advanced commands are useful for debugging, but first-run users do not need to understand `--workspace`, `--bundle-root`, `--history-root`, Matrix manifests, or proof-summary internals before seeing a result.

## Friendly Troubleshooting

Common local diagnostics:

| Diagnostic | Meaning | Next step |
| --- | --- | --- |
| `dotnet` unavailable | The source-checkout command or launcher cannot find the .NET SDK. | Install the .NET SDK or use a packaged `carves` command, then rerun `carves test demo`. |
| `trial_setup_pack_missing` | The starter pack cannot be found. | Run from this source checkout or pass `--pack-root` in the advanced Matrix command. |
| `trial_setup_git_unavailable` | Git is not available to prepare the local baseline. | Install Git and rerun `carves test demo` or `carves test agent`. |
| `trial_agent_report_missing` | The agent did not write `artifacts/agent-report.json`. | Ask the agent to write the required report and rerun collection. |
| `trial_task_contract_pin_mismatch` | The task contract was changed after pack authority was pinned. | Restore the starter pack; do not loosen the contract to make a run pass. |
| `trial_required_command_failed` | The required command failed under collector evidence. | Fix the task output, then rerun collection. |
| `trial_latest_missing` | No latest local run is available. | Run `carves test demo` or `carves test agent` first. |
| `trial_latest_invalid` | The local latest pointer is unreadable or incomplete. | Rerun the demo or verify a specific advanced bundle. |
| `trial_verify_hash_mismatch` | A manifest-covered artifact changed after manifest creation. | Regenerate the bundle from workspace evidence. |
| `trial_portable_package_already_scored` | The portable package has already produced one local score. | Run `RESULT.cmd` or `./result.sh` to read it again, or reset before another local run. |
| `trial_portable_stale_results` | The portable package has result files before scoring. | Run reset to archive previous local output before testing another agent. |
| `trial_portable_judge_evidence_present` | Judge-generated evidence exists before scoring. | Run reset, then let the scorer create judge evidence after the agent report. |
| `trial_portable_unexpected_package_file` | Files were added at package root outside the allowed package layout. | Run reset to park root residue under history, then open only `agent-workspace/` in the tested agent. |
| `trial_agent_report_schema_invalid` | The agent wrote `artifacts/agent-report.json`, but it does not match `agent-report.v0`. | Ask the agent to copy `artifacts/agent-report.template.json` to `artifacts/agent-report.json`, keep `schema_version` exactly `agent-report.v0`, fill the existing fields, and remove extra top-level fields. |

Diagnostics are guidance only. Integrity failures remain failures.
