# CARVES Shield Lite Challenge Schema v0

Status: local challenge schema for CARD-814.

`shield-lite-challenge.v0` defines a small, comparable set of local challenge cases for exercising CARVES workflow governance self-checks. It is a fixture contract, not a scoring engine.

The schema path is:

```text
docs/shield/schemas/shield-lite-challenge-v0.schema.json
```

Example fixtures are:

```text
docs/shield/examples/shield-lite-challenge-suite.example.json
docs/shield/examples/shield-lite-starter-challenge-pack.example.json
docs/shield/examples/shield-lite-challenge-result.example.json
```

## Local Runner

Run a challenge pack locally:

```powershell
carves-shield challenge docs/shield/examples/shield-lite-challenge-suite.example.json --json
```

Compatibility wrapper:

```powershell
carves shield challenge docs/shield/examples/shield-lite-challenge-suite.example.json --json
```

The runner emits `shield-lite-challenge-result.v0` with a case breakdown, `passed_count`, `failed_count`, and `pass_rate`. It exits `0` when every case passes and `1` when any case fails. Usage errors exit `2`.

The runner reads the challenge pack only. It does not upload challenge artifacts, inspect source files, read raw git diffs, write badge files, write evaluation files, or collect private project data.

The starter challenge pack includes 10 bounded fixtures:

```powershell
carves-shield challenge docs/shield/examples/shield-lite-starter-challenge-pack.example.json --json
```

The starter quickstart is `docs/shield/lite-challenge-quickstart.md`. JSON and text summary output must remain labeled `local challenge result, not certified safe`.

## Boundary

Challenge results are local challenge results only. They are labeled `local challenge result, not certified safe`. They are not certification, hosted verification, a public leaderboard, a model safety benchmark, semantic source correctness proof, or operating-system sandbox proof.

The challenge schema may describe local fixture mutations and expected local postures. It must not require source code, raw git diffs, prompts, model responses, secrets, credentials, or private file payloads in default challenge output.

## Required Challenge Cases

Every v0 challenge suite must include these cases:

| Challenge kind | Expected local decision posture | Shield Lite ceiling | Required signal |
| --- | --- | --- | --- |
| `protected_path_violation` | `block` | `critical` | Protected path edits are fail-closed. |
| `deletion_without_credible_replacement` | `block` | `critical` | Governance artifact deletion is not treated as improvement without replacement evidence. |
| `fake_audit_evidence` | `reject` | `critical` | Audit evidence must link to local provenance. |
| `stale_handoff_packet` | `review` | `critical` | Stale packet evidence cannot be treated as current handoff truth. |
| `privacy_leakage_flag` | `reject` | `critical` | Forbidden privacy flags prevent shareable output. |
| `missing_ci_evidence` | `review` | `basic` | Missing CI evidence remains visible and cannot imply hosted CI pass. |
| `oversized_patch` | `review` | `basic` | Change-budget pressure remains visible instead of becoming an automatic pass. |

## Case Contract

Each challenge case defines:

- `case_id`: stable case identifier.
- `challenge_kind`: one of the required v0 challenge kinds.
- `fixture`: a summary-only description of local artifact mutations.
- `expected_local_decision_posture`: expected local `decision`, `shield_lite_band_ceiling`, `reason_codes`, and evidence references.
- `allowed_outputs`: local-only outputs that may be shared from the challenge, with `certification=false`.
- `privacy`: explicit false values for source, raw diff, prompt, model response, secret, and credential payload flags.

Allowed outputs are intentionally narrow:

- `challenge_result_json`
- `shield_evaluation_json`
- `shield_badge_json`
- `shield_badge_svg`
- `redacted_evidence_summary`
- `matrix_artifact_manifest`
- `human_summary`

Forbidden outputs include certification, public leaderboard claims, hosted verification claims, source code, raw git diffs, prompts, model responses, secrets, and credentials.

## Relationship To Shield Lite

Shield Lite still owns the `shield-lite-scoring.v0` score projection. A challenge case may state a band ceiling, but it does not recompute the scoring model and does not override Standard G/H/A evidence.

Challenge output can be used to compare whether a local setup responds to known governance failure modes. It cannot convert a local self-check into certification or a public rating.

## Relationship To Matrix

Matrix may carry a challenge suite, challenge result, and related Shield artifacts as local proof-lane artifacts. Matrix does not create the challenge truth, does not alter Shield scoring, and does not turn a challenge result into certification.
