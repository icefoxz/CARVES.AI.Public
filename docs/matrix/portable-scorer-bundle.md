# CARVES Portable Scorer Bundle Contract

Status: CARD-945 Windows playable scorer bundle contract, updated by CARD-949 for Linux/macOS follow-up staging.

This contract freezes the V1 Windows playable package rule: a playable zip must carry a runnable package-local scorer. A clean Windows user should not need a global `carves` install, a CARVES source checkout, or a `dotnet build` step before double-clicking `SCORE.cmd`.

Linux/macOS playable scorer bundles are staged follow-up, not the current Windows V1 gate. They must not be described as complete release packages until their package-local scorer, archive format, executable permissions, launcher lookup, and clean-machine smoke are implemented and verified.

This card defines the contract only. It does not implement hosted submission, receipts, signatures, transparency logs, leaderboard eligibility, or release publishing.

The release-oriented local assembly command is:

```bash
carves-matrix trial package \
  --output ./carves-agent-trial-pack-win-x64 \
  --windows-playable \
  --scorer-root ./artifacts/publish/carves-win-x64 \
  --zip-output ./carves-agent-trial-pack-win-x64.zip \
  --runtime-identifier win-x64 \
  --build-label <build-label>
```

`--scorer-root` points at an already published or staged Windows scorer directory that contains `carves.exe`. The assembler copies that directory into `tools/carves/`, writes `tools/carves/scorer-manifest.json`, and emits the playable zip. It fails clearly if the scorer root or `carves.exe` is missing.

The release wrapper that publishes the scorer root and assembles the zip in one source-checkout step is:

```powershell
pwsh ./scripts/matrix/build-windows-playable-package.ps1 `
  -OutputRoot ./artifacts/release/windows-playable `
  -Configuration Release `
  -BuildLabel <build-label> `
  -Force
```

That wrapper calls `publish-windows-playable-scorer.ps1`, then runs the staged Runtime `carves.exe test package --windows-playable` with the staged scorer root, and verifies the zip contains package-local scorer files while keeping `tools/carves/` outside `agent-workspace/`.

## Windows Scorer Root Publish

CARD-950 stages the release input that feeds `--scorer-root`. The scorer root is produced from the Runtime public `carves` CLI, not from `carves-matrix`, because `SCORE.cmd` calls the user-facing `carves test collect` path from the package root.

The release input command is:

```powershell
pwsh ./scripts/matrix/publish-windows-playable-scorer.ps1 `
  -OutputRoot ./artifacts/publish/carves-win-x64 `
  -Configuration Release `
  -BuildLabel <build-label> `
  -Force
```

The script publishes `src/CARVES.Runtime.Cli/carves.csproj` with `--runtime win-x64` and `--self-contained true`, writes `carves.exe`, and writes `scorer-root-manifest.json`. The scorer root is source-checkout work needed to create a release package; the resulting scorer root must be runnable from another directory without `dotnet run`, a source checkout, or a global `carves` command.

`scorer-root-manifest.json` is release-input diagnostic metadata. It records:

- `schema_version=carves-windows-scorer-root.v0`;
- `runtime_identifier=win-x64`;
- `entrypoint=carves.exe`;
- `target_project=src/CARVES.Runtime.Cli/carves.csproj`;
- `self_contained=true`;
- `requires_source_checkout_to_run=false`;
- `requires_dotnet_to_run=false`;
- `uses_dotnet_run=false`;
- `supported_commands=["test collect","test reset","test verify","test result"]`.

This root manifest is not the public package scorer manifest. During package assembly, `carves-matrix trial package --windows-playable` still writes the public `tools/carves/scorer-manifest.json` inside the package.

## Windows Playable Zip Assembly Gate

CARD-951 stages the release zip assembly path from the self-contained scorer root. The assembly script must verify:

- the scorer root contains `carves.exe`;
- the scorer root contains `scorer-root-manifest.json`;
- the zip contains `README-FIRST.md`, `SCORE.cmd`, `score.sh`, `RESULT.cmd`, `result.sh`, `RESET.cmd`, `reset.sh`, `.carves-pack/state.json`, `agent-workspace/AGENTS.md`, `tools/carves/carves.exe`, `tools/carves/scorer-root-manifest.json`, and `tools/carves/scorer-manifest.json`;
- zip entry names are portable-relative, not absolute local paths;
- `agent-workspace/tools/` is absent;
- packaged scorer manifests do not contain absolute local path leaks such as the source checkout, output root, package root, or scorer root.

Passing this gate does not prove Windows double-click scoring on a clean machine. That remains a later `SCORE.cmd` clean-smoke gate.

## Windows SCORE.cmd Clean Smoke

CARD-952 adds the Windows clean-smoke gate for the packaged `SCORE.cmd` path:

```powershell
pwsh ./scripts/matrix/smoke-windows-score-cmd.ps1 `
  -ZipPath ./artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
```

When `-ZipPath` is omitted, the smoke can call `build-windows-playable-package.ps1` first and then test the generated zip. The smoke is Windows-only. It extracts the zip into a temporary directory outside the source checkout, uses an isolated PATH that includes Windows system tools, Git, and task runtime tools such as `node.exe`, rejects global `carves.exe`, writes the bounded-edit agent report fixture, executes `cmd.exe /d /c SCORE.cmd` with `CARVES_AGENT_TEST_NO_PAUSE=1`, verifies the local-only scored result and submit bundle, repeats `SCORE.cmd` to confirm readback of the previous result, hides `tools/carves/carves.exe` to confirm the missing-scorer diagnostic, and removes Node.js from PATH to confirm the missing-Node dependency diagnostic.

Passing this smoke still does not provide signing, hosted verification, certification, leaderboard eligibility, producer identity, tamper-proof execution, anti-cheat, or operating-system sandboxing.

## Windows SCORE.cmd Path Smoke

CARD-953 adds a wrapper smoke for Windows path robustness:

```powershell
pwsh ./scripts/matrix/smoke-windows-score-cmd-paths.ps1 `
  -ZipPath ./artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip
```

The path smoke calls the clean smoke with a work root and build output root containing spaces and non-ASCII path text. It verifies the packaged `SCORE.cmd` path still produces the local-only score output and missing-scorer diagnostics under that extraction shape. Passing this smoke narrows a common Windows first-run risk, but it does not certify every locale, network share, long-path, antivirus, archive tool, or shell configuration.

## Windows Playable SCORE.cmd CI Smoke

CARD-954 wires the Windows path smoke into `.github/workflows/matrix-proof.yml` as `windows-playable-scorecmd-smoke` on `windows-latest`.

The job runs:

```powershell
./scripts/matrix/smoke-windows-score-cmd-paths.ps1 `
  -WorkRoot $env:RUNNER_TEMP/... `
  -Configuration Release `
  -BuildLabel "github-actions-<run-id>"
```

It uploads `windows-scorecmd-path-smoke-summary.json`, the generated Windows playable zip, and the local-only score output directories as diagnostic artifacts. The summary uses `carves-windows-scorecmd-path-smoke-summary.v0`, redacts the absolute work root and source checkout root, records the relative zip and result directories, repeats the local-only non-claims, and still writes a failure summary when the smoke fails. This is a CI regression gate for the packaged `SCORE.cmd` first-run path; it is not public download hosting, package signing, hosted verification, certification, leaderboard eligibility, or proof that every Windows machine configuration is supported.

## Windows Playable Layout

The Windows playable zip MUST include a package-local scorer under `tools/carves/`:

```text
carves-agent-trial-pack-win-x64/
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
      <runtime files required by the scorer>
```

`tools/carves/` is package tooling. It MUST NOT be placed inside `agent-workspace/`, and the tested agent must not need to read it to complete the task.

## Release And Developer Package Split

There are two valid package forms:

- Release Windows playable zip: includes a self-contained scorer under `tools/carves/`, supports double-click `SCORE.cmd`, and does not require global `carves`, source checkout, or `dotnet build` for the first-run score path.
- Developer directory package: may be framework-dependent or scorerless while building the release pipeline. It can rely on a repo-local or PATH `carves` command, but it must not be described as the Windows playable zip.

The product first-run promise applies only to the release Windows playable zip. Source-checkout generation commands are developer surfaces until they attach the package-local scorer bundle.

## Linux/macOS Follow-Up Staging

Linux/macOS playable scorer bundles are planned as follow-up packages after the Windows playable path is proven. They should use the same package authority boundary, local-only result contract, reset/readback launchers, and package-local scorer rule as the Windows package. They are not a blocker for the V1 Windows double-click closure.

Candidate linux-x64 layout:

```text
carves-agent-trial-pack-linux-x64/
  README-FIRST.md
  COPY_THIS_TO_AGENT_BLIND.txt
  COPY_THIS_TO_AGENT_GUIDED.txt
  score.sh
  SCORE.cmd
  result.sh
  RESULT.cmd
  reset.sh
  RESET.cmd
  agent-workspace/
  .carves-pack/
  results/
    local/
    submit-bundle/
  tools/
    carves/
      carves
      scorer-manifest.json
      <runtime files required by the scorer>
```

Candidate macOS layouts:

```text
carves-agent-trial-pack-osx-arm64/
  score.sh
  agent-workspace/
  .carves-pack/
  results/
  tools/
    carves/
      carves
      scorer-manifest.json
      <runtime files required by the scorer>

carves-agent-trial-pack-osx-x64/
  score.sh
  agent-workspace/
  .carves-pack/
  results/
  tools/
    carves/
      carves
      scorer-manifest.json
      <runtime files required by the scorer>
```

Linux/macOS archives should prefer a permission-preserving `.tar.gz` release artifact unless zip extraction is proven to preserve the executable bit for `score.sh` and `tools/carves/carves` in the supported first-run path. If zip is used for a non-Windows release, the smoke must explicitly verify executable permissions after extraction. Launchers and shell files must use LF line endings; CRLF in `score.sh` is a release blocker. macOS packages also need an explicit Gatekeeper/quarantine diagnostic if unsigned local binaries are blocked by the host OS.

The package-local POSIX scorer entrypoint is `tools/carves/carves`. `score.sh` must look for that entrypoint first. PATH `carves` is an advanced fallback only for developer packages or intentional local installs; it is not the primary future portable package path and must not be the first-run promise for Linux/macOS playable bundles.

Self-contained Linux/macOS scorer bundles are larger, but they best match the first-run product goal because the user does not need a preinstalled .NET runtime. Framework-dependent bundles are smaller and useful for development, internal smoke, or advanced users, but they must state the runtime requirement plainly and must not be marketed as zero-setup playable packages.

The Linux/macOS follow-up keeps the same non-claims as Windows. A package-local scorer is still local-only and does not create hosted verification, certification, leaderboard eligibility, producer identity, tamper-proof execution, signatures, transparency-log backing, anti-cheat, operating-system sandboxing, or semantic correctness proof.

## Launcher Lookup Order

`SCORE.cmd`, `score.sh`, `RESET.cmd`, and `reset.sh` use this scorer lookup order:

1. Package-local scorer first: `tools/carves/carves.exe` on Windows, or `tools/carves/carves` on POSIX.
2. PATH fallback second: `carves`.
3. Clear failure third: explain that no package-local scorer and no PATH `carves` were found.

The clear failure must not imply that users should build CARVES from source for the Windows playable first-run path. It should tell users to download a full Windows playable package, regenerate the package with a scorer bundle, or intentionally install `carves` as a developer fallback.

## Scorer Manifest

The scorer bundle MUST include `tools/carves/scorer-manifest.json`.

Minimum contract:

```json
{
  "schema_version": "carves-portable-scorer.v0",
  "scorer_kind": "runtime_cli",
  "runtime_identifier": "win-x64",
  "entrypoint": "tools/carves/carves.exe",
  "carves_version": "0.1.0-local",
  "build_label": "local-build-label",
  "supported_commands": [
    "test collect",
    "test reset",
    "test verify",
    "test result"
  ],
  "self_contained": true,
  "requires_source_checkout_to_run": false,
  "requires_dotnet_to_run": false,
  "uses_dotnet_run": false,
  "scorer_root_manifest": "tools/carves/scorer-root-manifest.json",
  "local_only": true,
  "server_submission": false,
  "certification": false,
  "leaderboard_eligible": false,
  "non_claims": [
    "not_tamper_proof_signature",
    "not_certification",
    "not_server_receipt",
    "not_leaderboard_proof",
    "not_producer_identity",
    "not_anti_cheat",
    "not_os_sandbox"
  ],
  "file_hashes": [
    {
      "path": "tools/carves/carves.exe",
      "sha256": "sha256:<hex>"
    }
  ],
  "file_hashes_unavailable_reason": null
}
```

If file hashes are unavailable for a developer package, `file_hashes` may be empty only when `file_hashes_unavailable_reason` is a non-empty string. A release Windows playable zip should provide hashes for the scorer entrypoint and runtime files.

## Manifest Non-Claims

`scorer-manifest.json` is diagnostic metadata. It is not:

- a tamper-proof signature;
- certification;
- a server receipt;
- leaderboard proof;
- producer identity;
- anti-cheat;
- an operating-system sandbox;
- proof of semantic source-code correctness.

A local user who controls the machine can still replace `tools/carves/`, edit `scorer-manifest.json`, patch launchers, change environment variables, alter clocks, or modify local evidence. The package-local scorer makes first run practical; it does not make local execution adversarially trustworthy.

## Boundaries

The package-local scorer is owned by the package root, not the tested workspace.

The following MUST stay outside `agent-workspace/`:

- `tools/carves/`;
- `tools/carves/scorer-manifest.json`;
- `SCORE.cmd`;
- `score.sh`;
- `RESULT.cmd`;
- `result.sh`;
- `RESET.cmd`;
- `reset.sh`;
- `.carves-pack/`;
- expected hashes;
- scorer policy files;
- server receipts or leaderboard metadata.

The scorer manifest can help diagnose what binary a user ran. It must not be treated as authority for hosted submission, certification, public ranking, or tamper-proof proof.
