# CARVES Public Docs Index

This index separates public product entrypoints from internal operator review checkpoints. The public docs should let a new reader start with local tools and current limits without needing private operating history.

Guard / Handoff / Audit / Shield / Matrix are the public product entrypoints for this snapshot.

## Public Matrix Entry

Use these public product entrypoints first:

- `matrix/README.md`
- `matrix/public-boundary.md`
- `matrix/quickstart.en.md`
- `matrix/quickstart.zh-CN.md`
- `release/matrix-release-notes.md`
- `matrix/known-limitations.md`
- `matrix/github-actions-proof.md`
- `matrix/packaged-install-matrix.md`
- `guard/README.md`
- `handoff/README.md`
- `audit/README.md`
- `shield/README.md`
- `guides/public-surface-size-advisory.md`

These pages describe local Guard / Handoff / Audit / Shield / Matrix behavior. They should avoid requiring Runtime CARD/TaskGraph concepts before a public user can run the local self-check path.

## Internal Checkpoints And Operator Review

The release checkpoint pages are operator review evidence, not the beginner entrypoint. They retain CARD traceability where needed for maintainers, but they should not be the first path for new users.

- `release/matrix-github-release-candidate-checkpoint.md`
- `release/trust-chain-hardening-release-checkpoint.md`
- `release/github-publish-readiness-boundary.md`
- `release/github-release-draft.md`
- `release/github-publish-readiness-checkpoint.md`
- `release/product-extraction-readiness-checkpoint.md`
- `release/matrix-verifiable-local-self-check-checkpoint.md`
- `release/matrix-operator-release-gate.md`

Publication still requires operator action for tags, release artifacts, checksums, package pushes, and hosted release pages.
