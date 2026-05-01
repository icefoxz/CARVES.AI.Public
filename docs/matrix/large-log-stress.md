# CARVES Matrix Large Log Stress Boundary

Status: local stress boundary for large Guard logs and Matrix manifests.

This document records the current bounded behavior for large local proof artifacts.

## Audit Guard JSONL Limits

Audit reads Guard decisions from:

```text
.ai/runtime/guard/decisions.jsonl
```

Current bounded limits:

| Limit | Value | Behavior |
| --- | ---: | --- |
| Guard JSONL tail window | 1000 lines | Audit keeps the most recent 1000 lines and marks the input degraded when older lines are skipped. |
| Guard decision record size | 131072 bytes | Audit skips an oversized JSONL record, increments `oversized_record_count`, and marks the input degraded. |

Oversized records are explicit, not silent. `carves-audit summary --json` exposes `guard.diagnostics.oversized_record_count` and `guard.diagnostics.max_record_byte_count`. `carves-audit evidence --json` records `dimensions.audit.records.oversized_record_count` and adds a provenance warning such as:

```text
guard_oversized_records:1
```

If an explicit Guard decision path contains only unusable oversized, malformed, or future-schema records, Audit returns `input_error`.

## Large Patch Metadata

Audit may read large summary-only patch metadata in a Guard decision record when the JSONL record stays under the per-record byte limit. The record may include high `patch_stats` counts and many path summaries, but Matrix and Shield outputs must remain summary-only.

Large patch metadata must not include source code, raw git diffs, prompts, model responses, secrets, credentials, or customer payloads.

## Matrix Manifest Stress

Matrix manifest verification hashes artifact bytes through streams and records file sizes in `matrix-artifact-manifest.v0`. The verifier reports explicit reason codes when a large artifact changes:

| Change | Verification code | Reason code |
| --- | --- | --- |
| Artifact bytes changed | `artifact_hash_mismatch` | `hash_mismatch` |
| Artifact size changed | `artifact_size_mismatch` | `hash_mismatch` |
| Artifact missing | `artifact_missing` | `missing_artifact` |
| Privacy flag violated | `privacy_forbidden_flag_true:<flag>` | `privacy_violation` |

The manifest JSON itself should remain metadata-only and bounded to artifact entries. It should not inline large Guard JSONL contents or raw diffs.

## Known Limitations

These stress checks are local bounded fixtures. They do not prove unlimited log handling, large production monorepo coverage, semantic source correctness, hosted verification, public certification, operating-system sandboxing, automatic rollback, or network isolation.
