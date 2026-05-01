# CARVES Agent Trial V1 Local History

Status: CARD-914 local history and compare implemented for offline Agent Trial runs.

Local history gives users a way to compare repeated local runs without registering, submitting data, or treating the result as a server receipt.

## Commands

Record a verified or locally collected trial bundle summary into a local history directory:

```text
carves-matrix trial record \
  --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle \
  --history-root ./carves-agent-trial-history \
  --run-id day-1 \
  --json
```

Compare two recorded runs:

```text
carves-matrix trial compare \
  --history-root ./carves-agent-trial-history \
  --baseline day-1 \
  --target day-2 \
  --json
```

## Storage Boundary

History entries are written under:

```text
<history-root>/runs/<run-id>.json
```

The history root must be outside the verified bundle root. History is a local user convenience surface; it is not part of the Matrix manifest, does not become a verified artifact, and does not replace `matrix-artifact-manifest.json` or `trial/carves-agent-trial-result.json`.

## Stored Fields

Each history entry stores summary-only fields:

- run id;
- recorded timestamp;
- local-only authority mode;
- Matrix verification status;
- suite, pack, task, prompt, and scoring profile identity;
- aggregate score;
- dimension scores;
- stable reason codes;
- bundle-relative evidence refs;
- manifest and trial-result SHA-256 anchors;
- non-claims.

History entries must not store private workspace roots, bundle absolute paths, raw prompts, raw diffs, model responses, source snippets, secrets, or credentials.

## Compare Modes

`trial compare` emits:

- `comparison_mode=direct` when suite, pack, task, prompt, and scoring profile identity match exactly;
- `comparison_mode=trend_only` when any identity field differs.

Trend-only comparisons still show movement, but they are not direct score comparisons. Common reason codes include:

- `pack_version_mismatch`;
- `task_version_mismatch`;
- `prompt_version_mismatch`;
- `scoring_profile_version_mismatch`.

## Score Movement

Compare output includes aggregate movement and per-dimension movement:

```json
{
  "aggregate_score": {
    "baseline": 100,
    "target": 30,
    "delta": -70
  },
  "dimensions": [
    {
      "dimension": "report_honesty",
      "baseline": 10,
      "target": 0,
      "delta": -10
    }
  ]
}
```

This is the local Day 1 versus Day 2 style surface. It is intentionally simple: the user can see a drop without uploading raw project data.

## Non-Claims

Local history does not claim:

- server receipt issuance;
- hosted verification;
- public leaderboard eligibility;
- certification;
- tamper-proof local execution;
- direct comparability across changed pack, prompt, task, or scoring profile versions.

## CARD-914 Acceptance Mapping

`CARD-914` is satisfied by the current implementation:

- `trial record` writes local history entries outside the verified bundle root;
- history entries contain summary-only fields and hash anchors, not private raw prompts or secrets;
- `trial compare` shows aggregate and dimension movement;
- cross-version, prompt, task, pack, or scoring-profile mismatches become `trend_only`;
- tests cover directly comparable and non-comparable local history entries.
