# Shield Lite Starter Challenge Quickstart

Status: local starter pack guide.

This guide runs the starter challenge pack locally in under five minutes from the repository root.

## Run The Starter Pack

Use the standalone Shield CLI:

```powershell
carves-shield challenge docs/shield/examples/shield-lite-starter-challenge-pack.example.json --json
```

Compatibility wrapper:

```powershell
carves shield challenge docs/shield/examples/shield-lite-starter-challenge-pack.example.json --json
```

Expected local result:

```text
schema_version: shield-lite-challenge-result.v0
status: passed
case_count: 10 or more
summary_label: local challenge result, not certified safe
certification: false
```

For exact definitions of challenge result, Lite score, PASS, REVIEW, BLOCK, local self-check, and verification result, see the [Shield glossary](wiki/glossary.en.md).

## Smoke Check

From the repository root, run:

```powershell
pwsh ./scripts/shield/shield-lite-starter-challenge-smoke.ps1
```

The smoke script runs the starter pack with `carves-shield challenge`, parses the JSON output, and fails if the pack stops passing, drops below 10 cases, loses `certification=false`, or loses the local challenge result label.

## Boundary

The starter pack is summary-only. It exercises bounded fixture descriptions for protected paths, deletion without replacement evidence, fake audit evidence, stale handoff packets, privacy leakage flags, missing CI evidence, and oversized patches.

The output is a local challenge result, not certified safe. It is not certification, hosted verification, a public leaderboard, a model safety benchmark, semantic source correctness proof, or operating-system sandbox proof.
