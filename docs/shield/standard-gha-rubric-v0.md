# CARVES Shield Standard G/H/A Rubric v0

Status: rubric draft for CARD-758.

This document defines the Standard CARVES Shield rubric for the independent Guard, Handoff, and Audit dimensions.

The rubric consumes `shield-evidence.v0` evidence. It does not implement `carves shield evaluate`, define Shield Lite scoring, render badges, upload evidence, or provide hosted API behavior.

## Contract Files

```text
docs/shield/evidence-schema-v0.md
docs/shield/schemas/shield-evidence-v0.schema.json
docs/shield/rubrics/standard-gha-rubric-v0.json
```

The rubric id is:

```text
shield-standard-gha-rubric.v0
```

## Standard Output

Standard keeps three independent dimensions:

```text
G  Guard    output governance
H  Handoff  input governance
A  Audit    history governance
```

The visible label format stays:

```text
CARVES G<level>.H<level>.A<level> /<window>d <critical>
```

Examples:

```text
CARVES G8.H5.A3 /30d PASS
CARVES G2.H0.A1 PASS
CARVES G8.HC.A3 /30d FAIL CH-02
```

Standard must not produce a single overall score. A project can be strong in Guard and weak in Handoff or Audit; averaging those dimensions would hide the actual governance posture.

## Shared Rules

Each dimension returns one of:

```text
0
1
2
3
4
5
6
7
8
9
C
```

`C` means Critical Gate failure. It is not a number and must not be averaged.

Numeric levels are cumulative. A dimension can only claim level `N` when it satisfies every predicate from level `1` through level `N`.

If a dimension is disabled or has no usable evidence, it returns `0`.

If a dimension is enabled and any Critical Gate for that dimension fails, it returns `C`.

If evidence is missing for a predicate, that predicate is not satisfied. Missing evidence should lower the level rather than be guessed.

For Audit specifically, raw decision counts are not explain coverage, report generation, or append-only proof. Producers must report those stronger claims only when separate evidence exists. Otherwise, the evaluator treats the claim as absent, keeps the Audit level lower, or triggers the relevant Audit Critical Gate.

Missing scoring fields are conservative degradation, not input errors. The evaluator still returns a Standard label when the privacy and schema envelope are valid, but the affected dimension stops before the level that needed the missing field.

## Color Bands

```text
0      gray    not enabled / no evidence
C      red     critical failure
1-4    white   basic configured
5-7    yellow  disciplined
8-9    green   sustained or strong
```

## Input Validity

Standard evaluation requires valid `shield-evidence.v0` input.

The evidence schema already rejects default evidence that includes:

- source code
- raw git diff
- prompts
- secrets
- credentials

If the evidence version is unsupported, or if the evidence violates the schema privacy contract, an evaluator should return an unsupported or invalid input result instead of a Standard G/H/A label.

## Sample Window Rules

The sample window is evidence context, not decoration.

The effective dimension window is resolved in this order:

1. Use the dimension-specific `window_days` field when present.
2. Otherwise use top-level `sample_window_days`.
3. Otherwise treat the dimension as having no sustained sample window.

Rules:

- Levels `1-7` may be reached with configuration and current-state evidence.
- Level `8` requires at least 30 days of sustained evidence for that dimension.
- Level `9` requires at least 90 days of sustained evidence for that dimension.
- A dimension cannot reach level `8` or `9` if the relevant dimension evidence omits `window_days` and the top-level `sample_window_days` is absent.
- The visible label should include `/Nd` when sustained evidence is used.

## Guard Rubric

Guard measures output governance. It asks whether AI-generated patches are checked before entering review or merge.

Evidence source:

```text
dimensions.guard
```

Guard evidence reports whether a project has patch discipline controls in place. It does not claim semantic code equivalence. For deletion discipline, the underlying Guard policy must use conservative replacement-candidate semantics; unrelated added files must not be treated as proof that a deleted file was replaced.

### Guard Critical Gates

| Gate | Failure Condition | Result |
| --- | --- | --- |
| CG-01 | Guard is enabled and `policy.schema_valid` is `false`. | `GC` |
| CG-02 | Guard is enabled and either `policy.fail_closed` is `false` or `policy.protected_path_action` is not `block`. | `GC` |
| CG-03 | CI claims to run Guard but `ci.fails_on_review_or_block` is `false`. | `GC` |
| CG-04 | `decisions.unresolved_block_count` is greater than `0`. | `GC` |

### Guard Levels

| Level | Required Evidence |
| --- | --- |
| 0 | Guard is disabled, or no Guard policy evidence is present. |
| 1 | `policy.present=true`. |
| 2 | Level 1 plus `policy.schema_valid=true`. |
| 3 | Level 2 plus `policy.fail_closed=true`, `policy.protected_path_action=block`, and at least one `effective_protected_path_prefixes` entry. |
| 4 | Level 3 plus `policy.change_budget_present=true` and `policy.outside_allowed_action` is `review` or `block`. |
| 5 | Level 4 plus CI evidence: `ci.detected=true`, `ci.guard_check_command_detected=true`, and `ci.fails_on_review_or_block=true`. |
| 6 | Level 5 plus source/test discipline and at least one adjacent discipline rule: `source_test_rule_present=true` and either `dependency_policy_present=true` or `mixed_feature_refactor_rule_present=true`. |
| 7 | Level 6 plus decision evidence with `decisions.present=true`, `unresolved_review_count=0`, and `unresolved_block_count=0`. |
| 8 | Level 7 plus at least 30 days of decision evidence and at least one proof reference. |
| 9 | Level 8 plus at least 90 days of decision evidence, all listed discipline rules present, and CI proof evidence. |

## Handoff Rubric

Handoff measures input governance. It asks whether the next AI session receives reliable context, known constraints, completed facts, and remaining work.

Evidence source:

```text
dimensions.handoff
```

### Handoff Critical Gates

| Gate | Failure Condition | Result |
| --- | --- | --- |
| CH-01 | Handoff is enabled and `latest_packet.schema_valid` is `false`. | `HC` |
| CH-02 | Handoff is enabled and `latest_packet.target_repo_matches` is `false`. | `HC` |
| CH-03 | Handoff is enabled and either `latest_packet.current_objective_present` or `latest_packet.remaining_work_present` is `false`. | `HC` |
| CH-04 | Handoff is enabled and the latest packet is older than 30 days. | `HC` |

### Handoff Levels

| Level | Required Evidence |
| --- | --- |
| 0 | Handoff is disabled, or no handoff packet evidence is present. |
| 1 | `packets.present=true` and `packets.count>=1`. |
| 2 | Level 1 plus `latest_packet.schema_valid=true`. |
| 3 | Level 2 plus `current_objective_present=true` and `remaining_work_present=true`. |
| 4 | Level 3 plus `must_not_repeat_present=true` and at least one completed fact with evidence. |
| 5 | Level 4 plus `repo_orientation_fresh=true`, `target_repo_matches=true`, and `age_days<=7`. |
| 6 | Level 5 plus at least one decision reference and `confidence` of `medium` or `high`. |
| 7 | Level 6 plus continuity evidence where every recorded session switch has a packet and `stale_packet_count=0`. |
| 8 | Level 7 plus at least 30 days of handoff evidence and at least three packets in that window. |
| 9 | Level 8 plus at least 90 days of handoff evidence, at least five covered session switches, at least ten completed facts with evidence, and at least three decision references. |

Handoff boundary values are exact:

- `latest_packet.age_days<=7` is fresh enough for `H5`.
- `latest_packet.age_days` from `8` through `30` caps Handoff at `H4`.
- `latest_packet.age_days>30` triggers `CH-04`.
- `packets.window_days>=30` is the first sustained-evidence boundary for `H8`.
- `packets.window_days>=90` is the first strong-evidence boundary for `H9`.

## Audit Rubric

Audit measures history governance. It asks whether prior AI-related decisions can be read, explained, and reviewed.

Evidence source:

```text
dimensions.audit
```

### Audit Critical Gates

| Gate | Failure Condition | Result |
| --- | --- | --- |
| CA-01 | Audit is enabled and the log is present but unreadable or schema-unsupported. | `AC` |
| CA-02 | Audit claims append-only integrity and `log.integrity_check_passed` is `false`. | `AC` |
| CA-03 | Audit has block decisions and not every block decision has explain coverage. | `AC` |

### Audit Levels

| Level | Required Evidence |
| --- | --- |
| 0 | Audit is disabled, or no readable audit log evidence is present. |
| 1 | `log.present=true` and `log.readable=true`. |
| 2 | Level 1 plus `log.schema_supported=true`. |
| 3 | Level 2 plus at least one audit record and earliest/latest record timestamps. |
| 4 | Level 3 plus records with rule ids and evidence references. |
| 5 | Level 4 plus `malformed_record_count=0` and `future_schema_record_count=0`. |
| 6 | Level 5 plus explain coverage for every block and review decision in the sample. |
| 7 | Level 6 plus a summary report generated in the evidence window. |
| 8 | Level 7 plus at least 30 days of audit evidence and at least ten records in that window. |
| 9 | Level 8 plus at least 90 days of audit evidence, passing integrity check, and either change-report or failure-pattern distribution evidence. |

`log.append_only_claimed=true` is not required for numeric levels. It is only evaluated as a critical claim when present. A local evidence producer should leave it `false` unless it has explicit append-only proof.

`coverage.block_explain_covered_count` and `coverage.review_explain_covered_count` must count explicit explain coverage. They must not be copied from the decision counts.

Report fields under `dimensions.audit.reports` must describe generated report evidence. If the producer only scanned Guard/Handoff records and did not observe report artifacts, the fields should be `false`.

## Caps And Downgrades

These caps apply after Critical Gates:

- If a dimension has no sustained sample window, its maximum level is `7`.
- If a dimension has less than 30 days of evidence, its maximum level is `7`.
- If a dimension has less than 90 days of evidence, its maximum level is `8`.
- If Guard has unresolved review decisions but no unresolved block decisions, its maximum level is `6`.
- If Audit has malformed or future-schema records, its maximum level is `4` unless a future migration rule says otherwise.
- If Handoff packet age is greater than 7 days but not greater than 30 days, its maximum level is `4`.

Caps lower numeric levels only. They do not turn a dimension into `C` unless a Critical Gate also fails.

## What This Rubric Does Not Do

This rubric does not:

- define Shield Lite scoring
- assign a single Standard score
- implement an evaluator
- implement a hosted API
- render a badge
- certify a project
- inspect source code
- require source upload
- prove operating-system sandboxing

Those are separate follow-up cards.
