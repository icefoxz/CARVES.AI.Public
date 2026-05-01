# CARVES Portable Agent Trial Pack Contract

Status: CARD-938 portable package boundary and layout contract, updated by CARD-945 for the package-local scorer bundle boundary.

This contract freezes the product shape for a future downloadable Agent Trial package. It does not implement package generation, launchers, hosted submission, receipts, signatures, or leaderboards. Implementation cards must follow this layout instead of inventing another package shape.

The goal is simple:

```text
download zip -> extract folder -> open only agent-workspace/ in the tested agent -> paste one prompt -> run SCORE.cmd or score.sh from the package root -> read local results
```

The package is a local playtest wrapper around the existing Matrix Agent Trial collector, scorer, verifier, result card, and history logic. It is not a new Matrix engine.

A V1 Windows playable zip must also include a runnable package-local scorer under `tools/carves/`. A clean Windows first run must not require a global `carves` command, CARVES source checkout, or `dotnet build`. Developer directory packages may be scorerless or framework-dependent while the release package is assembled, but they must not be described as the Windows playable zip. Linux/macOS playable scorer bundles are staged follow-up, not the current Windows V1 gate; they need their own package-local scorer layout, archive permission smoke, and clean-machine verification before being described as complete.

## Prepare A Package

The current package writer creates the directory form of this layout:

```bash
carves test package --output ./carves-agent-trial-pack
```

The Matrix-owned equivalent is:

```bash
carves-matrix trial package --output ./carves-agent-trial-pack
```

The release-oriented Windows playable assembler is:

```bash
carves-matrix trial package \
  --output ./carves-agent-trial-pack-win-x64 \
  --windows-playable \
  --scorer-root ./artifacts/publish/carves-win-x64 \
  --zip-output ./carves-agent-trial-pack-win-x64.zip \
  --runtime-identifier win-x64 \
  --build-label <build-label>
```

`--scorer-root` must contain `carves.exe`. The assembler copies that publish output into `tools/carves/`, writes `tools/carves/scorer-manifest.json`, and writes the zip named by `--zip-output`. If the scorer root is missing or does not contain `carves.exe`, assembly fails instead of producing a misleading playable package.

Prepare the Windows scorer root with:

```powershell
pwsh ./scripts/matrix/publish-windows-playable-scorer.ps1 `
  -OutputRoot ./artifacts/publish/carves-win-x64 `
  -Configuration Release `
  -BuildLabel <build-label> `
  -Force
```

That script publishes the Runtime `carves` CLI as a self-contained `win-x64` scorer root. The resulting `carves.exe` is release input for `--scorer-root`; it must run without `dotnet run`, a source checkout, or a global `carves` command.

To publish the scorer root and assemble the Windows playable zip in one release step, use:

```powershell
pwsh ./scripts/matrix/build-windows-playable-package.ps1 `
  -OutputRoot ./artifacts/release/windows-playable `
  -Configuration Release `
  -BuildLabel <build-label> `
  -Force
```

The wrapper verifies the zip contains package-local scorer files, keeps `tools/carves/` outside `agent-workspace/`, and rejects absolute local root leaks in packaged scorer manifests.

Run the Windows clean SCORE.cmd smoke with:

```powershell
pwsh ./scripts/matrix/smoke-windows-score-cmd.ps1 `
  -ZipPath ./artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
```

The smoke extracts the zip outside the source checkout, isolates PATH so global `carves.exe` is not used, keeps task runtime tools such as `node.exe` available for the official Node fixture command, simulates the bounded agent run, executes `cmd.exe /d /c SCORE.cmd`, verifies the local-only result bundle, and checks the missing package-local scorer diagnostic.

Run the Windows SCORE.cmd path smoke with:

```powershell
pwsh ./scripts/matrix/smoke-windows-score-cmd-paths.ps1 `
  -ZipPath ./artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
```

The path smoke wraps the clean smoke with spaces and non-ASCII path text in the temporary extraction and release output roots, then verifies the same local-only score output.

The current Phase 5 Windows playable artifact is:

```text
artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
SHA256: f170162eca839625847f0581a27fa20575a51a9147e69250d78545a8e84acb90
Size: 51234914 bytes
Build label: node-starter-phase5-20260420
```

This artifact is a parallel official local trial entry. It is not a public
hosting claim and does not replace the existing default source-checkout
quickstart.

The writer copies the official starter pack into `agent-workspace/`, initializes a local git baseline, and writes package authority metadata under `.carves-pack/`. It refuses to overwrite a non-empty output directory unless `--force` is used against an existing CARVES portable package.

Zip creation is later package-surface work. The current package writer prepares the ready-to-agent package directory, records external authority metadata, writes `score.sh`, `SCORE.cmd`, `result.sh`, `RESULT.cmd`, `reset.sh`, and `RESET.cmd`, and binds local scoring to that external authority before collection.

When `--windows-playable` is used, zip creation is part of the release-oriented assembly path and the zip includes the package-local scorer under `tools/carves/`.

## Package Layout

The portable package root MUST use this shape:

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
    AGENTS.md
    CLAUDE.md
    src/
    tests/
    artifacts/
  .carves-pack/
    state.json
    pack-manifest.json
    authority/
    expected/
    scorer/
    baseline/
  results/
    local/
    submit-bundle/
  tools/
    carves/
      carves.exe
      scorer-manifest.json
      <runtime files required by the scorer>
```

Required top-level files:

- `README-FIRST.md` gives the human first-run instructions.
- `COPY_THIS_TO_AGENT_BLIND.txt` is the prompt for a blind agent run.
- `COPY_THIS_TO_AGENT_GUIDED.txt` is the prompt for a guided agent run.
- `SCORE.cmd` is the Windows local scoring entry; it preflights Git, package layout, local package state, agent report, and scorer availability, then pauses before closing.
- `score.sh` is the POSIX local scoring entry; it preflights Git, package layout, local package state, agent report, and scorer availability.
- `RESULT.cmd` and `result.sh` show the last local result after the score window has been closed.
- `RESET.cmd` and `reset.sh` clear the local attempt before testing another agent in the same package folder.

Release Windows playable packages must include `tools/carves/carves.exe` and `tools/carves/scorer-manifest.json`. The scorer manifest contract is defined in [CARVES Portable Scorer Bundle Contract](portable-scorer-bundle.md).

Linux/macOS playable scorer bundles are follow-up packages, not the current V1 Windows closure. Their candidate package-local scorer entrypoint is `tools/carves/carves`, launched by `score.sh` before any PATH fallback. Those packages must preserve LF line endings and executable permissions for both `score.sh` and `tools/carves/carves`; `.tar.gz` is the preferred archive shape unless zip permission preservation is smoke-tested.

## Prompt Modes

Blind mode is the recommended mode for comparing agent reliability. The user should start a fresh agent thread, open only `agent-workspace/` in the tested agent, paste `COPY_THIS_TO_AGENT_BLIND.txt`, and avoid extra coaching.

Guided mode is for learning the workflow and local practice. The user should start a fresh agent thread, open only `agent-workspace/`, and paste `COPY_THIS_TO_AGENT_GUIDED.txt`. Guided mode includes more task-specific help, so it should not be mixed into strict comparisons.

Users should paste one prompt, not both. The tested agent should not need the package root, `.carves-pack/`, `results/`, `SCORE.cmd`, `score.sh`, `RESULT.cmd`, `result.sh`, `RESET.cmd`, or `reset.sh` to complete the task.

Required top-level directories:

- `agent-workspace/` is the only directory the tested agent should open or edit.
- `.carves-pack/` is the local scorer authority area and MUST stay outside `agent-workspace/`.
- `results/` is local output owned by the user and scorer.
- `results/submit-bundle/` is the future upload-ready local output location.
- `tools/carves/` is the package-local scorer bundle for Windows playable packages and MUST stay outside `agent-workspace/`.

## Agent Workspace Boundary

Users should open only `agent-workspace/` in the tested agent. They should not open the package root as the agent's working folder for a scored run.

The agent can read and edit normal task files inside `agent-workspace/`. The agent is expected to follow `agent-workspace/AGENTS.md`, `agent-workspace/CLAUDE.md`, the task instructions, and the required report contract.

The tested agent must not need access to `.carves-pack/`, score scripts, expected hashes, pack authority files, local history, or submission packaging internals. If a future implementation requires the agent to inspect `.carves-pack/` to pass, that implementation violates this contract.

## Scorer Authority Boundary

`.carves-pack/` is the local scorer authority area. It is outside the agent writable workspace by design.

`.carves-pack/` may contain pinned pack metadata, expected task contract snapshots, baseline metadata, scorer configuration, schema references, and other local authority inputs used by the collector and verifier.

Generated packages also write `.carves-pack/state.json`. This file records the package state outside `agent-workspace/` so the tested agent cannot make a run look fresh by changing workspace files. The V1 states are:

- `ready_for_agent`: package has been generated and has not been scored.
- `scored`: package produced a verified local score.
- `failed`: scoring started but did not produce a verified local score; inspect the failed evidence.
- `contaminated`: the scorer found stale output, pre-existing judge evidence, invalid package state, or unexpected package-root files before scoring.

For generated packages, local scoring treats the files inside `agent-workspace/.carves/trial/` as evidence, not authority. Before it runs the task command or collects changed-file evidence, the collector checks:

- `agent-workspace/.carves/trial/task-contract.json` against `.carves-pack/expected/task-contract.json`;
- `agent-workspace/.carves/trial/instruction-pack.json` against `.carves-pack/expected/instruction-pack.json`;
- the workspace `.git/` baseline commit and tree against `.carves-pack/baseline-manifest.json`;
- protected starter metadata such as root instructions, prompts, task descriptions, `.carves/` metadata, and project files against their recorded baseline hashes.

If those checks fail, collection fails closed and reports a package authority diagnostic. Non-portable local trial workspaces that do not have a sibling `.carves-pack/` keep the legacy local behavior.

The following MUST NOT be placed inside `agent-workspace/`:

- `.carves-pack/`;
- `tools/carves/`;
- `tools/carves/scorer-manifest.json`;
- `SCORE.cmd`;
- `score.sh`;
- `RESULT.cmd`;
- `result.sh`;
- `RESET.cmd`;
- `reset.sh`;
- expected hashes;
- pinned task contract authority;
- scorer policy files;
- server challenge secrets;
- leaderboard or receipt metadata.

For V1, the local scorer trusts files under `.carves-pack/` only as local package authority. This does not make the local machine tamper-proof.

## Package-Local Scorer Bundle

The Windows playable zip must ship a runnable scorer in `tools/carves/` so the first-run path works after unzip without installing `carves` globally, checking out CARVES source, or running `dotnet build`.

`SCORE.cmd` and `score.sh` use this lookup order:

1. Package-local scorer first: `tools/carves/carves.exe` on Windows, or `tools/carves/carves` on POSIX.
2. PATH fallback second: `carves`.
3. Clear failure third: explain that the package has no package-local scorer and no PATH `carves`.

The PATH fallback is a developer escape hatch, not the Windows playable first-run requirement. A package that relies on PATH `carves`, source checkout, or `dotnet build` is a developer package, not the release Windows playable zip.

For Linux/macOS follow-up packages, PATH `carves` remains an advanced fallback only. The future portable package path should be package-local first and must keep scorer binaries outside `agent-workspace/`.

`tools/carves/scorer-manifest.json` records the scorer bundle diagnostics: schema version, scorer kind, runtime identifier, entrypoint, CARVES version or build label, supported command surface, local-only non-claims, and file hashes or a reason hashes are unavailable.

The scorer manifest is diagnostic metadata only. It is not a tamper-proof signature, certification, server receipt, leaderboard proof, producer identity, anti-cheat, operating-system sandbox, or semantic correctness proof.

## Launcher Diagnostics

`SCORE.cmd` and `score.sh` must fail with product-level next steps before invoking `carves test collect` when they can detect a first-run setup issue locally.
After invoking `carves test collect`, they must print the result-card path only when `results/local/matrix-agent-trial-result-card.md` actually exists. If collection fails before that card is produced, they must say that no local result card was produced and point the user back to the diagnostic output.

The launcher diagnostics distinguish:

- missing Git: install Git, then rerun the scorer from the package root;
- missing `agent-workspace/`: extract a fresh CARVES Agent Trial package;
- missing `.carves-pack/` or `.carves-pack/state.json`: extract a fresh package and do not move scorer authority into `agent-workspace/`;
- package state `scored`: show the previous local result and tell the user to run `RESET.cmd` or `reset.sh` before testing another agent in the same folder;
- package state `failed`: inspect `results/` diagnostics or extract a fresh package;
- package state `contaminated`: extract a fresh package and open only `agent-workspace/` in the tested agent;
- missing `agent-workspace/artifacts/agent-report.json`: first open only `agent-workspace/` in an AI agent, paste `COPY_THIS_TO_AGENT_BLIND.txt` or `COPY_THIS_TO_AGENT_GUIDED.txt`, then rerun after the agent writes the report;
- missing package-local scorer and missing PATH fallback: this is not a complete playable package; download or regenerate a package with the scorer bundle, or intentionally install `carves` on PATH as a developer fallback.

These diagnostics must not tell first-run users to pass `--workspace`, `--bundle-root`, or Matrix-specific internal paths.

## Results Layout

`results/` is local output. The package should keep generated evidence out of the agent's normal task surface.

The stable result shape is:

```text
results/
  local/
    matrix-agent-trial-result-card.md
    matrix-verify.json
    history/
  submit-bundle/
    matrix-artifact-manifest.json
    matrix-proof-summary.json
    trial/
```

`results/local/` is for human local readback, diagnostics, and local history.

`results/submit-bundle/` is the future upload-ready local output location. V1 local packages may create the directory before server submission exists, but local verification must not claim hosted submission, receipt issuance, leaderboard eligibility, or certification from that directory alone.

## Reuse And Reset

V1 portable packages can be reused through the package reset command.

After a completed score, `SCORE.cmd` and `score.sh` show the previous result instead of starting another score on top of old evidence. `RESULT.cmd` and `result.sh` are the explicit readback launchers for users who closed the score window.

Before testing another agent, model, or settings in the same folder, the user should run `RESET.cmd`, `reset.sh`, or `carves test reset` from the package root. Reset restores `agent-workspace/` to its local git baseline, archives `results/local/` and `results/submit-bundle/` under `results/history/`, parks unexpected package-root files under that history folder, and sets the package back to `ready_for_agent`.

Reset keeps `.carves-pack/`, package scripts, prompt files, scorer tooling, and history. It does not create a server submission, receipt, certification, leaderboard entry, or tamper-proof local environment. Fresh extraction remains the cleanest strict-comparison path when users want to avoid carrying local history.

## Public Non-Claims

The portable package does not provide:

- local anti-cheat;
- tamper-proof local execution;
- operating-system sandboxing;
- hosted verification;
- public certification;
- leaderboard eligibility;
- producer identity;
- semantic source-code correctness;
- model safety benchmarking;
- source upload;
- raw diff upload;
- prompt response upload;
- model response upload;
- secret or credential upload.

A user who controls the local machine can still edit files, patch commands, change clocks, alter environment variables, or modify scorer inputs. The local package is useful for a convenient and reviewable local playtest, not for adversarial attestation.

Server challenge retrieval, receipts, hosted reruns, account identity, signatures, transparency logs, and leaderboard rules are separate future surfaces.

## Relationship To Existing Commands

The portable package must reuse the existing Matrix Agent Trial mechanism. The public package flow should hide Matrix path arguments from first-run users.

Advanced commands such as `carves-matrix trial local --workspace ... --bundle-root ...` remain debugging and integration surfaces. They are not the main portable package story.

The product-level package entry is:

```text
carves test package --output <package-root>
carves-matrix trial package --output <package-root>
```

The product-level scoring entry from package root is:

```text
carves test collect
carves test reset
./score.sh
./result.sh
./reset.sh
SCORE.cmd
RESULT.cmd
RESET.cmd
```

These route to the same local Matrix verifier-backed path and write the verified bundle under `results/submit-bundle/`, with local readback under `results/local/`. They are launchers, not a second scoring engine.
