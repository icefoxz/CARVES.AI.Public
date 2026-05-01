# CARVES Agent Trial V1 Matrix Integration

Status: Phase 4 Matrix integration contract freeze for the proposed Agent Trial V1 product line.

This document defines how Agent Trial V1 local artifacts integrate with Matrix proof and verify. It does not implement submission, receipts, hosted verification, leaderboards, or safety posture scoring.

## Goal

Agent Trial artifacts must become Matrix-covered local proof artifacts without weakening the existing Matrix manifest and verification guarantees.

It answers:

```text
Which trial artifacts should Matrix cover, how should verify treat them, and how do existing Matrix lanes remain compatible?
```

## Trial Artifact Roles

Matrix integration adds these optional-to-core but required-for-trial artifact roles:

| Artifact kind | Default path | Producer | Schema |
| --- | --- | --- | --- |
| `trial_task_contract` | `trial/task-contract.json` | `carves-trial` | `matrix-agent-task.v0` |
| `trial_agent_report` | `trial/agent-report.json` | `carves-trial` or user agent | `agent-report.v0` |
| `trial_diff_scope_summary` | `trial/diff-scope-summary.json` | `carves-trial` | `diff-scope-summary.v0` |
| `trial_test_evidence` | `trial/test-evidence.json` | `carves-trial` | `test-evidence.v0` |
| `trial_result_summary` | `trial/carves-agent-trial-result.json` | `carves-trial` | `carves-agent-trial-result.v0` |

The concrete bundle layout may map from workspace paths into Matrix bundle-relative paths, but the manifest role names must stay stable.

## Manifest Coverage

When a Matrix bundle declares Agent Trial mode, all five trial artifact roles are required.

Each trial artifact manifest entry must include:

- artifact kind;
- bundle-relative path;
- SHA-256;
- size;
- schema version;
- producer;
- created at;
- summary-only privacy flags.

The same Matrix hardening rules apply:

- no symlink or reparse-point artifacts;
- no path traversal;
- no absolute local producer paths in public metadata;
- no missing schema versions;
- no duplicate role entries;
- no raw source, raw diff, prompt response, model response, secret, credential, or customer payload flags.

## Verify Behavior

Matrix verify keeps two modes separate:

1. normal Matrix proof bundle;
2. Matrix proof bundle with Agent Trial artifact coverage.

Normal Matrix bundles remain valid without trial artifacts.

Ordinary verification is the compatibility path:

- `carves-matrix verify <artifact-root>` runs with `verification_mode=ordinary`;
- non-trial bundles return `trial_artifacts.mode=not_present`;
- bundles with loose known `trial/*.json` files outside manifest coverage return explicit `trial_artifacts.mode=loose_files_not_manifested` readback instead of silently claiming those files were verified;
- ordinary verify does not turn loose local trial files into public leaderboard evidence.

Strict Agent Trial verification is explicit:

- `carves-matrix verify <artifact-root> --trial` and `--require-trial` run with `verification_mode=trial`;
- trial mode requires all five trial artifact roles to be present in the manifest;
- trial mode fails when known trial files exist locally but are not manifest-covered;
- trial mode fails when any required trial artifact is missing, mismatched, schema-invalid, privacy-invalid, or inconsistent with the other trial artifacts.

Trial-mode bundles require:

- all trial artifact roles present;
- hash and size matches for each trial artifact;
- schema version matches for each role;
- strict runtime validation against the embedded public schema for each trial artifact;
- identity consistency across `suite_id`, `pack_id`, `pack_version`, `task_id`, `task_version`, `prompt_id`, `prompt_version`, and `challenge_id` wherever those fields are present;
- summary-only privacy posture for each role;
- `trial_result_summary.artifact_hashes` matches the manifest entries;
- `trial_result_summary.matrix_manifest_sha256` matches the manifest being verified or the approved non-circular equivalent;
- `trial_result_summary.matrix_verify_status` is consistent with the verifier result projection.

If a bundle is verified in explicit trial mode but omits required trial artifacts, verify fails with missing-artifact reasons rather than silently downgrading to a normal Matrix bundle.

## Trust-Chain Gate

`matrix-verify.v0.trust_chain_hardening.gates[]` includes a dedicated `trial_artifacts` gate.

The gate behavior is:

- non-trial bundles satisfy the gate with explicit non-applicable compatibility reason text;
- loose local trial files outside manifest coverage satisfy the gate only as ordinary-verify compatibility readback, not as verified trial evidence;
- manifest-claimed trial artifacts satisfy the gate only when all five trial artifacts are manifest-covered and verified;
- explicit `--trial` / `--require-trial` verification fails the gate when any required trial artifact is missing or invalid;
- trial artifact issue codes and stable reason codes are projected into the gate for automation.

This keeps `trust_chain_hardening.gates_satisfied` aligned with trial artifact failures instead of letting downstream readers see a green trust-chain posture while trial verification failed.

## Compatibility Rules

Existing Matrix lanes must remain compatible:

- `proof --lane native-minimal` can continue to omit trial artifacts unless explicitly invoked for Agent Trial mode;
- `proof --lane full-release` can continue to omit trial artifacts unless explicitly invoked for Agent Trial mode;
- `proof --lane native-full-release` can continue to omit trial artifacts unless explicitly invoked for Agent Trial mode;
- `verify <artifact-root>` keeps accepting existing non-trial bundles;
- `verify <artifact-root> --trial` is the strict local equivalent expected by submit-gate validation before server-issued authority checks;
- public proof summary non-claims remain unchanged.

Agent Trial integration is additive. It must not turn Matrix into the owner of Guard, Handoff, Audit, Shield, prompt, model, or leaderboard truth.

## Trial Mode Marker

A trial-enabled Matrix bundle should expose an explicit marker, for example:

```json
{
  "trial": {
    "enabled": true,
    "schema_version": "carves-agent-trial-result.v0",
    "suite_id": "official-agent-dev-safety",
    "pack_version": "1.0.0",
    "prompt_version": "1.0.0",
    "challenge_id": "mch_01"
  }
}
```

The exact public summary placement can be finalized during implementation, but it must remain summary-only and closed-schema checked.

## Tamper Detection

Matrix verify must detect:

- missing trial artifact;
- changed trial artifact bytes;
- schema mismatch;
- closed-schema violations, including unknown fields, wrong consts, wrong enums, wrong types, and invalid relative paths;
- hash mismatch between trial result summary and manifest-covered artifacts;
- prompt or pack version mismatch between task contract, challenge metadata, and trial result summary;
- task, prompt, pack, or challenge identity mismatch across task contract, agent report, diff summary, test evidence, and trial result summary;
- forbidden privacy flag;
- unknown public fields if trial fields are exposed in a closed public summary contract.

Tamper detection only proves local artifact consistency. It does not prove producer identity, model identity, semantic correctness, or that the user did not control the local environment.

## Submit Preparation

Phase 4 Matrix integration prepares Phase 6 submission by ensuring a server can later receive summary-only artifacts and verify:

- Matrix manifest hash;
- Matrix proof summary hash;
- task contract hash;
- agent report hash;
- diff scope summary hash;
- test evidence hash;
- trial result hash.

The server receipt chain is not part of Phase 4.

## Phase 4 Acceptance Mapping

TRIAL-V1-042 is satisfied by this document:

- Agent Trial artifacts required for Matrix coverage are frozen;
- Matrix verify behavior for trial-enabled bundles is defined;
- tampering or missing trial artifacts must be detected;
- existing Matrix native minimal and full-release paths remain compatible because trial integration is explicit and additive.
