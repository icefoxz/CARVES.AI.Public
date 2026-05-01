# Matrix Operator Release Gate

Status: release gate checklist for Matrix public publication.

## Publication State

- Git tag: not created.
- GitHub release: not created.
- NuGet.org packages: not pushed.
- Package signing: not performed.
- Local verification work remains valid only as local operator evidence until publication is approved.

## Required Evidence Before Publication

- source commit SHA
- matrix-verify.json
- matrix-artifact-manifest.json
- matrix-proof-summary.json
- Shield Lite starter challenge smoke output
- shield-evidence.v0
- cross-platform Matrix verify pilot output
- package ids, versions, sizes, and SHA-256 values
- git diff --check

## Operator Actions

- Create the Git tag only after the evidence above is accepted.
- Create the GitHub release only after the tag is accepted.
- Push packages to NuGet.org only after token, owner, and signing decisions are accepted.
- Record any deferred operator gate explicitly.

## Non-Automatic Gate

`github-publish-readiness.ps1` must not use GitHub tokens or NuGet tokens. The script cannot perform tag creation, GitHub release creation, NuGet.org package push, or package signing.

## Deferral Outcomes

- defer_publication
- defer_nuget
- defer_signing

Deferral is not a failed Matrix verification result.

## Required Publication Record

The operator publication record should list the source commit, accepted local artifacts, final package filenames, checksums, tag decision, release decision, NuGet.org decision, and signing decision.

## Public Non-Claims

This gate does not claim hosted verification, public leaderboard availability, certification, public certification, model safety benchmarking, semantic correctness proof, or operating-system sandboxing.

It also does not claim raw diff upload, secret upload, hosted verification, public certification, semantic source-code correctness, or operating-system sandboxing.
