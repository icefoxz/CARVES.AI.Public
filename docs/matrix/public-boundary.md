# CARVES Matrix Public Boundary

Status: frozen public boundary.

This document freezes the public product boundary for the Guard, Handoff, Audit, and Shield matrix. It is the boundary that later release, documentation, package, and smoke-test work must follow.

## One Sentence

CARVES Matrix is a local-first AI coding workflow governance self-check toolchain: Guard checks AI code changes, Handoff preserves session continuity, Audit reads governance history, Shield turns summary evidence into a local self-check label, and Matrix proves the four products compose.

Matrix is the composition layer, not a fifth governance engine and not a fifth safety engine.

## Product Roles

| Product | Public role | Owns | Does not own |
| --- | --- | --- | --- |
| Guard | Patch admission gate | Policy checks, diff/path/change-budget decisions, decision records | Session continuity, history aggregation, Shield self-check output |
| Handoff | AI session continuity packet | Current objective, remaining work, completed facts, decision references, next-session guidance | Patch admission, Guard decision truth, Shield self-check output |
| Audit | History readback and evidence discovery | Read-only discovery, summaries, timelines, explain readback, `shield-evidence.v0` collection | Guard policy decisions, Handoff packet ownership, Shield scoring |
| Shield | Evidence evaluator and badge | `shield-evidence.v0` evaluation, Standard G/H/A levels, Lite score, badge metadata and SVG | Raw source inspection, raw diff inspection, Audit discovery, certification, model safety rating |
| Matrix | Composition proof lane | Local orchestration of Guard -> Handoff -> Audit -> Shield proof artifacts | Guard policy logic, Handoff packet truth, Audit discovery logic, Shield scoring, certification |

## Stable Matrix Chain

The public matrix chain is:

```text
carves-guard init
carves-guard check --json

carves-handoff draft
carves-handoff inspect <packet-path>
carves-handoff next <packet-path>

carves-audit summary
carves-audit timeline
carves-audit explain <record-id>
carves-audit evidence --output .carves/shield-evidence.json

carves-shield evaluate .carves/shield-evidence.json --json --output combined
carves-shield badge .carves/shield-evidence.json --output artifacts/shield-badge.svg

carves-matrix proof --lane native-minimal --json
```

Later cards may refine exact option names, but they must preserve the ownership split:

```text
Guard/Handoff/Audit source evidence -> shield-evidence.v0 -> Shield evaluation
```

Shield evaluation consumes `shield-evidence.v0`. It must not silently bypass that contract by reading every raw Guard, Handoff, or Audit artifact directly.

Matrix proof output may record the Shield evidence, Shield evaluation, badge JSON, and badge SVG artifacts that passed through the proof lane. That linkage is provenance metadata only. Matrix does not alter Shield scoring, does not recompute Standard G/H/A levels, and does not turn a local proof into certification.

## Evidence Ownership

Guard is the owner of Guard decisions. Handoff may reference Guard decision ids, but missing Guard records are warnings, not proof that Handoff owns or repairs Guard truth.

Handoff is the owner of handoff packets. Audit may discover and summarize Handoff packets, but it must not rewrite packet truth.

Audit is the owner of readback and evidence collection. Audit may produce `shield-evidence.v0`, but it must not compute Standard G/H/A levels, Lite score, or badge output.

Shield owns the G/H/A and Lite self-check output. Shield evaluates the evidence summary and produces the Standard and Lite projections; it does not rate AI models or prove semantic correctness.

## Public Commands And Artifacts

| Command family | Primary public artifacts |
| --- | --- |
| `carves-guard` | Guard policy, Guard decision JSON/JSONL |
| `carves-handoff` | Handoff packet JSON |
| `carves-audit` | Audit summary, timeline, explain output, collected Shield evidence |
| `carves-shield` | Shield evaluation JSON, badge JSON, badge SVG |
| `carves-matrix` | Project proof output, packaged proof output, matrix proof summary |

The matrix smoke artifact bundle should contain:

```text
guard-decision.json
handoff-packet.json
audit-summary.json
shield-evidence.json
shield-evaluate.json
shield-badge.svg
matrix-summary.json
matrix-artifact-manifest.json
```

## Privacy Boundary

The default public path is summary-first.

Default matrix evidence must not require or upload:

- source code
- raw git diff text
- prompts
- model responses
- secrets
- credentials
- environment variable values
- private file payloads

The default Shield input is `shield-evidence.v0`, which carries normalized booleans, counts, timestamps, rule ids, workflow paths, safe path prefixes, and summary metadata.

## Human And Operator Gates

The following actions are human/operator gates, not automatic card completion:

- choosing or changing the public license
- pushing packages to NuGet.org
- creating public GitHub releases
- enabling a hosted Shield API
- creating a public directory, leaderboard, registry, or certification program
- claiming verified or third-party verified status
- changing the public Shield evaluation contract after release
- publishing a project as certified by CARVES

Implementation cards may prepare local package smoke tests, release notes, templates, and checkpoints. They must not claim that an operator-gated action has already happened.

## Public Documentation Boundary

Public product documentation should explain:

- what each product does
- how to install or run it
- how to read outputs
- what artifacts are generated
- what data is not uploaded
- what current limitations remain

Public product documentation must not depend on Runtime internal task/card/planning mechanics. Internal planning truth can remain in this repository for execution governance, but it is not the user-facing product model.

Runtime may remain as a compatibility and reference host for `carves guard`, `carves handoff`, `carves audit`, `carves shield`, and `carves matrix`. That compatibility host must delegate product-facing behavior to extracted product units where possible, and must not make Runtime task/card governance required public product knowledge.

Runtime internal governance remains separate from the public Matrix product story.

Users do not need Runtime task/card governance concepts to run Guard, Handoff, Audit, Shield, or Matrix.

## Non-Goals

The matrix public boundary does not include:

- operating-system sandboxing
- model safety benchmarking
- AI model ranking or rating
- semantic proof that code is correct
- source-code security certification
- hosted verification
- public ranking
- automatic package publication
- automatic GitHub release creation
- replacing human review

## Release Readiness Dependency

The matrix is GitHub-publishable only after all of these are true:

1. Guard has package metadata, local install smoke, init, quickstart, and CI template proof.
2. Handoff has default packet path, quickstart, local install smoke, Guard reference bridge, and readiness checkpoint.
3. Audit has auto discovery, `shield-evidence.v0` collection, local install smoke, and readiness checkpoint.
4. Shield continues to evaluate only from `shield-evidence.v0`.
5. The external-repo matrix smoke proves Guard -> Handoff -> Audit evidence -> Shield evaluate/badge.
6. Cross-platform packaged install smoke passes without NuGet.org and uses the standalone Guard, Handoff, Audit, Shield, and Matrix tool entries.
7. Public repo hygiene, README, release notes, and known limitations are complete.
8. Remaining operator gates are listed rather than implied complete.

## Trust-Chain Hardening Gate

The matrix proof can now be used as a local self-check proof with the current hardening categories complete:

1. Audit evidence integrity, timeline, summary, and explainability checks.
2. Guard deletion/replacement honesty plus decision-store append and retention durability.
3. Shield evidence contract alignment, Lite/Standard self-check boundary, and badge path consistency.
4. Handoff terminal-state semantics, reference freshness, and portability checks.
5. Matrix-to-Shield provenance linkage, large-log boundaries, output-path boundaries, and release checkpoint coverage.
6. Guard, Audit, and Shield usability plus coverage cleanup for the local proof lane.

Verifier-computed `trust_chain_hardening.gates_satisfied=true` means these local hardening gates are complete for the current Matrix proof bundle. Matrix verifies that both `proof_mode=native_minimal` and `proof_mode=full_release` public proof summaries match verifier-computed gates and proof capability expectations rather than trusting summary-authored gate or capability fields. It does not change the public non-claims: no model safety benchmark, no hosted verification, no public ranking, no certification, no semantic source proof, no automatic rollback, and no operating-system sandboxing. Internal checkpoint documents retain exact CARD traceability for operator review.

## Verifier Public Contract

`matrix-proof-summary.json` is a closed public readback contract. Unknown public fields are rejected by `carves-matrix verify` with `summary_unknown_field:<json_path>` so private local paths or extra metadata cannot be smuggled into a passing public proof summary. Native minimal source-summary semantics, full release project/packaged summary semantics, and Shield evaluation semantics are read through manifest-bound verified reads before public fields are trusted. This is still local bundle verification, not hosted verification or certification.
