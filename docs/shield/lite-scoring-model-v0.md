# CARVES Shield Lite Scoring Model v0

Status: scoring model draft for CARD-759.

Shield Lite is a quick workflow governance self-check for broad adoption and first-time project orientation.

Lite may produce one 0-100 score. That score is not certification, not a model safety benchmark, not a replacement for Standard G/H/A reporting, and not a claim that CARVES reviewed source code.

The model consumes `shield-evidence.v0` or a Standard G/H/A level projection from `shield-standard-gha-rubric.v0`. It does not implement `carves shield evaluate`, render badges, create a public directory, upload evidence, or provide hosted API behavior.

## Contract Files

```text
docs/shield/evidence-schema-v0.md
docs/shield/standard-gha-rubric-v0.md
docs/shield/rubrics/lite-scoring-model-v0.json
docs/shield/lite-challenge-schema-v0.md
docs/shield/schemas/shield-lite-challenge-v0.schema.json
```

The model id is:

```text
shield-lite-scoring.v0
```

The optional local challenge suite schema is:

```text
shield-lite-challenge.v0
```

## Product Boundary

Lite is for:

- individual developers checking their own repo
- independent developers sharing a simple workflow governance posture
- open-source maintainers finding quick next steps
- teams trying CARVES before adopting Standard reporting

Lite is not for:

- production certification
- model safety benchmarking
- AI model ranking or rating
- vendor compliance claims
- proof that source code is semantically correct
- proof of operating-system sandboxing
- replacing security review
- replacing Standard G/H/A reporting when detailed evidence matters

## Input

Lite accepts either:

1. `shield-evidence.v0` summary evidence.
2. A Standard projection containing Guard, Handoff, and Audit levels.

When only `shield-evidence.v0` is provided, Lite should first project Standard G/H/A levels using `shield-standard-gha-rubric.v0`, then compute the Lite score from those levels.

The default evidence posture remains summary-first. Lite must not require:

- source code
- raw git diff
- prompts
- secrets
- credentials

## Score Formula

Lite uses the same three dimensions as Standard, but collapses them into a single self-check score:

```text
Guard    40 points
Handoff  30 points
Audit    30 points
Total   100 points
```

Dimension points are computed from Standard numeric levels:

```text
dimension_points = round(dimension_weight * standard_level / 9)
```

The evaluator names these constants:

| Constant | Value |
| --- | ---: |
| `StandardMaxNumericLevel` | 9 |
| `LiteGuardWeight` | 40 |
| `LiteHandoffWeight` | 30 |
| `LiteAuditWeight` | 30 |
| `LiteCriticalCapScore` | 39 |
| `LiteBasicMaximumScore` | 49 |
| `LiteDisciplinedMaximumScore` | 79 |

Rounding uses midpoint-away-from-zero integer rounding after each dimension contribution is computed. The dimension contributions are rounded independently before summing.

Examples:

```text
G8 -> round(40 * 8 / 9) = 36
H7 -> round(30 * 7 / 9) = 23
A5 -> round(30 * 5 / 9) = 17
Total = 76
```

If a dimension is `0`, it contributes `0` points.

If a dimension is `C`, it contributes `0` points and activates Critical cap behavior.

## Critical Cap

Critical failure must stay visible in Lite.

If any Standard dimension is `C`, the Lite result:

- uses band `Critical`
- uses color `red`
- caps numeric score at `39` (`LiteCriticalCapScore`)
- lists the failing Critical Gate ids
- prioritizes Critical Gates ahead of ordinary top risks

Example:

```text
CARVES Shield Lite: 39/100 Critical
Critical gates: CG-04
Top risk: Guard has unresolved block decisions.
Next step: Resolve or explicitly dismiss blocked Guard decisions before publishing a Lite score.
```

Critical cap prevents a repo from appearing healthy because other dimensions have high scores while one enabled workflow governance claim is failing.

## Bands

Lite bands are intentionally simple:

| Score | Color | Band | Meaning |
| --- | --- | --- | --- |
| Critical | red | Critical | At least one configured dimension failed a Critical Gate. |
| 0 | gray | No Evidence | No usable Shield evidence. |
| 1-49 | white | Basic | Some governance exists, but the project is still easy for AI changes to drift. |
| 50-79 | yellow | Disciplined | The project has meaningful controls and review posture. |
| 80-100 | green | Strong | The project has strong or sustained AI governance evidence. |

Lite labels should include the score, band, and self-check status:

```text
CARVES Shield Lite: 76/100 Yellow Self-Check
```

If sustained evidence is used, include the window:

```text
CARVES Shield Lite: 82/100 Green Self-Check /30d
```

## Top Risks

Lite output should include up to three top risks.

Risk selection order:

1. Critical Gate failures.
2. Disabled or zero-evidence dimensions.
3. Missing Guard CI or fail-closed posture.
4. Missing Handoff packet freshness or target match evidence.
5. Missing Audit explain coverage or readable log evidence.
6. Lowest dimension contribution.

Risks should be phrased as current posture, not blame.

Good:

```text
Guard is not running in pull request CI.
Audit has no explain coverage for review decisions.
Handoff packet evidence is older than seven days.
```

Avoid:

```text
Your project is unsafe.
Your AI workflow is bad.
```

## Next Steps

Lite output should include up to three next steps.

Next steps should be mechanical and achievable:

```text
Add Guard check to pull_request CI.
Create a fresh handoff packet for the current repo.
Generate an audit summary for the current evidence window.
Resolve unresolved Guard block decisions.
```

Next steps are guidance, not automatic remediation.

## Output Shape

A Lite result should contain:

```json
{
  "model_id": "shield-lite-scoring.v0",
  "score": 76,
  "band": "yellow",
  "self_check": true,
  "sample_window_days": 30,
  "dimension_contributions": {
    "guard": { "standard_level": 8, "weight": 40, "points": 36 },
    "handoff": { "standard_level": 7, "weight": 30, "points": 23 },
    "audit": { "standard_level": 5, "weight": 30, "points": 17 }
  },
  "critical_gates": [],
  "top_risks": [],
  "next_steps": []
}
```

This section defines the scoring output contract. `carves-shield challenge` separately emits `shield-lite-challenge-result.v0` for local challenge packs.

## Privacy Boundary

Lite must be shareable without exposing private code.

Default Lite output may include:

- score
- band
- self-check label
- sample window
- dimension contributions
- Critical Gate ids
- top risks
- next steps
- evidence hash

Default Lite output must not include:

- source code
- raw git diff
- prompts
- model responses
- secrets
- credentials
- private file payloads

## Relationship To Standard

Lite is a projection. Standard is the detailed report.

Rules:

- Lite may use one 0-100 score.
- Standard must keep G/H/A separate.
- Lite should link to or expose the G/H/A source levels when available.
- A Lite score must not be used to hide a weak or missing dimension.
- A Lite score must not claim verified certification in v0.

## Local Challenge Schema

`shield-lite-challenge.v0` defines local challenge fixtures for simple comparable exercises. The required v0 cases are protected path violation, deletion without credible replacement, fake audit evidence, stale handoff packet, privacy leakage flag, missing CI evidence, and oversized patch.

Stable challenge kind values:

```text
protected_path_violation
deletion_without_credible_replacement
fake_audit_evidence
stale_handoff_packet
privacy_leakage_flag
missing_ci_evidence
oversized_patch
```

Each challenge case defines its expected local decision posture and allowed outputs. Challenge results remain local challenge results; they are not certification, hosted verification, public ranking, model safety benchmarking, or semantic source proof.

## What This Model Does Not Do

This model does not:

- implement `carves shield evaluate`
- implement a hosted API
- render badges
- create a public leaderboard
- create a project directory
- certify a project
- benchmark or rate AI model safety
- replace Standard G/H/A reporting
- inspect source code
- require source upload
- prove operating-system sandboxing

Those are separate follow-up cards.
