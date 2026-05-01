# Matrix Full-Release Artifact Contract

Status: current PowerShell compatibility contract.

This document freezes the public and verifier-consumed artifact shape of `carves-matrix proof --lane full-release` and the equivalent `native-full-release` proof-summary shape. It is a characterization document only. It does not claim that the default PowerShell compatibility lane is Linux-native, and it does not replace `scripts/matrix/matrix-e2e-smoke.ps1` or `scripts/matrix/matrix-packaged-install-smoke.ps1`.

## Producer Boundaries

The current full-release lane has three producer layers:

| Layer | Current owner | Output role |
| --- | --- | --- |
| Project-mode proof unit | `scripts/matrix/matrix-e2e-smoke.ps1`; native producer in `src/CARVES.Matrix.Core/MatrixNativeProjectProof*.cs` | Produces project evidence and `project/matrix-summary.json` source summary. The native producer is project-only and is not a complete full-release lane. |
| Packaged-install proof unit | `scripts/matrix/matrix-packaged-install-smoke.ps1`; native producer in `src/CARVES.Matrix.Core/MatrixNativePackagedProof*.cs` | Builds local packages, installs local tools, runs the installed peer command chain, and produces `packaged/matrix-packaged-summary.json`. The native producer is used by the explicit native full-release lane. |
| Bundle assembly | `src/CARVES.Matrix.Core/MatrixProofCommand.cs` | Captures wrapper outputs, writes `matrix-artifact-manifest.json`, writes `matrix-proof-summary.json`, and verifies the final bundle. |

Native full-release work preserves this public shape behind the explicit `native-full-release` lane described in [Matrix Native Full-Release Public Contract](native-full-release-public-contract.md).

## Bundle Files

| Path | Producer | Manifest role | Public role | Verifier role |
| --- | --- | --- | --- | --- |
| `matrix-artifact-manifest.json` | `carves-matrix` | Manifest root | Public hash/size/producer/privacy index | Verifier source for artifact coverage and hash checks. |
| `matrix-proof-summary.json` | `carves-matrix` | Not a manifest artifact entry; carries the manifest hash/posture/issue-count reference | Closed public readback summary | Verifier checks fields against manifest, verified source summaries, and computed gates. |
| `project-matrix-output.json` | `carves-matrix` wrapper around project script stdout | Optional `matrix-script-output.v0` | Compatibility wrapper output | Not the semantic source; preserved for release continuity. |
| `packaged-matrix-output.json` | `carves-matrix` wrapper around packaged script stdout | Optional `matrix-script-output.v0` | Compatibility wrapper output | Not the semantic source; preserved for release continuity. |
| `project/decisions.jsonl` | `carves-guard` | Required `guard-decision-jsonl` | Summary-only Guard decision evidence | Required artifact and Guard decision provenance. |
| `project/handoff.json` | `carves-handoff` | Required `carves-continuity-handoff.v1` | Summary-only handoff evidence | Required artifact and Handoff packet provenance. |
| `project/shield-evidence.json` | `carves-audit` | Required `shield-evidence.v0` | Summary-only Shield input evidence | Required artifact and Shield consumed-evidence hash source. |
| `project/shield-evaluate.json` | `carves-shield` | Required `shield-evaluate.v0` | Summary-only Shield evaluation | Required artifact; score fields and consumed evidence hash are verifier-consumed. |
| `project/shield-badge.json` | `carves-shield` | Required `shield-badge.v0` | Summary-only badge metadata | Required artifact. |
| `project/shield-badge.svg` | `carves-shield` | Required `shield-badge-svg.v0` | Summary-only badge display artifact | Required artifact. |
| `project/matrix-summary.json` | `carves-matrix` project script | Required `matrix-summary.v0` | Project source summary | Verifier reads the manifest-bound byte snapshot and compares public `project` fields. |
| `packaged/matrix-packaged-summary.json` | `carves-matrix` packaged script | Optional manifest entry, but required for `proof_mode=full_release` verification | Packaged-install source summary | Verifier reads the manifest-bound byte snapshot and compares public `packaged` fields. |

## Project Source Summary

`project/matrix-summary.json` is the project-mode source summary produced by `matrix-e2e-smoke.ps1`. Its current top-level contract is:

- `smoke`
- `tool_mode`
- `target_repository`
- `artifact_root`
- `guard_run_id`
- `artifacts`
- `matrix`
- `guard`
- `handoff`
- `audit`
- `shield`
- `privacy`
- `public_claims`

The following fields are verifier-consumed for public proof-summary readback:

- `guard_run_id`
- `shield.status`
- `shield.standard_label`
- `shield.lite_score`
- `shield.consumed_evidence_sha256`
- `matrix.proof_role`
- `matrix.scoring_owner`
- `matrix.alters_shield_score`
- `matrix.consumed_shield_evidence_artifact`
- `matrix.shield_evaluation_artifact`
- `matrix.shield_badge_json_artifact`
- `matrix.shield_badge_svg_artifact`
- `matrix.trust_chain_hardening.*`
- `artifact_root`, after conversion to a public bundle-relative marker

The following fields are public but not used as verifier truth for the public proof summary:

- `artifacts.*`
- `guard.*`
- `handoff.*`
- `audit.*`
- `shield.lite_band`
- `shield.badge_message`
- `privacy.*`
- `public_claims.*`

The following fields must remain redacted or portable:

- `target_repository` is `<redacted-target-repository>`.
- `artifact_root` is `.` in script output and becomes a portable or bundle-relative marker in the public proof summary.

## Packaged Source Summary

`packaged/matrix-packaged-summary.json` is the packaged-install source summary produced by `matrix-packaged-install-smoke.ps1`. Its current top-level contract is:

- `smoke`
- `guard_version`
- `handoff_version`
- `audit_version`
- `shield_version`
- `matrix_version`
- `package_root`
- `tool_root`
- `artifact_root`
- `remote_registry_published`
- `nuget_org_push_required`
- `installed_commands`
- `packages`
- `matrix`
- `privacy`
- `public_claims`
- `pack_command_count`
- `install_command_count`

The following fields are verifier-consumed for public proof-summary readback:

- `guard_version`
- `handoff_version`
- `audit_version`
- `shield_version`
- `matrix_version`
- `matrix.guard_run_id`
- `matrix.shield.status`
- `matrix.shield.standard_label`
- `matrix.shield.lite_score`
- `matrix.shield.consumed_evidence_sha256`
- `matrix.matrix.proof_role`
- `matrix.matrix.scoring_owner`
- `matrix.matrix.alters_shield_score`
- `matrix.matrix.consumed_shield_evidence_artifact`
- `matrix.matrix.shield_evaluation_artifact`
- `matrix.matrix.shield_badge_json_artifact`
- `matrix.matrix.shield_badge_svg_artifact`
- `matrix.matrix.trust_chain_hardening.*`
- `artifact_root`, after conversion to a public bundle-relative marker

The following fields are compatibility or audit-readback metadata, not verifier truth for public proof-summary semantics:

- `installed_commands.*`
- `packages.*`
- `pack_command_count`
- `install_command_count`
- `privacy.*`
- `public_claims.*`

The following fields must remain redacted or portable:

- `package_root` is `<redacted-local-package-root>`.
- `tool_root` is `<redacted-local-tool-root>`.
- `installed_commands.*` use command names such as `carves-guard`, not local shim paths.
- `artifact_root` is `.` in script output and becomes a portable or bundle-relative marker in the public proof summary.

## Proof Summary

`matrix-proof-summary.json` is assembled by C# after the producer scripts finish. For the current PowerShell compatibility full-release lane:

- `schema_version` is `matrix-proof-summary.v0`.
- `proof_mode` is `full_release`.
- `proof_capabilities.proof_lane` is `full_release`.
- `proof_capabilities.execution_backend` is `powershell_release_units`.
- `proof_capabilities.coverage.project_mode`, `packaged_install`, and `full_release` are all `true`.
- `proof_capabilities.requirements.powershell`, `source_checkout`, `dotnet_sdk`, and `git` are all `true`.
- `project` fields are compared to manifest-bound `project/matrix-summary.json`.
- `packaged` fields are compared to manifest-bound `packaged/matrix-packaged-summary.json`.
- `trust_chain_hardening` is verifier-computed, not trusted from producer scripts.

## Migration Gates

Native full-release producers must not be considered compatible until they satisfy all of these gates:

1. They write the same required and full-release-required optional artifact paths.
2. Their project and packaged source summaries expose the verifier-consumed fields listed above.
3. Their public JSON uses portable or redacted paths for local roots and installed tools.
4. Their wrapper outputs preserve the compatibility paths or explicitly map them.
5. `carves-matrix verify <artifact-root> --json` passes without relaxing manifest, hash, symlink, privacy, proof capability, or summary consistency checks.
6. Existing `proof --lane full-release` remains PowerShell compatibility until a separate promotion decision changes that contract.
7. Explicit `proof --lane native-full-release` remains opt-in, emits `proof_capabilities.proof_lane=native_full_release`, and verifier checks native producer markers so a PowerShell bundle cannot be relabeled as native by editing only the public summary.
