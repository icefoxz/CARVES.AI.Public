# CARVES Agent Trial V1 Prompt Version Leaderboard

Status: Phase 7 prompt version leaderboard contract freeze for the proposed Agent Trial V1 product line.

This document defines the V1 prompt version leaderboard. It does not implement aggregation, storage, UI, anti-gaming enforcement, hosted verification, certification, semantic correctness proof, model identity verification, or operating-system sandboxing.

## Goal

The prompt version leaderboard compares official prompt versions by aggregate Agent Trial behavior.

It answers:

```text
Under the same official suite and compatible task pack, which prompt versions tend to produce better summary-only development safety posture?
```

It does not answer:

```text
Which model is generally safest?
Which agent identity is verified?
Which generated code is semantically correct?
Which prompt certifies production safety?
```

## Grouping Key

Prompt leaderboard rows are grouped by:

- `suite_id`;
- `pack_id`;
- `pack_version`;
- `task_pack_version`;
- `prompt_id`;
- `prompt_version`.

The displayed row must always show prompt id and prompt version.

Results from incompatible task pack versions must not be silently mixed. If a UI shows them together, it must label the pack version split and avoid a single combined rank unless an explicit compatibility map exists.

## Eligible Results

Only results with this posture can enter the main prompt leaderboard:

- `result_mode=accepted`;
- `visibility=public`;
- server receipt exists;
- server-issued challenge;
- Matrix verify or equivalent server validation passed;
- trial artifacts were manifest-covered;
- result is not duplicate or replay flagged for public ranking;
- prompt id/version and pack version match the issued challenge;
- privacy posture is summary-only;
- official suite and official pack.

Private and unlisted results can appear only in private user analytics, not in the public prompt leaderboard.

## Metrics

V1 prompt version metrics:

| Metric | Meaning |
| --- | --- |
| `median_lite_score` | Median Shield Lite score across eligible results |
| `median_guard_score` | Median Guard score readback |
| `median_handoff_score` | Median Handoff score readback |
| `median_audit_score` | Median Audit score readback |
| `completion_rate` | Share of eligible results whose task completion posture is complete or acceptable |
| `scope_violation_rate` | Share with forbidden path or allowed-scope failures |
| `test_evidence_rate` | Share with required test evidence present and matched |
| `report_honesty_rate` | Share where agent claims match diff/test/Matrix evidence |
| `initial_spike_rate` | Share with one-time high score not reproduced by later accepted results |
| `verified_run_count` | Count of accepted public verified results |
| `unique_user_count` | Count of users contributing eligible results |

Exact numeric weighting is not frozen in this document. The leaderboard should preserve component metrics so users can see why a row ranks where it ranks.

## Default Sorting

Recommended default sort:

1. sufficient `verified_run_count`;
2. highest rolling or median verified posture;
3. lower scope violation rate;
4. higher test evidence rate;
5. higher report honesty rate;
6. lower initial spike rate;
7. newer prompt version only as a tie breaker when clearly labeled.

Do not sort the main leaderboard by historical best score.

Low-sample prompt versions should be labeled `experimental_sample` rather than ranked as stable winners.

## Public Row Shape

Example public row:

```json
{
  "schema_version": "agent-trial-prompt-leaderboard-row.v0",
  "suite_id": "official-agent-dev-safety",
  "pack_id": "official-agent-dev-safety-v1",
  "pack_version": "1.0.0",
  "task_pack_version": "official-v1",
  "prompt_id": "official-v1-test-discipline",
  "prompt_version": "1.0.0",
  "median_lite_score": 88,
  "completion_rate": 0.82,
  "scope_violation_rate": 0.04,
  "test_evidence_rate": 0.91,
  "report_honesty_rate": 0.89,
  "initial_spike_rate": 0.06,
  "verified_run_count": 120,
  "unique_user_count": 45,
  "sample_posture": "stable",
  "non_claims": {
    "certification": false,
    "model_identity_verified": false,
    "semantic_correctness_proven": false
  }
}
```

## Non-Claims

Every public prompt leaderboard view must preserve these non-claims:

- results are non-certification analytics;
- prompt rank is not model safety certification;
- prompt rank is not semantic correctness proof;
- agent and model labels remain self-reported unless a later verification layer says otherwise;
- local evidence can still be manipulated by a determined local attacker.

## Phase 7 Acceptance Mapping

TRIAL-V1-070 is satisfied by this document:

- prompt version is always displayed;
- incompatible task pack versions are not silently mixed;
- results are explicitly non-certification analytics.
