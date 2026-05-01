# GitHub Release Draft

Status: copyable draft only. No Git tag or GitHub release has been created by this document.

## Release Title

CARVES Matrix 0.1.0 RC1

## Tag Candidate

```text
matrix-v0.1.0-rc.1
```

Creating this tag is an operator action.

## Release Body

````markdown
# CARVES Matrix 0.1.0 RC1

CARVES is a local AI coding workflow governance self-check for AI-generated code changes before review or merge.

- Guard checks patch boundaries.
- Handoff records continuation context.
- Audit gathers local evidence.
- Shield turns `shield-evidence.v0` into G/H/A levels, Lite self-check score, and an SVG badge.
- Matrix proves the local Guard -> Handoff -> Audit -> Shield composition path.

## What Is Included

- Local `carves-matrix` proof shell and matrix proof scripts.
- GitHub Actions matrix proof and verify workflow.
- Public README and docs.
- Local package bundle dry-run manifest.
- Trust-chain hardening checkpoint summary.
- Known limitations and operator gate language.

## What Is Checked

- Guard policy initialization and patch decision summaries.
- Handoff continuity packet output.
- Audit summary, timeline, explain, and `shield-evidence.v0`.
- Shield Standard G/H/A, Lite score/band, badge JSON, and badge SVG output.
- Matrix proof summary and artifact manifest generation.

## What Is Verified

- `matrix-artifact-manifest.json` required entries, hashes, sizes, producers, paths, schemas, and summary-only privacy flags.
- `matrix-proof-summary.json` consistency with the manifest hash, verification posture, proof mode, proof capabilities, privacy posture, public non-claims, and native proof readback fields.
- Shield evaluation readback shape with local self-check output and `certification=false`.
- GitHub Actions proof and verify behavior on Windows and Linux.

## Local Proof

Run:

```powershell
pwsh ./scripts/matrix/matrix-proof-lane.ps1 -ArtifactRoot artifacts/matrix/local
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj -- verify artifacts/matrix/local --json
pwsh ./scripts/release/github-publish-readiness.ps1 -ArtifactRoot artifacts/release/github-publish-readiness -AllowDirty
```

## What Is Not Claimed

This release candidate is local self-check only. It is not a model safety benchmark, operating-system sandbox, hosted verification service, public leaderboard, public certification, or certification authority.

It does not claim semantic source-code correctness, automatic rollback, package signing, NuGet.org publication, source upload, raw diff upload, prompt upload, model response upload, secret upload, credential upload, or customer payload upload.

NuGet.org publication, package signing, repository visibility changes, tag creation, and GitHub release creation are operator-gated actions.
````

## Asset Checklist

Recommended release assets after operator approval:

- `github-publish-readiness-manifest.json`
- locally built `.nupkg` files from the manifest
- `matrix-proof-summary.json`
- `matrix-artifact-manifest.json`
- `matrix-verify.json`
- `matrix-summary.json`
- `matrix-packaged-summary.json`
- `shield-evidence.json`
- `shield-evaluate.json`
- `shield-badge.svg`

Do not upload private source, raw diffs, prompts, model responses, secrets, credentials, customer payloads, or environment variable values.

## Operator Checklist

Before creating the GitHub release:

1. Review `docs/release/matrix-operator-release-gate.md`.
2. Confirm repository visibility.
3. Confirm tag name.
4. Confirm package versions and SHA-256 values.
5. Decide whether packages are signed or explicitly accepted as unsigned.
6. Decide whether NuGet.org publication happens now or remains deferred.
7. Confirm SECURITY.md reporting instructions.
8. Confirm no artifact contains source, raw diff, prompt, model response, secret, credential, or private payload.
9. Create tag only after the above checks pass.
10. Create GitHub release only after the tag is accepted.

## Non-Performed Actions

This draft did not:

- create a Git tag;
- create a GitHub release;
- upload release assets;
- push packages to NuGet.org;
- sign packages;
- call GitHub APIs;
- call NuGet APIs;
- publish hosted verification;
- publish a public leaderboard;
- grant certification.
