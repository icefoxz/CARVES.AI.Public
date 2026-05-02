# CARVES Public Docs Index

Language: [中文](INDEX.zh-CN.md)

This index keeps the Runtime-first path at the top so a new reader can start with the local Runtime CLI and current limits.

CARVES Runtime is the public entrypoint for this snapshot. Guard / Handoff / Audit / Shield / Matrix are Runtime governance capabilities, not separate first entries.

## Runtime Entry

Use these Runtime entrypoints first:

- `../README.md`
- `../START_CARVES.md`
- `runtime/runtime-project-recenter-carves-g0-visible-gateway-contract.md`
- `release/runtime-versioning-policy.md`
- `release/runtime-0.6.2-beta-release-identity.md`
- `release/runtime-0.6.2-beta-version-bom.md`

The first public path is source build, CLI/gateway readback, then `carves up <target-project>`. New users should not need Runtime CARD, TaskGraph, or Host truth internals before that path.

## Public Matrix Entry And Runtime Capability Docs

Use these public product entrypoints after the Runtime entry is clear:

- `guard/README.md`
- `handoff/README.md`
- `audit/README.md`
- `shield/README.md`
- `matrix/README.md`
- `matrix/public-boundary.md`
- `matrix/quickstart.en.md`
- `matrix/quickstart.zh-CN.md`
- `release/matrix-release-notes.md`
- `matrix/known-limitations.md`
- `matrix/github-actions-proof.md`
- `matrix/packaged-install-matrix.md`
- `guides/public-surface-size-advisory.md`

These pages describe local Runtime capability behavior. They should not present Guard / Handoff / Audit / Shield / Matrix as separate first entries, and they should avoid requiring Runtime CARD/TaskGraph concepts before a public user can run the local self-check path.

## Internal Checkpoints And Operator Review

These pages are release evidence for maintainers. They are not the beginner entrypoint. They keep CARD traceability, operator review, release gate, and non-claim boundaries visible without making Runtime CARD/TaskGraph concepts part of the first public path.

- `release/matrix-github-release-candidate-checkpoint.md`
- `release/trust-chain-hardening-release-checkpoint.md`
- `release/github-publish-readiness-boundary.md`
- `release/github-release-draft.md`
- `release/github-publish-readiness-checkpoint.md`
- `release/product-extraction-readiness-checkpoint.md`
- `release/matrix-verifiable-local-self-check-checkpoint.md`
- `release/matrix-operator-release-gate.md`

Publication still requires operator action for tags, release artifacts, checksums, package pushes, and hosted release pages.
