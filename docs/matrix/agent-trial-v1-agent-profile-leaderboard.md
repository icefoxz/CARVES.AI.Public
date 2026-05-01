# CARVES Agent Trial V1 Agent Profile Leaderboard

Status: Phase 7 agent profile leaderboard contract freeze for the proposed Agent Trial V1 product line.

This document defines the V1 self-reported agent/profile leaderboard. It does not implement aggregation, storage, UI, anti-gaming enforcement, hosted verification, certification, semantic correctness proof, model identity verification, or operating-system sandboxing.

## Goal

The agent profile leaderboard compares self-reported agent workflows under official Agent Trial packs.

It answers:

```text
For self-reported AgentProfile snapshots, which workflows repeatedly produce better verified development-safety posture under official tasks?
```

It does not prove that the named model, agent binary, toolchain, or local environment is independently verified.

## Identity Boundary

V1 AgentProfile identity is self-reported.

Public rows must label:

- `agent_label` as self-reported;
- `model_label` as self-reported;
- reasoning depth as self-reported;
- tool profile as self-reported unless future runner provenance verifies it;
- permission profile as declared unless future enforcement evidence verifies it.

Do not use language such as verified model, certified agent, or authenticated model binary in V1 public leaderboard surfaces.

## Grouping Key

Agent profile leaderboard rows are grouped by:

- `suite_id`;
- `pack_id`;
- `pack_version`;
- `task_pack_version`;
- `agent_profile_snapshot` grouping key;
- prompt version grouping mode.

Prompt version grouping mode must be explicit:

- `single_prompt_version`: row includes one prompt id/version;
- `compatible_prompt_family`: row includes compatible prompt versions only when a compatibility map exists;
- `all_prompt_versions_labeled`: row is exploratory and must show prompt-version breakdown.

The main leaderboard should prefer `single_prompt_version` or compatible prompt family grouping to avoid hiding prompt effects.

## Eligible Results

Only results with this posture can enter the main agent profile leaderboard:

- `result_mode=accepted`;
- `visibility=public`;
- server receipt exists;
- server-issued challenge;
- Matrix verify or equivalent server validation passed;
- trial artifacts were manifest-covered;
- result is not duplicate or replay flagged for public ranking;
- AgentProfile snapshot is present;
- prompt and pack versions are comparable for the row;
- privacy posture is summary-only;
- official suite and official pack.

The main leaderboard should require a minimum verified run count. Low-sample profiles can appear in an exploratory table, not as stable rank leaders.

## Metrics

V1 agent profile metrics:

| Metric | Meaning |
| --- | --- |
| `median_lite_score` | Median Shield Lite score across eligible results |
| `rolling_lite_score` | Rolling recent score when enough accepted results exist |
| `verified_run_count` | Count of accepted public verified results |
| `safety_posture_dimensions` | Aggregated reviewability, traceability, explainability, report honesty, constraint, reproducibility |
| `scope_violation_rate` | Share with forbidden path or allowed-scope failures |
| `test_claim_accuracy` | Share where test claims match test evidence |
| `handoff_completion_rate` | Share with required handoff evidence present and valid |
| `report_honesty_rate` | Share where agent report claims match collected evidence |
| `initial_spike_rate` | Share with one-time high score not reproduced by later accepted results |
| `unique_prompt_version_count` | Count of prompt versions represented in the row |

The leaderboard should display enough component metrics that a high rank cannot hide weak report honesty or boundary behavior.

## Default Sorting

Recommended default sort:

1. minimum verified run count satisfied;
2. highest rolling or median verified posture;
3. higher report honesty rate;
4. lower scope violation rate;
5. higher test claim accuracy;
6. higher handoff completion rate;
7. lower initial spike rate.

Do not sort by historical best score.

Do not rank a single lucky run above a profile with a stable multi-run record.

## Public Row Shape

Example public row:

```json
{
  "schema_version": "agent-trial-agent-profile-leaderboard-row.v0",
  "suite_id": "official-agent-dev-safety",
  "pack_id": "official-agent-dev-safety-v1",
  "pack_version": "1.0.0",
  "task_pack_version": "official-v1",
  "prompt_grouping": "single_prompt_version",
  "prompt_id": "official-v1-test-discipline",
  "prompt_version": "1.0.0",
  "agent_profile": {
    "agent_label": "User reported agent",
    "model_label": "User reported model",
    "reasoning_depth": "high",
    "self_reported": true
  },
  "median_lite_score": 86,
  "verified_run_count": 18,
  "scope_violation_rate": 0.03,
  "test_claim_accuracy": 0.94,
  "handoff_completion_rate": 0.88,
  "report_honesty_rate": 0.91,
  "initial_spike_rate": 0.0,
  "sample_posture": "stable",
  "non_claims": {
    "agent_identity_verified": false,
    "model_identity_verified": false,
    "certification": false
  }
}
```

## Eligibility Labels

Recommended row labels:

- `stable`: minimum verified run count met and no major replay/spike flags;
- `experimental_sample`: low run count;
- `visibility_limited`: some user results are private or unlisted and excluded;
- `replay_flagged`: duplicate or replay flags exclude some results;
- `prompt_mixed`: exploratory row with prompt versions shown separately.

Labels should explain ranking posture without accusing the user of cheating.

## Phase 7 Acceptance Mapping

TRIAL-V1-071 is satisfied by this document:

- identity is labeled self-reported unless future verification exists;
- ranking uses median or rolling verified score, not historical best;
- minimum verified run count is required for main leaderboard eligibility.
