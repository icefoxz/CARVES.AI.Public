# Beginner Guide: Run Your First Shield Self-Check

Language: [Chinese](shield-beginner-guide.zh-CN.md)

CARVES Shield checks whether a repository has evidence for AI code governance.

It does not ask whether the code is good. It asks:

```text
When AI changes code, does the project have boundaries?
When the next AI session starts, does it receive reliable handoff context?
Can prior AI-related decisions be read and explained later?
```

This matters because AI can change code quickly, while human review attention is limited. Without boundaries, handoff, and audit evidence, AI work can drift across too many files, touch protected areas, or repeat old mistakes in later sessions.

Shield v0 starts with a local self-check so any project can see its current posture.

## What You Need

You need three things:

1. A git repository.
2. The CARVES CLI.
3. A `shield-evidence.v0` summary evidence file.

The evidence file is not source code and not a raw diff. It stores safe summary fields such as booleans, counts, sample windows, workflow paths, and rule ids.

## First Run

After installing the CLI, run the included example evidence:

```powershell
carves shield evaluate docs/shield/examples/shield-evidence-standard.example.json --json --output combined
```

You should see two result families:

```text
Standard: CARVES G8.H8.A8 /30d PASS
Lite: 90/100 strong
```

Standard keeps three dimensions:

```text
G  Guard    output governance: did the AI patch pass a boundary check?
H  Handoff  input governance: did the next AI session receive reliable context?
A  Audit    history governance: can prior decisions be traced and explained?
```

Lite converts the same evidence into one 0-100 score for quick sharing and comparison.

For exact definitions of G/H/A, Lite score, PASS, REVIEW, BLOCK, local self-check, challenge result, and verification result, see the [glossary](glossary.en.md).

## Prepare Evidence For Your Project

Create this file in your project:

```text
.carves/shield-evidence.json
```

Copy the example and adapt it:

```powershell
Copy-Item docs/shield/examples/shield-evidence-standard.example.json .carves/shield-evidence.json
```

Update summary fields to match your project:

- Whether a Guard policy exists.
- Whether CI runs Shield or Guard checks.
- How many allow, review, and block decisions exist in the latest window.
- Whether Handoff packets exist.
- Whether Audit logs are readable.

Do not place source code, raw diffs, prompts, model responses, secrets, or credentials in the evidence file.

## Generate A Badge

Render a local static SVG:

```powershell
carves shield badge .carves/shield-evidence.json --output docs/shield-badge.svg
```

Inspect badge metadata:

```powershell
carves shield badge .carves/shield-evidence.json --json
```

The badge is a self-check label, not official certification.

## Add GitHub Actions

The smallest CI flow is:

1. Check out the repository.
2. Install the CARVES CLI.
3. Run `carves shield evaluate`.
4. Run `carves shield badge`.
5. Upload the generated JSON and SVG artifacts.

See [GitHub Actions integration](github-actions.en.md) for a copyable workflow.

## Read The Colors

```text
red     critical failure: a configured governance claim failed a critical gate
gray    no evidence
white   basic configuration exists
yellow  disciplined governance exists
green   strong or sustained evidence exists
```

Do not read only the Lite score. The Standard `G/H/A` label tells you which dimension is weak.

## Common Misunderstandings

Shield v0 is not:

- a code quality score
- a vulnerability scanner
- an AI coding agent
- a hosted API
- a public leaderboard
- a certificate
- an operating-system sandbox

Shield v0 is:

- a local self-check
- summary evidence evaluation
- a readable label for AI code governance posture
- a lightweight proof that can run in CI
