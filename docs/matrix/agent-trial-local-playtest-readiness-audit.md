# Matrix Agent Trial Local Playtest Readiness Audit

Status: `CARD-907` readiness re-audit.

## Verdict
The local Agent Trial evidence engine is partially real, but the downloadable first-run playtest is not ready as a product entry point.

The current repo can prove the important internal mechanics:

- a local Task 001 fixture exists;
- local collector code can produce summary evidence;
- the collector can fail closed on task-contract pin mismatch;
- Matrix can verify manifest-covered trial artifacts with `verify --trial`;
- schema, privacy, consistency, and tamper checks have direct Matrix tests;
- safety posture projection exists as dimension levels.

The missing part is not the evidence model. The missing part is the user path. A first-time user still cannot start from the public Matrix quickstart, get an official starter pack, run one obvious local command, collect evidence, see a human result card, and compare repeated runs without reading internal tests.

## First-Run Journey Audit

| Step | Current state | Readiness |
| --- | --- | --- |
| Discover the feature | Agent Trial docs are linked from Matrix docs, but the beginner quickstart is still the Guard/Handoff/Audit/Shield Matrix proof path, not an Agent Trial path. | not ready |
| Get a starter pack | `tests/fixtures/agent-trial-v1/task-001-pack/` is a runnable fixture, but it is not packaged as a public official starter pack. | partial |
| Read standard instructions | Fixture-level `AGENTS.md` and `CLAUDE.md` exist. There is no public instruction-pack identity or versioned prompt pack surfaced for users. | partial |
| Give the task to an agent | The task fixture has task text and constraints, but the user-facing prompt flow is not exposed as a product command or quickstart. | partial |
| Agent writes a report | `artifacts/agent-report.json` is modeled and tested. Users still need exact public guidance for producing it. | partial |
| Collect evidence | `AgentTrialLocalCollector` exists as a Core API and is tested. There is no public `carves-trial collect` or Matrix CLI command for users. | partial |
| Manifest-cover trial artifacts | Matrix fixture helpers can build trial bundles for tests. There is no user command that packages collector output into the verified Matrix bundle shape. | partial |
| Verify the bundle | `carves-matrix verify <artifact-root> --trial` exists and has strong runtime checks. | ready |
| Understand the result | Safety posture projection exists, but there is no first-run result card or plain-language local score readback. | not ready |
| Compare repeated runs | Version comparability fields exist, but there is no local history or compare surface. | not ready |

## Solid Evidence Already In Place

### Contracts And Schemas
- `docs/matrix/schemas/matrix-agent-task.v0.schema.json`
- `docs/matrix/schemas/agent-report.v0.schema.json`
- `docs/matrix/schemas/diff-scope-summary.v0.schema.json`
- `docs/matrix/schemas/test-evidence.v0.schema.json`
- `docs/matrix/schemas/carves-agent-trial-result.v0.schema.json`

These schemas are not just prose. Matrix verifier runtime validation uses embedded trial schemas and rejects closed-schema, type, enum, const, privacy, and relative-path violations.

### Local Collector
`AgentTrialLocalCollector` can:

- read pack, challenge, task contract, and agent report;
- require an expected task-contract hash from pack or challenge metadata;
- fail closed before trusting mutated workspace policy;
- take pre-command and post-command diff snapshots;
- run required commands itself;
- write diff summary, test evidence, and trial result artifacts;
- mark output as local-only and not leaderboard-eligible.

This is enough for a local evidence engine. It is not yet enough for a public user flow because it is not exposed as a simple command.

### Matrix Trial Verification
Matrix can:

- require all five trial artifacts in explicit trial mode;
- reject loose trial files that are not manifest-covered in strict mode;
- verify hashes, sizes, schema versions, producers, privacy flags, and artifact consistency;
- bind trial-result artifact hashes to manifest-covered artifacts;
- project a `trial_artifacts` trust-chain gate.

This means the Matrix side of the local integrity check is stronger than the current product entry point.

### Tests
Relevant test coverage exists for:

- local collector success and fail-closed behavior;
- task-contract self-edit attacks;
- required command failure not being overridden by agent claims;
- trial schema validation;
- trial artifact consistency;
- tampered evidence detection;
- local regression fixtures;
- adversarial fixture harness cases;
- safety posture projection.

## Missing Implementation

These are implementation gaps, not documentation-only gaps:

1. No official packaged starter pack outside test fixtures.
2. No public local Agent Trial command surface.
3. No command that materializes the fixture pack into a user workspace.
4. No command that collects local Agent Trial evidence from a user workspace.
5. No command that converts collector output into a manifest-covered Matrix trial bundle.
6. No human-readable result card.
7. No local history or Day 1 versus Day 2 compare command.

These map directly to `CARD-908` through `CARD-914`.

## Missing Documentation

These are documentation gaps:

1. The Matrix beginner quickstart does not yet tell a user how to run Agent Trial locally.
2. The docs do not yet provide one exact first-run command sequence.
3. The docs do not yet explain the local result in plain language.
4. The docs do not yet tell users how to generate `agent-report.json` safely.
5. The docs do not yet show what stays local and what can later be submitted.
6. The docs do not yet show score comparison rules in a user-facing way.

These map mainly to `CARD-915`, with result-language dependencies from `CARD-911` and `CARD-912`.

## Missing Product Framing

These are product framing gaps:

1. Current Agent Trial docs are still mostly contract and implementation planning documents.
2. A user can confuse "Matrix verified" with "official leaderboard accepted" unless the local-only boundary is repeated at the result surface.
3. A user can confuse safety posture dimensions with a general model-safety benchmark unless the result card keeps the local task boundary visible.
4. A user can confuse fixture tests with public pack availability unless the starter pack is promoted deliberately.

The public framing should say:

```text
This is a local task-evidence playtest. It checks whether one local agent run produced reviewable, traceable, explainable, constrained, reproducible summary evidence. It is not certification, not producer identity, not hosted verification, and not a leaderboard receipt.
```

## Stale Or Risky Claims To Fix Later

These claims should not be treated as current user-facing truth until follow-up cards resolve them:

- `docs/matrix/agent-trial-v1-local-runner.md` defines `carves-trial fetch` and `carves-trial init`, but those commands are not present in the current Matrix CLI surface.
- `docs/matrix/agent-trial-v1-local-collector.md` still says it does not implement collection, while the repo now has `AgentTrialLocalCollector`. The document needs a status split between contract history and current implementation.
- The local MVP fixture is runnable through tests, but it is not yet a public starter pack.
- Matrix `verify --trial` is ready for strict verification, but it does not by itself run the agent, collect evidence, or create the trial bundle.
- Local-only results remain ineligible for official leaderboard use even when Matrix verification passes.

## Follow-Up Card Order

The existing order remains correct and should not be reordered for server work:

1. `CARD-908` official starter pack packaging.
2. `CARD-909` standard agent instruction and prompt pack.
3. `CARD-910` local command surface.
4. `CARD-911` local score mapping.
5. `CARD-912` human result card.
6. `CARD-913` friendly diagnostics.
7. `CARD-914` local history and compare.
8. `CARD-915` local quickstart.
9. `CARD-916` end-to-end local user smoke.

Server registration, prompt pull, receipts, and leaderboard acceptance should stay out of this sequence until the offline playtest is actually usable.

## CARD-907 Acceptance Mapping

`CARD-907` is complete because this audit:

- reviews local entry points, contracts, schemas, proof outputs, and test-backed evidence as a first-run journey;
- separates missing implementation, missing documentation, and missing product framing;
- preserves the `CARD-908` through `CARD-916` order around local playtest value;
- calls out stale command, implementation-status, leaderboard, certification, and verification claims before new work proceeds.
