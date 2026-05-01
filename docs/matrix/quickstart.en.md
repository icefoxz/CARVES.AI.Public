# CARVES Matrix Beginner Quickstart

CARVES Matrix runs the local Guard -> Handoff -> Audit -> Shield local consistency proof chain and writes a summary-only proof bundle that you can verify later.

Use this guide when you want to run the native first-run local workflow self-check, inspect the summary-only proof bundle, read the Shield badge, and understand the current limits.

If you want your own AI agent to run a standard local task and receive a local Agent Trial score, use [Agent Trial V1 Local Quickstart](agent-trial-v1-local-quickstart.md).

## Prerequisites

- A .NET SDK that can build the repository target frameworks.
- Git available on `PATH`.
- A local checkout of this repository.
- PowerShell 7 or newer only if you want the full release proof or packaged-install smoke lane.

No hosted service, NuGet.org push, source upload, raw diff upload, prompt upload, model response upload, secret upload, or credential upload is required for this quickstart. The bundle does not establish producer identity, signatures, a transparency log, hosted verification, certification, benchmarking, OS sandboxing, or semantic correctness proof.

## Build The Native Matrix CLI

From the repository root:

```bash
dotnet build ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release
```

This builds the local Matrix CLI project and its Guard, Handoff, Audit, and Shield runner dependencies.

## Run The Native Minimal Proof

Run the Linux-native first-run proof:

```bash
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- proof --lane native-minimal --artifact-root artifacts/matrix/native-quickstart --configuration Release --json
```

This creates a bounded temporary external git repository, runs Guard, Handoff, Audit, and Shield through the local .NET runners, then writes summary-only Matrix artifacts under `artifacts/matrix/native-quickstart`. It does not invoke `pwsh` or `scripts/matrix/*.ps1`.

Expected top-level native outputs:

```text
artifacts/matrix/native-quickstart/matrix-proof-summary.json
artifacts/matrix/native-quickstart/matrix-artifact-manifest.json
```

Expected native project outputs:

```text
artifacts/matrix/native-quickstart/project/decisions.jsonl
artifacts/matrix/native-quickstart/project/handoff.json
artifacts/matrix/native-quickstart/project/shield-evidence.json
artifacts/matrix/native-quickstart/project/shield-evaluate.json
artifacts/matrix/native-quickstart/project/shield-badge.json
artifacts/matrix/native-quickstart/project/shield-badge.svg
artifacts/matrix/native-quickstart/project/matrix-summary.json
```

## Verify The Native Bundle

Verify the artifact bundle without rerunning Guard, Handoff, Audit, Shield, or any proof script:

```bash
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- verify artifacts/matrix/native-quickstart --json
```

`verify` is the Linux-native public recheck path. It emits `matrix-verify.v0`, reads existing local files, and does not invoke `pwsh`, Matrix proof scripts, Guard, Handoff, Audit, or Shield.

The public proof summary uses a closed public contract. Unknown public fields fail verification, and native summary fields, full release summary fields, and Shield evaluation fields are trusted only after manifest-bound verified reads.

Public JSON examples live under `docs/matrix/examples/`. Files named `.schema-example.json` are schema examples, not runnable verification bundles; use them to inspect the public shape, not as input to `verify`. Runnable example bundles, when present, use a `.runnable-bundle` directory name and include every referenced artifact file.

Important fields:

- `status`: `verified` only when the bundle passes verification.
- `proof_capabilities`: lane, backend, coverage, and environment requirements for the proof bundle.
- `reason_codes`: stable failure families for automation, such as `missing_artifact`, `hash_mismatch`, `schema_mismatch`, `privacy_violation`, `unverified_score`, or `unsupported_version`.
- `issues`: exact verifier diagnostics.
- `trust_chain_hardening.gates_satisfied`: whether the current local verifier gates passed.

Exit codes:

- `0`: bundle verified.
- `1`: verification failed.
- `2`: usage or argument error.

## Installed Tool Form

After installing `carves-matrix`, the same native first-run path is:

```bash
carves-matrix proof --lane native-minimal --artifact-root artifacts/matrix/native-quickstart --configuration Release --json
carves-matrix verify artifacts/matrix/native-quickstart --json
```

## Full Release Proof Lane

Use the PowerShell lane when you specifically need the full project-mode plus packaged-install release evidence:

```powershell
pwsh ./scripts/matrix/matrix-proof-lane.ps1 -ArtifactRoot artifacts/matrix/quickstart-release -Configuration Release
```

That full release proof writes:

```text
artifacts/matrix/quickstart-release/project-matrix-output.json
artifacts/matrix/quickstart-release/packaged-matrix-output.json
artifacts/matrix/quickstart-release/matrix-proof-summary.json
artifacts/matrix/quickstart-release/matrix-artifact-manifest.json
```

PowerShell scripts are release proof and packaged smoke lanes, not the Linux first-run requirement.

## Optional Packaged Smoke

If you want standalone commands available in the current shell before public package publication, keep the temporary install directory from the packaged smoke:

```powershell
$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) "carves-matrix-tools"
pwsh ./scripts/matrix/matrix-packaged-install-smoke.ps1 -WorkRoot $workRoot -ArtifactRoot artifacts/matrix/quickstart-packaged -Configuration Release -Keep
$env:PATH = "$(Join-Path $workRoot "tool")$([System.IO.Path]::PathSeparator)$env:PATH"
carves-matrix help
```

That script builds local `.nupkg` files and installs these dotnet tools from the local package directory:

| Package | Command |
| --- | --- |
| `CARVES.Guard.Cli` | `carves-guard` |
| `CARVES.Handoff.Cli` | `carves-handoff` |
| `CARVES.Audit.Cli` | `carves-audit` |
| `CARVES.Shield.Cli` | `carves-shield` |
| `CARVES.Matrix.Cli` | `carves-matrix` |

These local packages are not a NuGet.org publication.

## Read The Shield Badge

The native proof writes:

```text
artifacts/matrix/native-quickstart/project/shield-badge.svg
artifacts/matrix/native-quickstart/project/shield-badge.json
```

The visible badge text comes from Shield Lite. The badge metadata keeps Shield Standard dimensions such as `G4.H3.A5` or stronger values depending on the local evidence.

Read it as a local workflow governance self-check:

- G = Guard evidence.
- H = Handoff evidence.
- A = Audit evidence.
- Lite score and band are for quick reading.
- `self_check=true` and `certification=false` mean the badge is not certification.

For exact term definitions, see the [Shield glossary](../shield/wiki/glossary.en.md).

Do not describe the badge as certified, verified safe, a model safety rating, or a security audit.

## Run The Chain Manually In Your Own Repository

After installing the tools, run this from the git repository you want to check:

```powershell
carves-guard init
carves-guard check --json
carves-handoff draft --json
carves-audit summary --json
carves-audit timeline --json
carves-audit evidence --json --output .carves/shield-evidence.json
carves-shield evaluate .carves/shield-evidence.json --json --output combined
carves-shield badge .carves/shield-evidence.json --json --output docs/shield-badge.svg
carves-matrix proof --lane native-minimal --artifact-root artifacts/matrix/local --configuration Release --json
carves-matrix verify artifacts/matrix/local --json
```

Use normal human review for any Guard `review` or `block` decision. Matrix records the local consistency proof; it does not decide whether your patch should merge.

## Product Entrypoints

- Guard: [docs/guard/README.md](../guard/README.md) and [Guard quickstart](../guard/quickstart.en.md)
- Handoff: [docs/handoff/README.md](../handoff/README.md) and [Handoff quickstart](../handoff/quickstart.en.md)
- Audit: [docs/audit/README.md](../audit/README.md) and [Audit quickstart](../audit/quickstart.en.md)
- Shield: [docs/shield/README.md](../shield/README.md), [badge guide](../shield/wiki/badge.en.md), and [Shield Lite starter challenge](../shield/lite-challenge-quickstart.md)
- Matrix: [docs/matrix/README.md](README.md), [artifact manifest](matrix-artifact-manifest-v0.md), [GitHub Actions proof](github-actions-proof.md), [large-log stress limits](large-log-stress.md), and [known limitations](known-limitations.md)

## Current Limits

Matrix is local-first and summary-only. The current proof and verifier do not provide:

- producer identity;
- signatures;
- transparency-log backing;
- model safety benchmarking;
- hosted verification;
- public certification;
- public leaderboard ranking;
- operating-system sandboxing;
- syscall interception;
- real-time file write prevention;
- network isolation;
- automatic rollback;
- semantic source-code correctness proof;
- source code upload;
- raw diff upload;
- prompt or model response upload;
- secret or credential upload.

Large Guard decision histories are read through a bounded Audit tail window of 1000 lines. Oversized Guard decision records above 131072 bytes are skipped with explicit diagnostics. Matrix artifact manifests keep hashes and sizes for large artifacts, but they do not inline large artifact bytes.
