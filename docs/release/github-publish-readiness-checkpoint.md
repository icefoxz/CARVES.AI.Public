# GitHub Publish Readiness Checkpoint

Status: operator review checkpoint for publishing the public source snapshot.

Checkpoint id: `carves-github-publish-readiness.v1`.
Trace reference: CARD-805.
Status language: ready for an operator to publish after local evidence review.

Required operator checks:

- Release build passes.
- Public tests selected for the snapshot pass.
- `scripts/release/github-publish-readiness.ps1` passes.
- Post-product-extraction-readiness evidence includes `docs/release/product-extraction-readiness-checkpoint.md`.
- Shield input remains `shield-evidence.v0`.
- Operator gate document: `docs/release/matrix-operator-release-gate.md`.
- Generated artifacts stay out of git.
- No source, raw diff, prompt, secret, credential, hosted verification, public leaderboard, certification, or operating-system sandbox claim is introduced.

This checkpoint is not itself a GitHub Release, tag, NuGet.org publication, hosted verification service, public certification, or package signature.
