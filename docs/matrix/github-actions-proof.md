# CARVES Matrix GitHub Actions Proof

The matrix proof workflow is:

- `.github/workflows/matrix-proof.yml`

It contains three separate lanes:

1. Linux-native public minimum: `matrix-native-proof-linux`.
2. Linux-native full-release shadow: `matrix-native-full-release-shadow-linux`.
3. Full PowerShell release proof: `matrix-proof`.

## Linux-Native Public Minimum

`matrix-native-proof-linux` runs on `ubuntu-latest` with `shell: bash`. It builds the Matrix CLI project, runs the native proof lane, then verifies the generated bundle:

```bash
dotnet build ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- proof --lane native-minimal --artifact-root artifacts/matrix-native/ubuntu-latest --configuration Release --json
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- verify artifacts/matrix-native/ubuntu-latest --json
```

This lane does not install or invoke `pwsh`, does not invoke `scripts/matrix/*.ps1`, and does not publish packages, tags, releases, hosted verification, leaderboard entries, or certification claims.

The product-level operation is `carves-matrix proof --lane native-minimal --json` followed by `carves-matrix verify <artifact-root> --json`; the workflow uses `dotnet run` only to execute the local CLI project before package publication.

It writes stable CI artifact paths:

```text
artifacts/matrix-native/ubuntu-latest/matrix-native-proof.json
artifacts/matrix-native/ubuntu-latest/matrix-artifact-manifest.json
artifacts/matrix-native/ubuntu-latest/matrix-proof-summary.json
artifacts/matrix-native-verify/ubuntu-latest/matrix-verify.json
artifacts/matrix-native-verify/ubuntu-latest/matrix-artifact-manifest.json
artifacts/matrix-native-verify/ubuntu-latest/matrix-proof-summary.json
```

The uploaded artifact names are:

- `carves-matrix-native-proof-ubuntu-latest`
- `carves-matrix-native-verify-ubuntu-latest`

## Linux-Native Full-Release Shadow

`matrix-native-full-release-shadow-linux` runs on `ubuntu-latest` with `shell: bash`. It builds the Matrix CLI project, runs the explicit native full-release lane, then verifies the generated bundle:

```bash
dotnet build ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- proof --lane native-full-release --runtime-root . --artifact-root artifacts/matrix-native-full-release-shadow/ubuntu-latest --configuration Release --json
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- verify artifacts/matrix-native-full-release-shadow/ubuntu-latest --json
```

This is a non-blocking shadow lane with `continue-on-error: true`. It is parity evidence for the native full-release producer, not the default release proof, not the default public quickstart, and not certification-grade evidence. The PowerShell full-release lane remains the compatibility and release evidence lane while native parity stabilizes.

The product-level operation is `carves-matrix proof --lane native-full-release --json` followed by `carves-matrix verify <artifact-root> --json`; the workflow uses `dotnet run` only to execute the local CLI project before package publication.

It writes stable CI artifact paths:

```text
artifacts/matrix-native-full-release-shadow/ubuntu-latest/matrix-native-full-release-proof.json
artifacts/matrix-native-full-release-shadow/ubuntu-latest/matrix-artifact-manifest.json
artifacts/matrix-native-full-release-shadow/ubuntu-latest/matrix-proof-summary.json
artifacts/matrix-native-full-release-shadow-verify/ubuntu-latest/matrix-verify.json
artifacts/matrix-native-full-release-shadow-verify/ubuntu-latest/matrix-artifact-manifest.json
artifacts/matrix-native-full-release-shadow-verify/ubuntu-latest/matrix-proof-summary.json
```

The uploaded artifact names are:

- `carves-matrix-native-full-release-shadow-ubuntu-latest`
- `carves-matrix-native-full-release-shadow-verify-ubuntu-latest`

Windows native full-release shadow is explicitly deferred. The reason is narrow: Windows packaged local tool install, command shim behavior, executable path resolution, and native full-release atomic promotion need their own parity evidence card before that shadow lane should be added. Windows remains covered by the existing PowerShell full-release lane and the cross-platform Matrix verify pilot.

## Full PowerShell Release Proof

The full proof lane runs on Windows and Linux and executes:

```powershell
./scripts/matrix/matrix-proof-lane.ps1 -ArtifactRoot artifacts/matrix/<os> -Configuration Release
carves-matrix verify artifacts/matrix/<os> --json
./scripts/matrix/matrix-cross-platform-verify-pilot.ps1 -ArtifactRoot artifacts/matrix-pilot-verify/<os> -Configuration Release
```

## What The Full Lane Proves

The lane proves two paths:

1. Project mode: run the Guard, Handoff, Audit, and Shield CLI projects directly from source through the Matrix shell.
2. Packaged mode: pack local `.nupkg` files, install `carves-guard`, `carves-handoff`, `carves-audit`, `carves-shield`, and `carves-matrix` as dotnet tools, then run the same matrix chain.

Both paths create a temporary external git repository and run:

```powershell
carves-guard init
carves-guard check --json
carves-handoff draft --json
carves-audit summary --json
carves-audit timeline --json
carves-audit explain <guard-run-id> --json
carves-audit evidence --json --output .carves/shield-evidence.json
carves-shield evaluate .carves/shield-evidence.json --json --output combined
carves-shield badge .carves/shield-evidence.json --json --output shield-badge.svg
carves-matrix proof --lane full-release
```

After the full proof artifacts are written, the workflow verifies the proof bundle with Matrix verify. This verify step is the native .NET artifact recheck path; it reads the existing bundle and stays separate from the PowerShell proof and pilot scripts. It writes:

```text
artifacts/matrix-verify/<os>/matrix-verify.json
artifacts/matrix-verify/<os>/matrix-artifact-manifest.json
artifacts/matrix-verify/<os>/matrix-proof-summary.json
```

`matrix-verify.json` uses `matrix-verify.v0`. It checks the manifest, required summary artifacts, hashes, sizes, schema metadata, privacy flags, Shield score readback, and proof-summary public readback consistency. A verification failure fails the workflow.

## Artifact Policy

The workflow uploads summary-only native proof artifacts under `artifacts/matrix-native/ubuntu-latest/`, native verification artifacts under `artifacts/matrix-native-verify/ubuntu-latest/`, native full-release shadow proof artifacts under `artifacts/matrix-native-full-release-shadow/ubuntu-latest/`, native full-release shadow verification artifacts under `artifacts/matrix-native-full-release-shadow-verify/ubuntu-latest/`, full proof artifacts under `artifacts/matrix/<os>/`, full verification artifacts under `artifacts/matrix-verify/<os>/`, and pilot verification artifacts under `artifacts/matrix-pilot-verify/<os>/`.

CARVES does not require upload of private source, raw diff text, prompts, model responses, secrets, credentials, or customer payloads for this workflow.

Allowed artifacts:

- `matrix-summary.json`
- `matrix-packaged-summary.json`
- `matrix-proof-summary.json`
- `matrix-artifact-manifest.json`
- `matrix-verify.json`
- `decisions.jsonl`
- `handoff.json`
- `audit-summary.json`
- `audit-timeline.json`
- `audit-explain.json`
- `shield-evidence.json`
- `shield-evaluate.json`
- `shield-badge.json`
- `shield-badge.svg`
- command logs

Forbidden artifacts:

- source code from a private repository;
- raw diffs;
- prompts;
- model responses;
- secrets;
- credentials;
- customer payloads.

## Non-Claims

This workflow is not a model safety benchmark, not a hosted verification service, not a public certification process, not a public leaderboard, and not an operating-system sandbox proof.

It is not an operating-system sandbox proof and does not rate AI models. It is a reproducible local AI coding workflow governance self-check and packaged-install proof.
