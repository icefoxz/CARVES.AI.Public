# GitHub Release Draft

Status: copyable draft only. No Git tag or GitHub release has been created by this document.

## Release Title

CARVES Runtime 0.6.2-beta Public Source Snapshot

## Tag Candidate

```text
carves-runtime-v0.6.2-beta
```

Creating this tag is an operator action.

## Release Body

````markdown
# CARVES Runtime 0.6.2-beta Public Source Snapshot

CARVES Runtime is a local AI coding workflow governance self-check for starting, binding, inspecting, and checking agent work in a project.

This is a beta public source snapshot. It is suitable for local source inspection, local build, and bounded local startup experiments. It is not a hosted service, signed release, complete autonomous agent platform, API/SDK worker execution authority, certification authority, public leaderboard, or model safety benchmark.

## What Is Included

- Runtime-first source snapshot for the `0.6` startup and visible gateway line.
- Local `carves` CLI source and release dist wrapper path.
- Guard, Handoff, Audit, Shield, and Matrix local governance capability source.
- Public README, safe agent entry, docs index, release notes, and known limitations.
- GitHub Actions CI and Matrix proof workflows.
- Local package/readiness proof script output when attached as release assets.

## What Is Checked

- `ci` on Ubuntu and Windows.
- `CARVES Matrix Proof` on Ubuntu and Windows.
- Guard Beta proof lane scoped for the public source snapshot.
- Shield GitHub Actions proof lane.
- Public snapshot tests.
- Matrix native proof, full-release shadow proof, proof verification, external pilot verification, and Windows `SCORE.cmd` smoke.

## What Is Verified

- `matrix-artifact-manifest.json` required entries, hashes, sizes, producers, paths, schemas, and summary-only privacy flags.
- `matrix-proof-summary.json` consistency with manifest hash, verification posture, proof mode, proof capabilities, privacy posture, public non-claims, and native proof readback fields.
- `matrix-verify` readback on GitHub Actions and local verification paths.
- Shield evaluation readback shape with `shield-evidence.v0`, local self-check output, and `certification=false`.
- Public release assets can include local package proof metadata from `github-publish-readiness-manifest.json`; those assets do not imply NuGet.org publication or signing.

## Local First Run

Build from source:

```bash
dotnet build CARVES.Runtime.sln --configuration Release
```

Inspect the local CLI and gateway surface:

```bash
./carves help
./carves gateway status
```

Start a target project through the safe entry:

```bash
./carves up <target-project>
```

Then follow the generated target-project `CARVES_START.md` / `.carves/carves agent start --json` readback.

## Asset Checklist

Recommended release assets after operator approval:

- `CARVES.Runtime-0.6.2-beta.zip`
- `github-publish-readiness-manifest.json`
- `checksums.txt`
- local `.nupkg` package files from the readiness manifest

The `.nupkg` files are GitHub release assets only unless separately pushed to NuGet.org by an operator. They are not signed unless a signing record is explicitly attached.

## What Is Not Claimed

This release is local self-check only. It is not a model safety benchmark.

It does not claim hosted verification, public certification, certification authority status, public leaderboard availability, model safety benchmarking, semantic source-code correctness proof, operating-system sandboxing, automatic rollback, package signing, NuGet.org publication, source upload, raw diff upload, prompt upload, model response upload, secret upload, credential upload, or customer payload upload.

NuGet.org publication, package signing, repository visibility changes, tag creation, and GitHub release creation remain operator-gated actions.
````

## Operator Checklist

Before creating the GitHub release:

1. Confirm source commit SHA.
2. Review `docs/release/matrix-operator-release-gate.md`.
3. Confirm tag name: `carves-runtime-v0.6.2-beta`.
4. Confirm `ci` and `CARVES Matrix Proof` are green for the release commit.
5. Run `git diff --check`.
6. Run `pwsh ./scripts/release/github-publish-readiness.ps1 -ArtifactRoot artifacts/release/github-publish-readiness`.
7. Run `pwsh ./scripts/pack-runtime-dist.ps1 -Version 0.6.2-beta -Force`.
8. Run `pwsh ./scripts/assert-runtime-release-dist.ps1 -DistRoot .dist/CARVES.Runtime-0.6.2-beta`.
9. Confirm package versions, sizes, and SHA-256 values.
10. Decide whether packages are signed or explicitly accepted as unsigned.
11. Decide whether NuGet.org publication happens now or remains deferred.
12. Confirm SECURITY.md reporting instructions.
13. Confirm no artifact contains private source, raw diff, prompt, model response, secret, credential, or private payload.
14. Create tag only after the above checks pass.
15. Create GitHub pre-release only after the tag is accepted.

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
