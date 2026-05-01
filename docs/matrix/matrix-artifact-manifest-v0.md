# Matrix Artifact Manifest v0

`matrix-artifact-manifest.v0` is the summary-only manifest for a CARVES Matrix proof bundle.

The manifest lets a reviewer verify which local proof artifacts were produced, their hashes, their sizes, their producers, and the privacy posture attached to each artifact. Matrix also verifies the manifest against the current local files and reports a verification posture in `matrix-proof-summary.json`. It is provenance metadata only. It does not upload artifacts, certify a project, provide hosted verification, rank models, prove semantic correctness, provide automatic rollback, or provide operating-system sandboxing.

## File Name

Matrix writes the manifest as:

```text
matrix-artifact-manifest.json
```

## Required Top-Level Fields

| Field | Type | Meaning |
| --- | --- | --- |
| `schema_version` | string | Must be `matrix-artifact-manifest.v0`. |
| `created_at` | string | UTC timestamp for manifest creation. |
| `producer` | object | Tool, component, and mode that wrote the manifest. |
| `artifact_root` | string | Portable bundle root marker. Current Matrix writers use `.`; verifiers resolve files from the CLI artifact root or manifest directory. |
| `producer_artifact_root` | string | Redacted local producer root marker. It is diagnostic only and is not trusted for artifact reads. |
| `privacy` | object | Bundle-level summary-only privacy flags. |
| `artifacts` | array | Artifact records covered by the manifest. |

## Required Artifact Fields

Each artifact record must include:

| Field | Type | Meaning |
| --- | --- | --- |
| `artifact_kind` | string | Stable logical role, such as `guard_decision` or `shield_evaluation`. |
| `path` | string | Bundle-relative artifact path. Paths must stay under the verification artifact root and identify a regular file, not a symlink or reparse point. |
| `sha256` | string | Lowercase hex SHA-256 digest of the artifact bytes. |
| `size` | number | File size in bytes. |
| `schema_version` | string | Artifact schema or file contract version. |
| `producer` | string | Product that produced the artifact. |
| `created_at` | string | Manifest timestamp applied to this artifact entry. |
| `privacy_flags` | object | Artifact-level summary-only privacy flags. |

## Required Artifact Coverage

The default Matrix proof manifest covers these artifact roles:

| Artifact kind | Default path | Producer |
| --- | --- | --- |
| `guard_decision` | `project/decisions.jsonl` | `carves-guard` |
| `handoff_packet` | `project/handoff.json` | `carves-handoff` |
| `audit_evidence` | `project/shield-evidence.json` | `carves-audit` |
| `shield_evaluation` | `project/shield-evaluate.json` | `carves-shield` |
| `shield_badge_json` | `project/shield-badge.json` | `carves-shield` |
| `shield_badge_svg` | `project/shield-badge.svg` | `carves-shield` |
| `matrix_summary` | `project/matrix-summary.json` | `carves-matrix` |

Optional entries may record wrapper outputs such as `project-matrix-output.json`, `packaged-matrix-output.json`, and `packaged/matrix-packaged-summary.json` when those files exist. For `proof_mode=full_release`, `verify` treats `packaged/matrix-packaged-summary.json` as required source coverage for the public proof summary even though native/minimal bundles may omit it.

`matrix-proof-summary.json` references the manifest path, schema version, SHA-256 digest, and verification posture separately. To avoid a circular hash relationship, Matrix does not put the proof summary hash inside the manifest. Instead, `verify` treats the proof summary as a public readback surface and validates its schema, proof mode, proof capabilities, portable artifact root marker, manifest reference, summary-only privacy posture, public non-claims, native proof readback fields, full release project/packaged/trust-chain readback fields, and closed public field contract against hash-covered artifacts. Unknown public proof-summary fields fail verification with `summary_unknown_field:<json_path>`.

Public Matrix metadata redacts local filesystem roots by default. The manifest writes `artifact_root` as `.` and writes `producer_artifact_root` as `<redacted-local-artifact-root>`. Matrix proof summaries and matrix summaries likewise use portable or bundle-relative root markers instead of usernames, home directories, workspace mounts, or CI runner absolute paths. Full release script summaries redact target repository, package root, tool root, and installed command paths before writing public JSON. Cross-platform pilot verification checkpoints and generated pilot bundles also use redacted runtime roots plus portable or bundle-relative artifact roots. `verify` still resolves files from the CLI artifact root or the manifest directory; it does not trust redacted producer metadata for artifact reads.

Matrix treats manifest artifacts and native proof materialization sources as regular-file inputs. Manifest generation rejects symlink or reparse-point artifacts before hashing them, manifest verification reports `artifact_reparse_point_rejected` before hashing linked artifacts, and native proof materialization rejects symlink or reparse-point sources before copying them into the public bundle. Linux regression tests cover bundle-local symlinks that point outside the artifact root; Windows reparse-point handling uses the same `FileAttributes.ReparsePoint` rejection path without requiring brittle elevated symlink creation in CI.

`carves-matrix verify <artifact-root> --json` is the Linux-native public artifact recheck path. It reads an existing bundle and emits `matrix-verify.v0`. It validates the manifest, required artifact entries, hashes, schema/producer/path metadata, privacy flags, Shield score readback shape, Shield evaluation consumed-evidence hash binding, and proof-summary public readback consistency without rerunning Guard, Handoff, Audit, Shield, Matrix proof scripts, `pwsh`, or repo-local release lanes.

The verifier uses manifest-bound verified reads before trusting semantic fields. For `proof_mode=native_minimal`, it requires the native source summary artifact to have manifest coverage, reads the `project/matrix-summary.json` byte snapshot, compares its byte length and SHA-256 with the manifest entry, parses that same byte snapshot as JSON, and compares public `native` fields plus `trust_chain_hardening` against verifier-computed gates. For `proof_mode=full_release`, it applies the same verified-byte process to `project/matrix-summary.json` and `packaged/matrix-packaged-summary.json`, then compares proof summary `project`, `packaged`, and `trust_chain_hardening` fields with those verified sources and the verifier-computed gate projection. In both modes, `proof_capabilities` records the lane, backend, coverage, and environment requirements and is checked against the proof mode so native minimal cannot be upgraded into packaged/full-release coverage by editing JSON. Shield evaluation semantics use the same manifest-bound verified read path before score fields are trusted. Its `trust_chain_hardening` object records verifier-computed gates, each gate's reason, and the issue codes that caused failure.

`matrix-verify.v0` keeps detailed issue `code` values for exact diagnostics and also exposes stable `reason_code` categories for automation. Top-level `reason_codes` and gate-level `reason_codes` are the distinct categories derived from the issues:

| Reason code | Failure family |
| --- | --- |
| `missing_artifact` | Missing manifest, summary, required entry, verified source-summary entry, verified Shield evaluation entry, or referenced artifact file. |
| `hash_mismatch` | Artifact hash, artifact size, proof-summary manifest hash mismatch, source-summary hash/size mismatch, or Shield evaluation hash/size mismatch during semantic read. |
| `schema_mismatch` | Manifest shape, required path/schema/producer metadata, closed proof-summary field contract, trust-chain hardening projection, duplicate verified semantic entries, symlink/reparse source artifacts, summary reference, or readable JSON shape mismatch. |
| `privacy_violation` | Summary-only privacy posture is missing or violated. |
| `unverified_score` | Shield evaluation score readback is present but not verified as an `ok` local self-check result bound to the included Audit evidence hash. |
| `unsupported_version` | Manifest schema version is unsupported by this verifier. |

`verify` exits `0` only when the bundle is verified, exits `1` for verification failures, and exits `2` for usage or argument errors. JSON output includes `exit_code` with the same success/failure mapping for machine readers.

For large artifacts, the manifest keeps only metadata. It records hashes and file sizes for artifacts such as `project/decisions.jsonl`, and the verifier recomputes those values from local files. Large artifact bytes are not inlined into `matrix-artifact-manifest.json`. Current large-log stress limits and Audit Guard JSONL behavior are recorded in `docs/matrix/large-log-stress.md`.

## Privacy Flags

Every bundle and artifact entry must keep the local summary-only posture explicit:

```json
{
  "summary_only": true,
  "source_included": false,
  "raw_diff_included": false,
  "prompt_included": false,
  "model_response_included": false,
  "secrets_included": false,
  "credentials_included": false,
  "private_payload_included": false,
  "customer_payload_included": false,
  "hosted_upload_required": false,
  "certification_claim": false,
  "public_leaderboard_claim": false
}
```

These flags are part of the public Matrix non-claim boundary. Allowed summary-only artifacts keep every forbidden payload class false: source, raw diff, prompt, model response, secret, credential, private payload, customer payload, hosted upload requirement, certification claim, and public leaderboard claim. `VerifyManifest` returns `privacy_gate_failed` when the bundle or any artifact entry omits those flags, sets `summary_only` to anything other than true, or sets a forbidden flag to true.

These flags do not prove that an arbitrary external artifact is safe to publish; they record the expected posture for artifacts produced by the local Matrix proof lane.

## Schema And Fixture

- `docs/matrix/schemas/matrix-artifact-manifest.v0.schema.json`
- `docs/matrix/schemas/matrix-proof-summary.v0.schema.json`
- `docs/matrix/examples/matrix-artifact-manifest.v0.schema-example.json`
- `docs/matrix/examples/matrix-proof-summary.v0.schema-example.json`

The files ending in `.schema-example.json` are schema examples only. They are not runnable verification bundles and should not be passed directly to `carves-matrix verify`.
