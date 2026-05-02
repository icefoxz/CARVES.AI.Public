# CARVES Audit

Language: [Chinese](README.zh-CN.md)

CARVES Audit is a CARVES Runtime capability for read-only evidence discovery and readback.

It reads Guard decisions and Handoff packets, then produces summaries, timelines, explanations, and safe `shield-evidence.v0` summary evidence.

The public CLI surface is `carves-audit`, and the public service type is `AuditService`.

Audit keeps only the bounded recent tail of large Guard JSONL histories in memory. The default tail is 1,000 records.

Audit evidence is conservative. It reports observed records, but it does not invent higher-trust claims from those records. Unless explicit source evidence exists, generated Shield evidence keeps append-only proof, explain coverage, summary report, change report, and failure-pattern report fields false or zero.

## Start Here

- [Quickstart](quickstart.en.md)

## Default Inputs

```text
.ai/runtime/guard/decisions.jsonl
.ai/handoff/handoff.json
```

Run from a repository root:

```powershell
carves-audit summary --json
carves-audit timeline --json
carves-audit evidence --json --output .carves/shield-evidence.json
```

Generated evidence output must stay inside the repository and outside protected truth paths such as `.git/`, `.ai/tasks/`, `.ai/memory/`, `.ai/runtime/guard/`, and `.ai/handoff/`. Prefer `.carves/shield-evidence.json` or an `artifacts/` path.

## What Audit Does

- discovers default Guard decision output
- discovers the default Handoff packet
- summarizes allow/review/block decisions
- reads Handoff readiness metadata
- explains a Guard run id or Handoff id
- emits privacy-safe Shield evidence
- detects Guard GitHub Actions usage with a simple workflow-text heuristic, not a hosted CI verification service

## What Audit Does Not Do

- no Shield scoring
- no badge rendering
- no append-only log proof
- no automatic claim that every block/review decision has explain coverage
- no claim that summary/change/failure-pattern report artifacts exist unless explicit report evidence exists
- no source upload
- no raw diff collection
- no prompt or model response collection
- no Guard/Handoff mutation
- no certification claim
