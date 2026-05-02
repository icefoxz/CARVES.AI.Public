# CARVES Shield

Language: [Chinese](README.zh-CN.md)

CARVES Shield is a CARVES Runtime capability for local-first AI governance self-checks in repositories.

It helps a project answer one practical question:

```text
Can this repository show evidence that AI-assisted code work is bounded, handed off clearly, and auditable?
```

Shield v0 is intentionally simple:

- `carves-shield evaluate` reads `shield-evidence.v0` summary evidence.
- `carves-shield badge` renders a local static SVG badge.
- the Runtime `carves` tool still exposes the compatibility wrapper `carves shield ...`.
- The GitHub Actions proof lane writes local artifacts for pull request inspection.
- Guard CI evidence from Audit is heuristic workflow-text evidence, not proof that a hosted CI service ran.
- No source code, raw diffs, prompts, model responses, secrets, credentials, or private file payloads are uploaded by default.
- v0 output is a self-check. It is not a model safety benchmark, hosted verification, public ranking, certification, source review, semantic correctness proof, or operating-system sandboxing.

## Start Here

- [Shield Wiki Home](wiki/README.md)
- [Beginner guide: run your first Shield self-check](wiki/shield-beginner-guide.en.md)
- [Glossary: what every keyword means](wiki/glossary.en.md)
- [Workflow diagrams: local, self-check, and CI](wiki/workflow.en.md)
- [Evidence starter: prepare shield-evidence.v0](wiki/evidence-starter.en.md)
- [GitHub Actions integration](wiki/github-actions.en.md)
- [Badge guide](wiki/badge.en.md)

## Stable v0 Commands

```powershell
carves-shield evaluate <evidence-path> [--json] [--output <lite|standard|combined>]
carves-shield badge <evidence-path> [--json] [--output <svg-path>]
carves-shield challenge <challenge-pack-path> [--json]
```

Compatibility wrapper:

```powershell
carves shield evaluate <evidence-path> [--json] [--output <lite|standard|combined>]
carves shield badge <evidence-path> [--json] [--output <svg-path>]
carves shield challenge <challenge-pack-path> [--json]
```

## Specification Docs

- [CARVES Shield Matrix Boundary v0](matrix-boundary-v0.md)
- [CARVES Shield Evidence Schema v0](evidence-schema-v0.md)
- [CARVES Shield Standard G/H/A Rubric v0](standard-gha-rubric-v0.md)
- [CARVES Shield Lite Scoring Model v0](lite-scoring-model-v0.md)
- [CARVES Shield Lite Challenge Schema v0](lite-challenge-schema-v0.md)
- [Shield Lite Starter Challenge Quickstart](lite-challenge-quickstart.md)
- [CARVES Shield API Privacy Contract v0](api-privacy-contract-v0.md)
- [CARVES Shield Local Evaluate Prototype v0](local-evaluate-prototype-v0.md)
- [CARVES Shield Badge Output v0](badge-output-v0.md)
- [CARVES Shield GitHub Actions Proof v0](github-actions-proof-v0.md)

## What The Score Means

Shield Standard keeps three dimensions separate:

```text
G  Guard    output governance
H  Handoff  input governance
A  Audit    history governance
```

Each dimension uses this visible scale:

```text
0      gray    not enabled / no evidence
C      red     critical failure
1-4    white   basic configured
5-7    yellow  disciplined
8-9    green   sustained or strong
```

Shield Lite turns the same evidence into one 0-100 workflow governance self-check score for quick sharing. Lite is easier to read, but Standard is the source of detail. Neither mode rates AI model safety.

Shield Lite challenges use `shield-lite-challenge.v0` to exercise known local governance failure modes such as protected path violations, fake audit evidence, stale handoff packets, privacy leakage flags, missing CI evidence, and oversized patches. `carves-shield challenge` reads a challenge pack locally, emits `shield-lite-challenge-result.v0`, reports case pass/fail and pass rate, and does not upload artifacts or private project data. The starter pack at `docs/shield/examples/shield-lite-starter-challenge-pack.example.json` includes 10 bounded fixtures and can be run from [Shield Lite Starter Challenge Quickstart](lite-challenge-quickstart.md). Challenge results are local challenge results, not certification, and summary output is labeled `local challenge result, not certified safe`.

Direct `ShieldLiteChallengeRunner` coverage lives in `tests/Carves.Shield.Tests/`. Those tests call the runner in-process, without invoking `pwsh`, CLI wrappers, or challenge smoke scripts, and cover valid packs, missing or invalid packs, suite metadata mismatches, missing required challenge kinds, case posture mismatches, privacy/allowed-output violations, duplicate case ids, unknown challenge kinds, and non-claim stability.
