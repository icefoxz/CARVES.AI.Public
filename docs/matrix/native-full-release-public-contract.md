# Matrix Native Full-Release Public Contract

Status: implemented opt-in lane. The project-mode native producer, packaged-install native producer, manifest assembly, proof-summary assembly, and verifier readback are wired behind `proof --lane native-full-release`.

This document fixes the public lane name and proof-summary vocabulary for the native full-release Matrix lane. It does not make that lane the default, and it does not rename or remove the current PowerShell compatibility full-release lane.

## Lane Names

| CLI lane | Current behavior | `proof_mode` | `proof_capabilities.proof_lane` | `proof_capabilities.execution_backend` |
| --- | --- | --- | --- | --- |
| `native-minimal` | Implemented native first-run lane | `native_minimal` | `native_minimal` | `dotnet_runner_chain` |
| `full-release` | Implemented PowerShell compatibility lane | `full_release` | `full_release` | `powershell_release_units` |
| `native-full-release` | Implemented opt-in native full-release lane | `full_release` | `native_full_release` | `dotnet_full_release_runner_chain` |

The distinction is intentional:

- `proof_mode` describes the proof capability level.
- `proof_capabilities.proof_lane` describes the public lane contract.
- `proof_capabilities.execution_backend` describes the producer backend.

Therefore native full release must still use `proof_mode=full_release`. It must not invent a third proof mode such as `native_full_release`.

## CLI Behavior

The CLI accepts `--lane native-full-release` as an explicit opt-in lane. It runs the native project producer and native packaged producer, writes the same public full-release manifest and proof-summary shape, and verifies the finished bundle with the existing Matrix verifier.

Expected behavior:

```bash
carves-matrix proof --lane native-full-release --json
```

The command emits a `matrix-proof-summary.v0` summary with `proof_mode=full_release`, `proof_capabilities.proof_lane=native_full_release`, `execution_backend=dotnet_full_release_runner_chain`, and `requirements.powershell=false`.

Compatibility behavior remains unchanged:

- `carves-matrix proof --lane full-release` remains the current PowerShell full-release lane.
- `carves-matrix proof` without `--json` still selects `full-release`.
- `carves-matrix proof --json` still selects `native-minimal`.
- `native-full-release` is not a default lane.

## Proof Capabilities

Native full release uses this capability block:

```json
{
  "proof_mode": "full_release",
  "proof_capabilities": {
    "proof_lane": "native_full_release",
    "execution_backend": "dotnet_full_release_runner_chain",
    "coverage": {
      "project_mode": true,
      "packaged_install": true,
      "full_release": true
    },
    "requirements": {
      "powershell": false,
      "source_checkout": true,
      "dotnet_sdk": true,
      "git": true
    }
  }
}
```

The native full-release lane writes the same public `project`, `packaged`, `artifact_manifest`, `trust_chain_hardening`, `privacy`, and `public_claims` objects documented in [Matrix Full-Release Artifact Contract](full-release-artifact-contract.md).

The project-mode native producer writes the project-side artifacts and `project-matrix-output.json` compatibility readback. The packaged-install native producer builds local CLI tool packages into an isolated package root, installs them into an isolated tool root, checks the installed Matrix command, runs the installed Guard -> Handoff -> Audit -> Shield chain through installed peer commands, and writes `packaged/matrix-packaged-summary.json` plus `packaged-matrix-output.json`. The native full-release lane then assembles the full manifest and proof summary.

Native full-release publication is staged. Producer output is written to a sibling staging directory first, and only a verified complete bundle is promoted to the requested artifact root. If project production, packaged production, proof assembly, or promotion fails, the requested artifact root is left untouched and the failed staging bundle is moved to an isolated sibling failure directory with `native-full-release-failure.json` evidence.

## Schema And Verifier Gate

The `matrix-proof-summary.v0` schema accepts `proof_capabilities.proof_lane=native_full_release` only inside `proof_mode=full_release`, with `execution_backend=dotnet_full_release_runner_chain` and `requirements.powershell=false`.

The runtime verifier also requires native producer evidence. A PowerShell full-release bundle cannot be relabeled as native by editing only the public summary because native full-release verification checks the manifest-covered source summaries for:

- `project/matrix-summary.json.producer=native_full_release_project`
- `packaged/matrix-packaged-summary.json.producer=native_full_release_packaged`
- full-release coverage with `project_mode=true`, `packaged_install=true`, and `full_release=true`
- `powershell=false`, `source_checkout=true`, `dotnet_sdk=true`, and `git=true`

`full-release` PowerShell compatibility remains accepted with `proof_capabilities.proof_lane=full_release` and `execution_backend=powershell_release_units`.

## Non-Goals

This contract does not:

- promote native full release as the default lane;
- change the public full-release artifact list;
- relax manifest, hash, privacy, symlink, proof capability, or summary consistency checks;
- promote native full release over the PowerShell compatibility lane.
