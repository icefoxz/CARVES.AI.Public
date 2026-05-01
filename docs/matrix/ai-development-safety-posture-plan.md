# CARVES AI Development Safety Posture Plan

This plan records the next maturity line for Matrix and Shield. It is not implemented yet, and it does not create hosted verification, public certification, public ranking, model safety benchmarking, semantic correctness proof, or operating-system sandboxing.

## Goal

The goal is to evaluate a project's AI-assisted development safety posture.

Plainly, the question is:

```text
When AI helps develop this project, can the project keep the work reviewable, recorded, explainable, truthfully reported, constrained, and reproducible?
```

This is different from asking whether an AI agent is inherently obedient. The result must stay bound to:

- the project;
- the task;
- the prompt or instruction class;
- tool permissions;
- repository state;
- evidence window;
- CARVES tool version;
- Matrix proof bundle.

## Current Baseline

The current local Matrix and Shield stack already provides a useful base:

- Guard records and evaluates patch boundary decisions.
- Handoff records continuation context.
- Audit collects local summary evidence into `shield-evidence.v0`.
- Shield projects Standard G/H/A and Lite score.
- Matrix creates and verifies a summary-only proof bundle.
- Matrix verify checks artifact hashes, sizes, required metadata, privacy flags, Shield score readback, evidence hash binding, and proof-summary consistency.

This means the current stack can say:

```text
This local evidence bundle is internally consistent, and the project shows this level of Guard/Handoff/Audit governance evidence.
```

It cannot yet fully say:

```text
This specific AI-assisted development task was executed safely and reported truthfully.
```

## Missing Layers

The next maturity line needs six layers.

### 1. Task Contract

Define a task input contract, tentatively `matrix-agent-task.v0`.

It should record:

- task objective;
- allowed paths;
- forbidden paths;
- required commands;
- expected tests;
- allowed change type;
- forbidden change type;
- prompt or instruction class;
- tool permission summary;
- sandbox/approval posture;
- stop-and-ask conditions.

Purpose:

```text
Make the task boundary explicit before the AI-assisted work starts.
```

Without this layer, CARVES can evaluate governance evidence, but it cannot strongly judge whether the work matched the assigned task.

### 2. Agent Report Contract

Define an AI work report contract, tentatively `agent-report.v0`.

It should record what the agent claims:

- files changed;
- task completed or not completed;
- tests run;
- tests not run and why;
- risks noticed;
- follow-up work;
- blocked or uncertain decisions;
- deviations from the task contract.

Purpose:

```text
Turn "the agent said it did X" into structured claims that can be checked.
```

Without this layer, report honesty remains mostly manual.

### 3. Claim Evidence Binding

Bind each important report claim to evidence.

Examples:

| Claim | Evidence |
| --- | --- |
| "Tests passed" | test result artifact hash, command, exit code |
| "Only allowed files changed" | diff scope summary |
| "No forbidden path touched" | Guard decision and changed-file summary |
| "Handoff completed" | Handoff packet path and hash |
| "Matrix verified" | `matrix-verify.v0` result and manifest hash |

Purpose:

```text
Make claims mechanically checkable instead of relying on prose.
```

### 4. Diff Scope Summary

Add a summary-only diff scope artifact. It must not include raw diff text by default.

Suggested fields:

- changed file paths;
- changed file count;
- source/test/doc/config/generated buckets;
- allowed scope match;
- forbidden path violations;
- unrequested change count;
- source files changed without tests;
- deleted files and replacement classification;
- large change budget status.

Purpose:

```text
Check whether the work stayed inside the task boundary without uploading raw source diffs.
```

### 5. Test Evidence

Add explicit test evidence artifacts.

Suggested fields:

- required commands;
- executed commands;
- command exit codes;
- test framework summary;
- pass/fail/skip counts;
- failure summary;
- log hash;
- result artifact hash;
- whether the agent claimed tests passed.

Purpose:

```text
Separate actual test evidence from agent self-report.
```

### 6. Continuous Challenge Receipt

Build on `docs/matrix/continuous-challenge.md`.

Add challenge and receipt contracts:

- `matrix-continuity-challenge.v0`;
- `matrix-continuity-receipt.v0`.

The continuity chain should preserve:

- challenge id;
- project id;
- day or sequence number;
- previous receipt;
- commit sha;
- manifest hash;
- proof summary hash;
- Shield evidence hash;
- Shield evaluation hash;
- Standard G/H/A label;
- Lite score;
- trend label.

Purpose:

```text
Detect one-time high scores that cannot be reproduced under fresh challenges.
```

Example readback:

```text
Day 1: G9.H9.A9 = 100
Day 2: G4.H2.A1 = 30
Day 3: G4.H2.A1 = 30
Trend: initial_spike
```

This should be treated as a reproducibility signal, not as an automatic cheating accusation.

## Safety Posture Projection

After those layers exist, project-level safety posture can be projected above G/H/A.

Suggested dimensions:

| Dimension | Meaning | Evidence Sources |
| --- | --- | --- |
| Reviewability | Can reviewers see what happened and why? | Guard, diff scope, test evidence |
| Traceability | Is the work recorded across decisions and handoff? | Guard decisions, Handoff, Audit |
| Explainability | Can failures, reviews, and blocks be explained? | Audit explain, Shield risks, report bindings |
| Report honesty | Do agent claims match evidence? | Agent report, test evidence, Matrix verify |
| Constraint | Did boundaries actually apply? | Task contract, Guard, diff scope |
| Reproducibility | Does posture remain stable under fresh challenges? | Continuity receipt chain |

This projection should not replace Shield G/H/A. It should sit above it as a separate read model.

## Execution Order

Recommended order:

1. `matrix-agent-task.v0` contract and examples.
2. `agent-report.v0` contract and examples.
3. Claim-to-evidence binding model.
4. Summary-only diff scope artifact.
5. Test evidence artifact.
6. Matrix proof-summary and verifier integration for task/report/scope/test artifacts.
7. Continuous challenge and receipt contracts.
8. Safety posture projection.
9. Optional API/hosted rerun design.

This order is deliberate. Continuous challenge becomes much more useful after the system knows what task was assigned, what the agent claimed, what changed, and what tests actually ran.

## Non-Goals For This Plan

This plan does not add:

- raw source upload by default;
- raw diff upload by default;
- prompt text upload by default;
- model response upload by default;
- hosted verification claims;
- public certification;
- model safety benchmarking;
- AI model ranking;
- semantic correctness proof;
- operating-system sandboxing;
- automatic rollback.

## Product Language

Use this language:

```text
AI-assisted development safety posture
continuous reproducibility signal
summary-only governance evidence
tamper-evident proof bundle
```

Avoid this language:

```text
certified safe
tamper-proof
AI model safety benchmark
agent obedience certification
semantic correctness proof
```
