# CARVES Shield Evidence Schema v0

Status: schema draft for CARD-757.

This document defines the evidence contract for CARVES Shield. The same evidence envelope should be usable by:

- local CLI evaluation
- future hosted API evaluation
- Lite evaluation
- Standard Guard/Handoff/Audit matrix evaluation
- badge and report generation
- GitHub Actions output

This card defines evidence only. It does not define scoring, implement `carves shield evaluate`, or introduce hosted API behavior.

## Contract Files

```text
docs/shield/schemas/shield-evidence-v0.schema.json
docs/shield/examples/shield-evidence-lite.example.json
docs/shield/examples/shield-evidence-standard.example.json
docs/shield/examples/shield-evidence-insufficient-audit.example.json
```

The schema id is:

```text
shield-evidence.v0
```

## Privacy Boundary

Evidence is summary-first. It must not require source upload by default.

Default evidence must not include:

- source file contents
- raw git diff text
- prompts
- model responses
- secrets
- credentials
- environment variable values
- private file payloads

Allowed evidence includes:

- booleans such as policy present or schema valid
- counts such as changed decision count or malformed record count
- normalized path prefixes such as `.git/`, `.env`, `secrets/`
- stable rule ids
- timestamps
- CI provider and workflow path metadata
- hash or fingerprint values that do not reveal source
- handoff and audit metadata

If a future mode wants richer evidence, it must be opt-in and visibly separate from this default schema posture.

## Top-Level Envelope

Every evidence document has one envelope:

```json
{
  "schema_version": "shield-evidence.v0",
  "evidence_id": "shev_20260414_example",
  "generated_at_utc": "2026-04-14T10:45:00Z",
  "mode_hint": "standard",
  "sample_window_days": 30,
  "repository": {},
  "privacy": {},
  "dimensions": {},
  "provenance": {}
}
```

### `mode_hint`

Allowed values:

```text
lite
standard
both
```

`mode_hint` is not a scoring result. It only tells an evaluator which output the caller is asking for.

### `sample_window_days`

The sample window is the number of days used for sustained evidence. It may be omitted for configuration-only evidence.

Standard reports should preserve the sample window in labels when sustained evidence is used:

```text
◈ CARVES G8·H5·A3 /30d ✓
```

## Repository Evidence

Repository evidence identifies the target without exposing source.

Recommended fields:

```json
{
  "host": "github",
  "visibility": "public",
  "default_branch": "main",
  "repo_id_hash": "sha256:example",
  "commit_sha": "0123456789abcdef"
}
```

`repo_id_hash` may be used instead of an owner/name string for private repositories.

## Privacy Evidence

Privacy evidence states what the collector did and did not include.

Required posture:

```json
{
  "source_included": false,
  "raw_diff_included": false,
  "prompt_included": false,
  "secrets_included": false,
  "redaction_applied": true,
  "upload_intent": "local_only"
}
```

The evaluator also recognizes these optional explicit privacy aliases. If present, they must be `false`:

```text
credential_included
credentials_included
model_response_included
model_responses_included
private_payload_included
private_file_payloads_included
```

They exist only to make accidental rich-evidence claims fail closed. A normal local evidence file may omit them.

Allowed `upload_intent` values:

```text
local_only
api_evidence_summary
```

The default should be `local_only` or `api_evidence_summary`.

`api_opt_in_rich_evidence` is reserved by the separate API privacy contract, but it is not accepted by the local `shield-evidence.v0` evaluator contract.

## Dimension Evidence

The `dimensions` object has exactly three named dimensions:

```text
guard
handoff
audit
```

Each dimension is represented explicitly. A dimension can be enabled independently; when a project does not provide evidence for a dimension, the evidence should keep that dimension present with `enabled: false`.

Evidence absence is not judged by this schema. Later rubric cards decide how missing or disabled dimension evidence affects Lite or Standard levels.

### Unknown And Insufficient Evidence

Evidence producers must be conservative:

- A count may report what was actually observed.
- A boolean may be `true` only when the producer observed explicit evidence for that claim.
- Missing support must be represented as `false`, `null`, omitted optional fields, or a lower count. Do not fill positive fields from related-but-weaker evidence.

For example, a readable Guard decision log proves that decisions were read. It does not prove that every block or review decision has Audit explain coverage, that a summary report artifact was generated, or that the log is append-only.

Schema-required fields are only the envelope and the three dimension objects. Scoring fields inside each dimension are optional at the schema layer because first integrations often start with partial evidence. The evaluator treats missing scoring fields as insufficient evidence for the next predicate; it does not infer a positive value.

Examples:

| Missing field | Evaluator behavior |
| --- | --- |
| `dimensions.guard.policy.present` | Guard remains `G0`. |
| `dimensions.guard.ci.fails_on_review_or_block` | Guard cannot reach `G5`. |
| `dimensions.handoff.latest_packet.age_days` | Handoff cannot prove freshness for `H5`. |
| `dimensions.handoff.packets.window_days` and top-level `sample_window_days` | Handoff cannot claim sustained `H8` or `H9`. |
| `dimensions.audit.coverage.*_explain_covered_count` | Audit cannot claim explain coverage; block decisions without coverage trigger `CA-03`. |
| `dimensions.audit.reports.summary_generated_in_window` | Audit cannot reach `A7`. |

## Guard Evidence

Guard evidence measures output governance.

Key sections:

- `policy`
- `ci`
- `decisions`
- `proofs`

Example:

```json
{
  "enabled": true,
  "policy": {
    "present": true,
    "path": ".ai/guard-policy.json",
    "schema_valid": true,
    "schema_version": 1,
    "policy_id": "starter-guard-policy",
    "effective_protected_path_prefixes": [".git/", ".env", "secrets/"],
    "protected_path_action": "block",
    "outside_allowed_action": "review",
    "fail_closed": true,
    "review_is_passing": false,
    "emit_evidence": true,
    "change_budget_present": true,
    "dependency_policy_present": true,
    "source_test_rule_present": true,
    "mixed_feature_refactor_rule_present": true
  },
  "ci": {
    "detected": true,
    "provider": "github_actions",
    "workflow_paths": [".github/workflows/carves-guard.yml"],
    "guard_check_command_detected": true,
    "fails_on_review_or_block": true
  },
  "decisions": {
    "present": true,
    "window_days": 30,
    "allow_count": 18,
    "review_count": 4,
    "block_count": 2,
    "unresolved_review_count": 0,
    "unresolved_block_count": 0
  }
}
```

## Handoff Evidence

Handoff evidence measures input governance.

Key sections:

- `packets`
- `latest_packet`
- `continuity`

Example:

```json
{
  "enabled": true,
  "packets": {
    "present": true,
    "count": 3,
    "window_days": 30
  },
  "latest_packet": {
    "schema_valid": true,
    "age_days": 2,
    "repo_orientation_fresh": true,
    "target_repo_matches": true,
    "current_objective_present": true,
    "remaining_work_present": true,
    "must_not_repeat_present": true,
    "completed_facts_with_evidence_count": 5,
    "decision_refs_count": 2,
    "confidence": "medium"
  },
  "continuity": {
    "session_switch_count": 3,
    "session_switches_with_packet": 3,
    "stale_packet_count": 0
  }
}
```

If Handoff is not enabled, use:

```json
{
  "enabled": false
}
```

## Audit Evidence

Audit evidence measures history governance.

Key sections:

- `log`
- `records`
- `coverage`
- `reports`

Example:

```json
{
  "enabled": true,
  "log": {
    "present": true,
    "readable": true,
    "schema_supported": true,
    "append_only_claimed": false,
    "integrity_check_passed": true
  },
  "records": {
    "record_count": 24,
    "malformed_record_count": 0,
    "future_schema_record_count": 0,
    "earliest_recorded_at_utc": "2026-03-15T00:00:00Z",
    "latest_recorded_at_utc": "2026-04-14T10:45:00Z",
    "records_with_rule_id_count": 24,
    "records_with_evidence_count": 24
  },
  "coverage": {
    "block_decision_count": 2,
    "block_explain_covered_count": 2,
    "review_decision_count": 4,
    "review_explain_covered_count": 4
  },
  "reports": {
    "summary_generated_in_window": true,
    "change_report_generated_in_window": false,
    "failure_pattern_distribution_present": false
  }
}
```

`append_only_claimed` is stronger than `integrity_check_passed`. Set it to `true` only when the producer has explicit append-only proof distinct from basic parsing and integrity checks. Local Audit discovery should normally emit `false`.

`coverage.block_explain_covered_count` and `coverage.review_explain_covered_count` count explicit explain coverage. They must not simply mirror `block_decision_count` or `review_decision_count`. If coverage was not observed, use `0` even when decision counts are non-zero.

`reports.summary_generated_in_window`, `reports.change_report_generated_in_window`, and `reports.failure_pattern_distribution_present` are report-artifact claims. Set them to `true` only when the producer observed that report evidence inside the sample window.

Conservative local evidence with insufficient Audit support can look like this:

```json
{
  "enabled": true,
  "log": {
    "present": true,
    "readable": true,
    "schema_supported": true,
    "append_only_claimed": false,
    "integrity_check_passed": true
  },
  "records": {
    "record_count": 3,
    "malformed_record_count": 0,
    "future_schema_record_count": 0,
    "records_with_rule_id_count": 3,
    "records_with_evidence_count": 3
  },
  "coverage": {
    "block_decision_count": 1,
    "block_explain_covered_count": 0,
    "review_decision_count": 1,
    "review_explain_covered_count": 0
  },
  "reports": {
    "summary_generated_in_window": false,
    "change_report_generated_in_window": false,
    "failure_pattern_distribution_present": false
  }
}
```

## Provenance

Provenance describes how evidence was produced.

```json
{
  "producer": "carves-cli",
  "producer_version": "0.2.0-beta.1",
  "generated_by": "local",
  "source": "working_tree_scan",
  "evidence_hash": "sha256:example",
  "warnings": []
}
```

`evidence_hash` should hash the evidence document after redaction. It is not a source hash.

## Versioning

The schema version is a string:

```text
shield-evidence.v0
```

Future breaking changes must use a new version. Evaluators should fail closed or return an unsupported-schema result for unknown major versions.

## What This Schema Does Not Do

This schema does not:

- assign scores
- decide Lite bands
- decide Standard 0-9 levels
- render badges
- upload evidence
- certify a project
- prove source code correctness
- prove operating-system sandboxing

Those are separate follow-up cards.
