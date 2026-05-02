# CARVES Public Docs Index

Language: [Chinese](INDEX.zh-CN.md)

This index separates the Runtime-first public path from internal operator review checkpoints. The public docs should let a new reader start with the local Runtime CLI and current limits without needing private operating history.

CARVES Runtime is the public entrypoint for this snapshot. Guard / Handoff / Audit / Shield / Matrix are Runtime governance capabilities, not separate first entries.

## Runtime Entry

Use these Runtime entrypoints first:

- `../README.md`
- `../START_CARVES.md`
- `runtime/runtime-project-recenter-carves-g0-visible-gateway-contract.md`
- `release/runtime-versioning-policy.md`
- `release/runtime-0.6.1-beta-release-identity.md`
- `release/runtime-0.6.1-beta-version-bom.md`

The first public path is source build, CLI/gateway readback, then `carves up <target-project>`. New users should not need Runtime CARD, TaskGraph, or Host truth internals before that path.

## Runtime Capability Docs

Use these capability docs after the Runtime entry is clear:

- `guard/README.md`
- `handoff/README.md`
- `audit/README.md`
- `shield/README.md`
- `matrix/README.md`
- `matrix/public-boundary.md`
- `matrix/quickstart.en.md`
- `release/matrix-release-notes.md`
- `matrix/known-limitations.md`
- `matrix/github-actions-proof.md`
- `matrix/packaged-install-matrix.md`
- `guides/public-surface-size-advisory.md`

These pages describe local Runtime capability behavior. They should not present Guard / Handoff / Audit / Shield / Matrix as separate first entries, and they should avoid requiring Runtime CARD/TaskGraph concepts before a public user can run the local self-check path.

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
