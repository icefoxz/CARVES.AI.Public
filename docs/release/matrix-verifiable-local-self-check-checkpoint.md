# Matrix Verifiable Local Self-Check Checkpoint

## Verdict

Ready for operator-controlled GitHub publication as a local self-check only source snapshot. No unresolved P0/P1 verification debt is hidden by this checkpoint.

## What Is Checked

Matrix runs local Guard, Handoff, Audit, and Shield steps and writes a summary-only bundle. The bundle contains `matrix-artifact-manifest.json`, `matrix-proof-summary.json`, and local evidence references such as `shield-evidence.v0`.

## What Is Verified

`carves-matrix verify` is the Linux-native public artifact recheck path. It verifies the existing bundle without rerunning the proof chain, invoking `pwsh`, or entering repo-local release lanes.

## Required Command Set

```bash
dotnet build CARVES.Runtime.sln --configuration Release
dotnet test CARVES.Runtime.sln --configuration Release --filter Matrix --no-restore
carves-matrix proof --lane native-minimal --artifact-root artifacts/matrix/native --configuration Release --json
carves-matrix verify artifacts/matrix/native --json
carves-matrix verify artifacts/matrix/local --json
pwsh ./scripts/matrix/matrix-proof-lane.ps1
pwsh ./scripts/shield/shield-lite-starter-challenge-smoke.ps1
pwsh ./scripts/matrix/matrix-external-pilot-set.ps1
pwsh ./scripts/matrix/matrix-cross-platform-verify-pilot.ps1
pwsh ./scripts/release/github-publish-readiness.ps1
git diff --check
```

## Current Local Readback

The verifier checks summary-only artifacts and records `certification=false`. These are local challenge results, not certification.

## Remaining Operator Gates

Operator action is still required for tags, checksums, GitHub Releases, NuGet.org publication, package signing, and any hosted release evidence.

## Remaining Limitations

This is local workflow governance evidence, not a hosted verifier.

## Public Non-Claims

No model safety benchmarking, hosted verification, public certification, public leaderboard, semantic source-code correctness, operating-system sandboxing, source upload, raw diff upload, prompt upload, or secret upload is provided.
