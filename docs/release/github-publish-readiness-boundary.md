# GitHub Publish Readiness Boundary

Status: Phase 5 boundary freeze.

## Meaning

GitHub publish readiness means the repository has enough local proof, public docs, public hygiene files, release notes, and dry-run artifacts for an operator to decide whether to publish the CARVES matrix on GitHub.

It does not mean publication has happened.

## Implementation-Completable Checks

The implementation side can complete:

- public README and docs links;
- license, contribution, security, code of conduct, issue templates, and PR template;
- matrix proof workflow;
- local project-mode matrix smoke;
- local packaged-install matrix smoke;
- local release bundle manifest dry-run;
- package metadata review;
- release draft text;
- known limitations;
- trust-chain hardening checkpoint;
- final checkpoint.

## Operator-Gated Actions

The following actions remain operator-gated:

- make or confirm repository public visibility;
- create a Git tag;
- create a GitHub release;
- upload release assets;
- push packages to NuGet.org;
- sign packages;
- approve package signing risk exceptions;
- approve any hosted verification service;
- approve any public leaderboard;
- approve any certification or verified status language.

## Current Public Claim

CARVES is publish-ready as a local AI coding workflow governance self-check:

- Guard checks AI patch boundaries.
- Handoff records continuation packets.
- Audit discovers local summary evidence.
- Shield evaluates `shield-evidence.v0` and renders a badge.

The stable Shield input remains `shield-evidence.v0`.

## Non-Claims

The current line does not claim:

- operating-system sandboxing;
- model safety benchmarking;
- syscall interception;
- network isolation;
- automatic rollback;
- hosted verification;
- public certification;
- public leaderboard ranking;
- semantic source-code correctness;
- public package publication;
- package signing.

## Artifact Privacy

Default readiness artifacts are summary-only. They must not include private source, raw diffs, prompts, model responses, secrets, credentials, customer payloads, or environment variable values.
