# CARVES Shield API Privacy Contract v0

Status: contract draft for CARD-760.

This document defines the future CARVES Shield API/privacy contract. It is a product and protocol boundary, not an implementation.

The contract describes how a local collector or CI job may send `shield-evidence.v0` summary evidence to a future evaluator and receive Lite, Standard, or combined results without uploading source code by default.

This card does not implement a hosted API, `carves shield evaluate`, authentication, billing, persistence, network code, badge rendering, or a public directory.

## Contract Files

```text
docs/shield/evidence-schema-v0.md
docs/shield/standard-gha-rubric-v0.md
docs/shield/lite-scoring-model-v0.md
docs/shield/contracts/shield-api-privacy-contract-v0.json
```

The contract id is:

```text
shield-api-privacy-contract.v0
```

## Default Flow

The default flow is evidence summary first:

```text
local collector -> shield-evidence.v0 summary -> future Shield evaluator -> Lite/Standard result
```

The default flow must not upload:

- source code
- raw git diff
- prompts
- model responses
- secrets
- credentials
- environment variable values
- private file payloads

The future evaluator should only need normalized evidence fields, counts, booleans, stable rule ids, timestamps, safe metadata, and hashes that do not reveal private source.

## Future Endpoint Contract

The default future endpoint shape is:

```text
POST /v0/shield/evaluate
```

This endpoint is not implemented by this card.

Request body:

```json
{
  "contract_version": "shield-api-privacy-contract.v0",
  "request_id": "shreq_20260414_example",
  "requested_outputs": ["lite", "standard"],
  "evaluation_posture": "self_check",
  "privacy_posture": "evidence_summary",
  "evidence": {}
}
```

Allowed `requested_outputs` values:

```text
lite
standard
combined
```

Allowed `evaluation_posture` values:

```text
self_check
verified
third_party_verified
```

For v0, only `self_check` is defined. `verified` and `third_party_verified` are reserved terms, not active claims.

Allowed `privacy_posture` values:

```text
evidence_summary
opt_in_rich_evidence
```

The default is `evidence_summary`.

## Evidence Summary Request

`evidence_summary` requests must contain a valid `shield-evidence.v0` document.

Required default privacy values:

```json
{
  "source_included": false,
  "raw_diff_included": false,
  "prompt_included": false,
  "secrets_included": false
}
```

The request should be rejected if any of those fields are `true`.

`privacy.upload_intent` should be one of:

```text
local_only
api_evidence_summary
```

`api_opt_in_rich_evidence` is not part of the default path.

## Opt-In Rich Evidence

`opt_in_rich_evidence` is reserved for future work.

Rules:

- It must be explicitly requested by the caller.
- It must be visibly separate from default evidence summary evaluation.
- It must document exactly what additional material is sent.
- It must provide a redaction story before any upload.
- It must not be silently enabled by policy defaults.
- It must not be required to receive Lite or Standard self-check results.

This card does not define or implement rich evidence payloads.

## Response Shapes

All responses should include:

```json
{
  "contract_version": "shield-api-privacy-contract.v0",
  "request_id": "shreq_20260414_example",
  "result_id": "shres_20260414_example",
  "evaluation_posture": "self_check",
  "privacy_posture": "evidence_summary",
  "status": "ok",
  "certification": false
}
```

### Lite Response

```json
{
  "status": "ok",
  "outputs": {
    "lite": {
      "model_id": "shield-lite-scoring.v0",
      "score": 76,
      "band": "yellow",
      "self_check": true,
      "critical_gates": [],
      "top_risks": [],
      "next_steps": []
    }
  }
}
```

### Standard Response

```json
{
  "status": "ok",
  "outputs": {
    "standard": {
      "rubric_id": "shield-standard-gha-rubric.v0",
      "label": "CARVES G8.H5.A3 /30d PASS",
      "dimensions": {
        "guard": { "level": 8, "critical_gates": [] },
        "handoff": { "level": 5, "critical_gates": [] },
        "audit": { "level": 3, "critical_gates": [] }
      },
      "overall_score": null
    }
  }
}
```

`overall_score` must be `null` for Standard.

### Combined Response

`combined` returns both Lite and Standard outputs in the same response. It must still preserve Standard G/H/A details.

### Unsupported Schema Response

```json
{
  "status": "unsupported_schema",
  "error": {
    "code": "unsupported_schema_version",
    "message": "The evidence schema version is not supported.",
    "supported_versions": ["shield-evidence.v0"]
  }
}
```

### Invalid Privacy Response

```json
{
  "status": "invalid_privacy_posture",
  "error": {
    "code": "default_upload_forbidden",
    "message": "Default evidence summary requests must not include source, raw diff, prompt, secret, credential, or private file payloads.",
    "violations": ["privacy.raw_diff_included"]
  }
}
```

### Rate Limit Style Response

```json
{
  "status": "rate_limited",
  "error": {
    "code": "rate_limited",
    "retry_after_seconds": 60
  }
}
```

Rate limiting is listed as an API outcome shape only. This card does not implement rate limiting.

## Self-Check Versus Verified

Shield v0 API responses are self-check unless a future verified program is explicitly defined.

Default response language:

```json
{
  "evaluation_posture": "self_check",
  "certification": false
}
```

Reserved future language:

```text
verified
third_party_verified
```

Reserved terms must not be emitted by v0 self-check evaluation.

Bad:

```text
CARVES certified this project.
CARVES verified this repository.
```

Good:

```text
CARVES Shield Lite self-check completed.
CARVES Shield Standard self-check completed from summary evidence.
```

## Retention And Logging Posture

Default v0 privacy posture:

- request evidence may be processed to produce a result
- raw source is not accepted by default
- raw diff is not accepted by default
- prompts and model responses are not accepted by default
- secrets and credentials are not accepted by default
- result metadata may include request id, result id, contract version, model/rubric ids, status, and evidence hash
- retention policy is not implemented by this card

A future hosted service must define retention before launch. Until then, this contract is only a boundary for later implementation.

## Local And CI Use

Local and GitHub Actions flows should prefer:

```text
local repo scan -> local evidence summary -> optional future API evaluation -> local/CI report
```

For private repositories, use `repo_id_hash` instead of public owner/name when possible.

## What This Contract Does Not Do

This contract does not:

- implement a hosted API
- implement `carves shield evaluate`
- add authentication
- add billing
- add persistence
- add network transport
- define storage retention
- render badges
- create a public leaderboard
- create a public project directory
- certify projects
- require source upload
- inspect source code
- prove operating-system sandboxing

Those are separate follow-up cards.
