# Matrix Native Full-Release Feasibility

This spike records the boundary and current implementation posture for the native full-release Matrix lane. It does not remove the current PowerShell release scripts, does not change the current default command contract, and does not promote native full release over the compatibility lane. The current PowerShell full-release output shape is frozen separately in [Matrix Full-Release Artifact Contract](full-release-artifact-contract.md), and the native full-release public summary vocabulary is fixed in [Matrix Native Full-Release Public Contract](native-full-release-public-contract.md).

Current state:

- `carves-matrix proof --lane native-minimal --json` is the explicit native minimal first-run lane.
- `carves-matrix verify <artifact-root> --json` is the native artifact recheck lane.
- `carves-matrix proof --lane full-release` remains the full source-repo release lane and still invokes the PowerShell project-mode and packaged-install proof units.
- `carves-matrix proof --lane native-full-release` is an opt-in native full-release lane that runs native project and packaged producers, assembles the manifest/proof summary, and verifies the bundle.
- Native packaged-install producer work now exists for isolated local package roots, isolated dotnet tool roots, installed command readback, installed peer command orchestration, and redacted packaged summary output.
- Native full-release publication now stages the bundle in a sibling directory and promotes only after verification, preserving any existing requested artifact root if a producer fails.
- Compatibility shorthand remains available: `proof --json` selects native minimal when `--lane` is omitted, and `proof` without `--json` selects full release.
- Full release shorthand must not be described as Linux-native; only the explicit `native-full-release` lane is the native full-release path.

## Current Full-Release Flow

The current full-release proof is a composition of one C# shell and two PowerShell proof units:

1. `CARVES.Matrix.Core/MatrixProofCommand.cs` resolves the Runtime root and artifact root.
2. It invokes `scripts/matrix/matrix-e2e-smoke.ps1` for project-mode evidence.
3. It invokes `scripts/matrix/matrix-packaged-install-smoke.ps1` for packaged-install evidence.
4. It parses both JSON outputs.
5. It writes `matrix-artifact-manifest.json`.
6. It writes `matrix-proof-summary.json`.
7. It runs the native verifier over the finished bundle.

`scripts/matrix/matrix-proof-lane.ps1` is only a CI/source checkout wrapper around `dotnet run ... carves-matrix proof`. The real full-release orchestration is currently split between C# and the two proof-unit scripts.

## Responsibility Map

| Responsibility | Current owner | Classification | Notes |
| --- | --- | --- | --- |
| Resolve Runtime root, artifact root, configuration, and command options | `MatrixProofCommand.cs` | Already native-capable | C# already owns this for `proof`. |
| Invoke project-mode full-release proof | `matrix-e2e-smoke.ps1` via `MatrixProofCommand.cs`; `MatrixNativeProjectProof*` for native producer work | Native producer wired into opt-in lane | The C# producer writes project artifacts and `project-matrix-output.json`; `native-full-release` uses it before manifest assembly. |
| Invoke packaged-install full-release proof | `matrix-packaged-install-smoke.ps1` via `MatrixProofCommand.cs`; `ProduceNativeFullReleasePackagedArtifacts` for native producer work | Native producer wired into opt-in lane | The C# producer builds packages, installs tools, checks the installed Matrix command, runs installed peer commands, writes packaged summary, and preserves compatibility wrapper output. |
| Checked process execution, stdout/stderr capture, timeout handling, and log file materialization | `matrix-checked-process.ps1` and `MatrixProcessInvoker.cs` | Already native-capable for C# callers | `MatrixProcessInvoker` already has bounded process handling, but script callers still use the PowerShell helper. |
| Build the Runtime solution for project-mode proof | `matrix-e2e-smoke.ps1` | Compatibility lane only | The native project producer uses in-process CLI runners and therefore does not shell out to `dotnet build`. |
| Create a temporary external git repository | `matrix-e2e-smoke.ps1` | Already native-capable | Native minimal proof already creates a bounded external repo. Full release would need the richer project fixture. |
| Initialize git identity and baseline commit | `matrix-e2e-smoke.ps1` | Already native-capable | Native minimal proof already runs git setup patterns; full release needs the same with the current richer fixture. |
| Create source and test files for the project-mode target repo | `matrix-e2e-smoke.ps1` | Needs C# orchestration | This is fixture materialization, not product logic. |
| Run Guard init/check and persist Guard decision artifacts | `matrix-e2e-smoke.ps1` | Already native-capable | Native minimal proof already runs the Guard chain; full release needs equivalent artifact naming and output capture. |
| Write the Guard workflow fixture used by Audit evidence | `matrix-e2e-smoke.ps1` | Needs C# orchestration | The fixture must remain summary-only and deterministic. |
| Draft, replace, and inspect the Handoff packet | `matrix-e2e-smoke.ps1` | Needs C# orchestration | The generated ready packet is release-fixture behavior, not Handoff product logic. |
| Run Audit summary, timeline, explain, and evidence | `matrix-e2e-smoke.ps1` | Already native-capable | Audit CLI commands are available; native full release needs the same command sequence and artifact writes. |
| Run Shield evaluate and badge | `matrix-e2e-smoke.ps1` | Already native-capable | Shield CLI commands are available; native full release needs the same output shape. |
| Write `project/matrix-summary.json` with redacted public fields | `matrix-e2e-smoke.ps1`; `MatrixNativeProjectProofArtifacts.cs` | Native producer implemented | The C# writer preserves the project summary and `project-matrix-output.json` compatibility shape. |
| Build local `.nupkg` packages for Guard, Handoff, Audit, Shield, and Matrix CLI projects | `matrix-packaged-install-smoke.ps1`; `MatrixNativePackaging*` primitives | Native producer implemented | C# builds packages into an isolated package root with bounded process capture. |
| Install local dotnet tools from the generated package root | `matrix-packaged-install-smoke.ps1`; `MatrixNativePackaging*` primitives | Native producer implemented | C# installs into an isolated tool root and discovers command shims cross-platform. |
| Run installed command-chain proof with installed peer command paths | `matrix-packaged-install-smoke.ps1`; `MatrixNativePackagedProof*` producer | Native producer implemented | The native producer checks the installed Matrix command and then runs Guard, Handoff, Audit, and Shield through installed command paths without invoking the PowerShell e2e script. |
| Write `packaged/matrix-packaged-summary.json` with redacted package/tool paths | `matrix-packaged-install-smoke.ps1`; `MatrixNativePackagedProofArtifacts.cs` | Native producer implemented | The C# packaged summary uses redacted local package/tool roots and command names rather than local shim paths. |
| Write `project-matrix-output.json` and `packaged-matrix-output.json` wrapper outputs | `MatrixProofCommand.cs` plus scripts | Already native-capable | C# already captures script stdout to these wrapper artifacts. Native parity should keep equivalent artifacts for release continuity. |
| Write and verify `matrix-artifact-manifest.json` | `MatrixArtifactManifestWriter` and verifier | Already native-capable | C# already owns manifest writing, privacy checks, path policy, and hash/size verification. |
| Build and verify `matrix-proof-summary.json` | `MatrixProofSummaryBuilder` and verifier | Already native-capable | C# already owns the public summary readback contract and full-release semantic verification. |
| Publish native full-release output atomically | `MatrixNativeFullReleaseProof*` | Native producer implemented | The native lane writes to sibling staging, promotes verified bundles, and preserves failed staging evidence outside the requested artifact root. |
| Preserve script-based release smoke compatibility | PowerShell scripts | Intentionally remains script-only | The current scripts should stay as the compatibility lane until native parity is implemented and tested. |
| External pilot catalog and cross-platform pilot verification scripts | PowerShell scripts | Intentionally remains script-only | These are adjacent release evidence lanes, not the minimal native full-release target. |

## Minimal Native Full-Release Lane

The smallest useful native full-release lane does not try to rewrite every script at once. It adds one C# orchestration path while leaving the current PowerShell lane intact.

Command shape:

```bash
carves-matrix proof --runtime-root . --artifact-root artifacts/matrix/native-full-release --configuration Release --lane native-full-release --json
```

The `native-full-release` lane spelling is now implemented as an explicit opt-in lane. It is not selected by `proof` or `proof --json` shorthand.

Minimal implementation slices:

1. Native project-mode full-release proof.
   Current status: implemented as a bounded C# producer for project artifacts, Guard, Handoff, Audit, Shield, project summary, and project wrapper output. It is exposed through the complete `native-full-release` lane.

2. Native packaged-install proof.
   Current status: implemented as a bounded C# producer for local package build, local tool install, installed Matrix command readback, installed peer command orchestration, packaged summary, and packaged wrapper output. It is exposed through the complete `native-full-release` lane.

3. Native full-release proof assembly.
   Current status: implemented. The lane reuses the existing manifest writer, proof-summary builder, and verifier to assemble the same public full-release bundle shape already verified today.

4. Compatibility shadow run.
   Keep PowerShell full release as the default. Add native full-release CI as an opt-in lane first, compare output contracts, then decide whether to promote it.

## Cost And Risk

Estimated cost:

- Project-mode native orchestration: medium. Most product commands already exist, but the current script has many fixture and artifact naming details.
- Packaged-install native orchestration: medium. The package build/install, command shim discovery, installed command readback, installed peer command orchestration, and packaged summary production now exist.
- Manifest and proof-summary assembly: complete for the opt-in native full-release lane. C# owns these pieces.
- CI rollout: medium. Native full-release should start as a non-blocking or separate lane until parity is proven across Linux and Windows.

Main risks:

- Accidentally changing the public full-release artifact shape.
- Creating a second project fixture that drifts from the PowerShell lane.
- Mixing packaging behavior with project-mode proof behavior in one large orchestrator.
- Accidentally implying that the default full-release shorthand is Linux-native; it still selects the PowerShell compatibility lane.
- Regressing path redaction or summary-only privacy guarantees while moving script summary writers into C#.

Mitigations:

- Keep project-mode and packaged-install native orchestration in separate small files.
- Reuse `MatrixProcessInvoker` for process capture and timeout behavior.
- Reuse the existing manifest writer, proof-summary builder, and verifier.
- Keep black-box tests for the explicit native full-release lane before changing README quickstart defaults.
- Keep PowerShell scripts as the compatibility path until native full-release has cross-platform CI evidence.

## Non-Goals

This spike does not:

- remove `matrix-e2e-smoke.ps1`;
- remove `matrix-packaged-install-smoke.ps1`;
- change `carves-matrix proof` behavior;
- claim that the default `full-release` compatibility lane is Linux-native;
- replace external pilot or cross-platform pilot scripts;
- change public schemas or verifier output.
