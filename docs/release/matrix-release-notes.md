# CARVES Matrix Release Notes

Release candidate date: 2026-04-14

## Summary

The CARVES public matrix is now organized around five local tools:

- Guard: patch admission gate for AI-generated code changes.
- Handoff: continuation packet for the next human or agent.
- Audit: local evidence discovery layer.
- Shield: local G/H/A, Lite self-check, and badge layer over `shield-evidence.v0`.
- Matrix: local composition proof shell for the four peer products.

The release candidate proves the full Guard -> Handoff -> Audit -> Shield workflow in a temporary external git repository.

The Matrix proof records the Shield evidence, Shield evaluation, and badge artifacts that passed through the lane. Matrix remains an orchestrator and does not alter Shield scoring.

## What Is Checked

The public proof checks that the local tools compose in a normal git repository:

- Guard initializes policy and writes patch decision summaries.
- Handoff writes a session continuity packet.
- Audit reads Guard and Handoff outputs, then emits `shield-evidence.v0`.
- Shield evaluates `shield-evidence.v0`, projects Standard G/H/A and Lite output, and renders badge metadata/SVG.
- Matrix orchestrates the chain, records the summary artifact set, and keeps Shield as the scoring owner.

The hardening categories covered by this release candidate are:

- Audit evidence integrity, timeline, summary, and explainability checks.
- Guard deletion/replacement honesty plus decision-store append and retention durability.
- Shield evidence contract alignment, Lite/Standard self-check boundary, and badge path consistency.
- Handoff terminal-state semantics, reference freshness, and portability checks.
- Matrix-to-Shield provenance linkage, large-log boundaries, output-path boundaries, and release checkpoint coverage.
- Guard, Audit, and Shield usability plus coverage cleanup for the local proof lane.

## What Is Verified

Matrix verify checks an existing summary-only proof bundle without rerunning Guard, Handoff, Audit, Shield, or the proof lane. It validates:

- `matrix-artifact-manifest.json` shape, required artifact entries, producers, schema names, paths, SHA-256 values, and file sizes.
- `matrix-proof-summary.json` consistency with the manifest hash, verification posture, proof mode, proof capabilities, privacy posture, public non-claims, and native proof readback fields.
- summary-only privacy flags for the bundle and each manifest entry.
- Shield evaluation readback shape, including local self-check posture, Standard label, Lite score/band fields, and `certification=false`.
- verifier gates under `trust_chain_hardening`.

The GitHub Actions workflow now separates the Linux-native public minimum, the Linux-native full-release shadow, and the full PowerShell release proof. The Linux-native minimum lane runs `carves-matrix proof --lane native-minimal --json` and `carves-matrix verify --json` on Ubuntu with bash and uploads `matrix-native-proof.json`, `matrix-verify.json`, `matrix-artifact-manifest.json`, and `matrix-proof-summary.json` as summary-only artifacts. The Linux-native full-release shadow runs `carves-matrix proof --lane native-full-release --json` and Matrix verify as a non-blocking shadow. It is not the default proof lane and is not certification-grade evidence. The full PowerShell proof lane remains available for project plus packaged release evidence and compatibility.

## Package Posture

| Tool | Package | Version | Public registry posture |
| --- | --- | --- | --- |
| `carves-guard` | `CARVES.Guard.Cli` | `0.2.0-beta.1` | local package proof; NuGet.org push remains operator-gated |
| `carves-handoff` | `CARVES.Handoff.Cli` | `0.1.0-alpha.1` | local package proof; NuGet.org push remains operator-gated |
| `carves-audit` | `CARVES.Audit.Cli` | `0.1.0-alpha.1` | local package proof; NuGet.org push remains operator-gated |
| `carves-shield` | `CARVES.Shield.Cli` | `0.1.0-alpha.1` | local package proof; NuGet.org push remains operator-gated |
| `carves-matrix` | `CARVES.Matrix.Cli` | `0.2.0-alpha.1` | local package proof; NuGet.org push remains operator-gated |

## Proof Lanes

| Lane | Script | Purpose |
| --- | --- | --- |
| Project E2E | `scripts/matrix/matrix-e2e-smoke.ps1` | Runs the matrix directly from local CLI projects. |
| Packaged install | `scripts/matrix/matrix-packaged-install-smoke.ps1` | Packs local `.nupkg` files, installs dotnet tools, then runs the same matrix chain. |
| CI proof | `scripts/matrix/matrix-proof-lane.ps1` | Runs both lanes and writes a summary-only artifact bundle. |
| GitHub Actions native minimum | `.github/workflows/matrix-proof.yml` | Runs `carves-matrix proof --lane native-minimal --json` and Matrix verify on Ubuntu without invoking PowerShell. |
| GitHub Actions native full-release shadow | `.github/workflows/matrix-proof.yml` | Runs `carves-matrix proof --lane native-full-release --json` and Matrix verify on Ubuntu as a non-blocking shadow while parity evidence stabilizes. |
| GitHub Actions full proof | `.github/workflows/matrix-proof.yml` | Runs the PowerShell proof lane and Matrix verify as full release evidence. |

## Expected Artifacts

- Guard decision JSON and `decisions.jsonl`
- Handoff packet JSON
- Audit summary, timeline, explain, and `shield-evidence.v0`
- Shield evaluation JSON
- Shield badge JSON and SVG
- Matrix proof summary, artifact manifest, and verification result
- command logs

Artifacts are summary-only and must not include private source, raw diffs, prompts, model responses, secrets, credentials, or customer data.

## Public Self-Check Claim Boundary

The current public posture is local AI coding workflow governance self-check only. The hardening and verifier gates reduce local self-check evidence inflation risk and make the Matrix proof output record `trust_chain_hardening.gates_satisfied=true`, but they do not create model safety benchmarking, certification, hosted verification, public ranking, semantic correctness, automatic rollback, or operating-system sandboxing claims. Internal checkpoint documents retain exact CARD traceability for operator review.

## Known Limitations

See `docs/matrix/known-limitations.md`.

## What Is Not Claimed

This release candidate remains local self-check only. It is not a model safety benchmark.

This release candidate does not claim:

- model safety benchmarking;
- operating-system sandboxing;
- hosted verification;
- public leaderboard ranking;
- public certification;
- semantic source-code correctness;
- automatic rollback;
- NuGet.org publication;
- package signing;
- source, raw diff, prompt, model response, secret, credential, or customer payload upload.

NuGet.org publication is operator-gated. The stable Shield input is `shield-evidence.v0` generated by `carves-audit evidence`.

## How To Reproduce

From a local checkout:

```powershell
dotnet build CARVES.Runtime.sln --configuration Release
pwsh ./scripts/matrix/matrix-proof-lane.ps1 -ArtifactRoot artifacts/matrix/local -Configuration Release
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release -- verify artifacts/matrix/local --json
pwsh ./scripts/release/github-publish-readiness.ps1 -ArtifactRoot artifacts/release/github-publish-readiness -AllowDirty
```

For CI, use `.github/workflows/matrix-proof.yml`. The Linux-native public minimum is `matrix-native-proof-linux`; the Linux native full-release shadow is `matrix-native-full-release-shadow-linux`; the full release proof remains in the PowerShell proof job. The shadow job is non-blocking, is not the default release lane, and is not certification-grade evidence. Windows native full-release shadow remains deferred until packaged local tool install, command shim behavior, executable path resolution, and native atomic promotion have separate Windows parity evidence. All lanes upload only summary-only proof and verification artifacts and do not publish packages, tags, releases, hosted verification, leaderboard entries, or certification claims.
