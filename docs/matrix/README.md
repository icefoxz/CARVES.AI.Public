# CARVES Matrix

Language: [Chinese](README.zh-CN.md)

CARVES Matrix is a CARVES Runtime proof and trial lane. It frames the Runtime governance capabilities as a local workflow self-check and local consistency proof for AI coding workflow governance, not as a model safety benchmark.

It records that these Runtime capabilities can work together in an external git repository:

```text
Guard -> Handoff -> Audit -> Shield
```

Matrix does not make a fifth governance decision and is not a fifth safety engine. It does not own Guard policy logic, Handoff packet truth, Audit discovery logic, or Shield scoring. It only orchestrates the local consistency proof lane and records whether the chain produced the expected summary-only local artifacts.

## Public First Run

For a first download-and-run experience, use the Runtime-owned `carves test` wrapper before learning Matrix artifact paths:

```powershell
carves test demo
carves test agent
carves test package --output ./carves-agent-trial-pack
carves test result
carves test reset
carves test verify
```

From a source checkout, the equivalent first command is:

```powershell
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test demo --json
```

`carves test demo` is the fully automatic local environment check. `carves test agent` prepares the real agent-assisted run. Both use the same Matrix Agent Trial collector, scorer, verifier, manifest, result card, and history logic, with default output under `./carves-trials/`.

`carves test package --output ./carves-agent-trial-pack` prepares the portable package directory form. It writes `agent-workspace/` for the tested agent, `.carves-pack/` as local scorer authority and package state outside the workspace, `COPY_THIS_TO_AGENT_BLIND.txt` for strict fresh-thread comparisons, `COPY_THIS_TO_AGENT_GUIDED.txt` for learning/practice, `score.sh` / `SCORE.cmd` scoring launchers, `result.sh` / `RESULT.cmd` readback launchers, `reset.sh` / `RESET.cmd` reset launchers, `results/local/` for human readback, and `results/submit-bundle/` as the future upload-ready local output location. The first-run shape is zip, extract, open only `agent-workspace/`, paste one prompt, then score from the package root. After the agent writes `agent-workspace/artifacts/agent-report.json`, users can run `carves test collect`, `./score.sh`, or `SCORE.cmd` from the package root without `--workspace` or `--bundle-root`. After scoring, users can run `RESULT.cmd` or `./result.sh` to read the last local score, and `RESET.cmd`, `./reset.sh`, or `carves test reset` before testing another agent in the same folder.

A release Windows playable zip must also include a package-local scorer under `tools/carves/` with `tools/carves/scorer-manifest.json`. That first-run package must not require global `carves`, CARVES source checkout, or `dotnet build`. Source-checkout generated packages that lack the scorer bundle are developer packages until release assembly attaches the scorer.

The Phase 5 Node Windows playable artifact is recorded as a parallel official
local trial entry in [Agent Trial Node Phase 5 release artifact](agent-trial-node-phase-5-release-artifact.md)
and [Agent Trial Windows playable handoff](agent-trial-windows-playable-handoff.md).
The beginner-facing zip path is documented in
[Node Windows Playable Agent Trial Quickstart](agent-trial-node-windows-playable-quickstart.md).
It is not yet the default public local trial entry.

The release-oriented assembly form is `carves-matrix trial package --windows-playable --scorer-root <win-publish-root> --output <package-root> --zip-output <zip>`. The scorer root must contain `carves.exe`; the assembler copies it into `tools/carves/`, writes `scorer-manifest.json`, and emits the playable zip. Prepare that release input with `pwsh ./scripts/matrix/publish-windows-playable-scorer.ps1 -OutputRoot ./artifacts/publish/carves-win-x64 -Configuration Release -BuildLabel <build-label> -Force`; it publishes the Runtime `carves` CLI as a self-contained `win-x64` scorer root that must run without `dotnet run`, a source checkout, or a global `carves`. To publish the scorer root and assemble the zip in one release step, use `pwsh ./scripts/matrix/build-windows-playable-package.ps1 -OutputRoot ./artifacts/release/windows-playable -Configuration Release -BuildLabel <build-label> -Force`. To run the Windows clean `SCORE.cmd` gate, use `pwsh ./scripts/matrix/smoke-windows-score-cmd.ps1 -ZipPath ./artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip`; to pin spaces and non-ASCII path text, use `pwsh ./scripts/matrix/smoke-windows-score-cmd-paths.ps1 -ZipPath ./artifacts/release/windows-playable/carves-agent-trial-pack-win-x64.zip`. The Windows playable SCORE.cmd CI smoke in `.github/workflows/matrix-proof.yml` runs that path smoke on `windows-latest` and uploads `windows-scorecmd-path-smoke-summary.json`, the zip, and local-only score output artifacts for diagnosis; the summary is also written on failure with redacted root paths.

Linux/macOS playable scorer bundles are staged follow-up, not the current Windows V1 gate. The planned POSIX package shape uses `score.sh` with a package-local `tools/carves/carves` entrypoint first; PATH `carves` stays an advanced fallback only until native packages have their own archive, executable-permission, and clean-machine smoke coverage.

Windows users can double-click `Start-CARVES-Agent-Test.cmd` from the repository or release folder. The launcher runs the same `carves test` path and pauses before closing so users can read the score summary, result-card path, or setup diagnostic.

The local score measures reviewability, traceability, explainability, report honesty, constraint adherence, and reproducibility evidence for one local task run. It does not prove certification, leaderboard eligibility, hosted verification, producer identity, OS sandboxing, semantic correctness, or local anti-cheat. A local machine owner can still tamper with the local process.

## Runtime Compatibility

The standalone capability CLIs remain available:

- `carves-guard`
- `carves-handoff`
- `carves-audit`
- `carves-shield`
- `carves-matrix`

The Runtime `carves` tool remains the primary public entry and reference host. Its capability-facing commands delegate to the standalone runners where possible:

- `carves guard ...`
- `carves handoff ...`
- `carves audit summary|timeline|explain|evidence ...`
- `carves shield ...`
- `carves matrix ...`

Runtime internal governance commands stay separate from the public capability story. Users do not need Runtime task/card governance concepts to run the Matrix local workflow self-check.

## Start Here

- [Beginner quickstart](quickstart.en.md)
- [Agent Trial local quickstart](agent-trial-v1-local-quickstart.md)
- [Agent Trial local user smoke](agent-trial-v1-local-user-smoke.md)
- [Node Windows Playable Agent Trial quickstart](agent-trial-node-windows-playable-quickstart.md)
- [Agent Trial Windows playable handoff](agent-trial-windows-playable-handoff.md)
- [Agent Trial Node Phase 5 release artifact](agent-trial-node-phase-5-release-artifact.md)
- [Agent Trial Node Phase 7 public onboarding](agent-trial-node-phase-7-public-onboarding.md)
- [Agent Trial Node Phase 8 small user trial](agent-trial-node-phase-8-small-user-trial.md)
- [Agent Trial Node Phase 9 default entry decision](agent-trial-node-phase-9-default-entry-decision.md)
- [Portable scorer bundle contract](portable-scorer-bundle.md)
- [Known limitations](known-limitations.md)
- [Matrix artifact manifest](matrix-artifact-manifest-v0.md)
- [GitHub Actions proof](github-actions-proof.md)
- [Full-release artifact contract](full-release-artifact-contract.md)
- [Native full-release public contract](native-full-release-public-contract.md)
- [Native full-release feasibility](native-full-release-feasibility.md)
- [AI development safety posture plan](ai-development-safety-posture-plan.md)
- [Agent Trial V1 boundary](agent-trial-v1-boundary.md)
- [Agent Trial V1 object model](agent-trial-v1-object-model.md)
- [Agent Trial V1 registration](agent-trial-v1-registration.md)
- [Agent Trial V1 agent profile](agent-trial-v1-agent-profile.md)
- [Agent Trial V1 official pack](agent-trial-v1-official-pack.md)
- [Agent Trial V1 official tasks](agent-trial-v1-official-tasks.md)
- [Agent Trial V1 prompt versioning](agent-trial-v1-prompt-versioning.md)
- [Agent Trial V1 task contract](agent-trial-v1-task-contract.md)
- [Agent Trial V1 agent report contract](agent-trial-v1-agent-report-contract.md)
- [Agent Trial V1 diff scope summary](agent-trial-v1-diff-scope-summary.md)
- [Agent Trial V1 test evidence](agent-trial-v1-test-evidence.md)
- [Agent Trial V1 local runner](agent-trial-v1-local-runner.md)
- [Agent Trial V1 local quickstart](agent-trial-v1-local-quickstart.md)
- [Agent Trial V1 local collector](agent-trial-v1-local-collector.md)
- [Agent Trial V1 Matrix integration](agent-trial-v1-matrix-integration.md)
- [Agent Trial V1 safety posture](agent-trial-v1-safety-posture.md)
- [Agent Trial V1 result contract](agent-trial-v1-result-contract.md)
- [Agent Trial V1 challenge API](agent-trial-v1-challenge-api.md)
- [Agent Trial V1 submit API](agent-trial-v1-submit-api.md)
- [Agent Trial V1 receipt chain](agent-trial-v1-receipt-chain.md)
- [Agent Trial V1 prompt version leaderboard](agent-trial-v1-prompt-version-leaderboard.md)
- [Agent Trial V1 agent profile leaderboard](agent-trial-v1-agent-profile-leaderboard.md)
- [Agent Trial V1 task difficulty leaderboard](agent-trial-v1-task-difficulty-leaderboard.md)
- [Agent Trial V1 anti-gaming](agent-trial-v1-anti-gaming.md)
- [Agent Trial V1 privacy and terms gate](agent-trial-v1-privacy-terms-gate.md)
- [Agent Trial V1 launch readiness](agent-trial-v1-launch-readiness.md)
- [Agent Trial V1 local test MVP](agent-trial-v1-local-test-mvp.md)
- [Agent Trial V1 card plan](agent-trial-v1-card-plan.md)
- [Agent Trial local playtest loop plan](agent-trial-local-playtest-loop-plan.md)
- [Agent Trial local playtest readiness audit](agent-trial-local-playtest-readiness-audit.md)
- [Agent Trial local user smoke](agent-trial-v1-local-user-smoke.md)
- [Agent Trial starter packs](starter-packs/README.md)
- [Continuous challenge notes](continuous-challenge.md)

## Commands

```powershell
carves test demo
carves test agent
carves test package --output ./carves-agent-trial-pack
carves-matrix trial package --windows-playable --scorer-root <win-publish-root> --output ./carves-agent-trial-pack-win-x64 --zip-output ./carves-agent-trial-pack-win-x64.zip
carves test result
carves test verify
Start-CARVES-Agent-Test.cmd
carves-matrix trial plan
carves-matrix trial prepare
carves-matrix trial local
carves-matrix trial verify
carves-matrix trial record
carves-matrix trial compare
carves-matrix proof --lane native-minimal --json
carves-matrix verify <artifact-root> --json
carves-matrix trial demo
carves-matrix trial play
carves-matrix trial package --output ./carves-agent-trial-pack
carves-matrix proof --lane full-release
carves-matrix proof --lane native-full-release --json
carves-matrix e2e
carves-matrix packaged
```

Use `proof --lane native-minimal --json` first when you want a minimal Linux-native local consistency proof lane. It creates a bounded temporary external git repository, runs the Guard -> Handoff -> Audit -> Shield chain through the local .NET CLI runners, writes summary-only Matrix artifacts, writes `matrix-artifact-manifest.json`, and verifies the bundle without invoking `pwsh` or `scripts/matrix/*.ps1`:

```powershell
carves-matrix proof --lane native-minimal --artifact-root artifacts/matrix/native-quickstart --configuration Release --json
```

The native proof emits `matrix-native-proof.v0` JSON with stable `reason_codes` for failed setup, chain, artifact materialization, or verification steps. It writes the quickstart bundle under `artifacts/matrix/native-quickstart`. It is intentionally minimal and does not replace the full release proof.

Use `proof --lane full-release` from this source repository to run the combined project-mode and packaged-install PowerShell proof:

```powershell
carves-matrix proof --lane full-release --runtime-root . --artifact-root artifacts/matrix
```

Use `proof --lane native-full-release --json` when you want the explicit native full-release lane. It runs native project-mode and packaged-install producers, writes the same manifest/proof-summary shape as full release, records `proof_capabilities.proof_lane=native_full_release`, and verifies the finished bundle. It is opt-in only; `proof --lane full-release` remains the PowerShell compatibility lane.

Compatibility shorthand remains available for existing automation: `proof --json` still selects the native minimal lane when `--lane` is omitted, and `proof` without `--json` still selects the full release lane.

Use `verify` first when you already have a summary-only proof bundle. It is the Linux-native public artifact recheck path: it runs inside the .NET Matrix verifier, reads existing local files, and does not invoke `pwsh`, `scripts/matrix/*.ps1`, Guard, Handoff, Audit, Shield, or the Matrix proof lane:

```powershell
carves-matrix verify artifacts/matrix/native-quickstart --json
```

`verify` emits `matrix-verify.v0` JSON. It checks `matrix-artifact-manifest.json`, required artifact entries, artifact hashes and sizes, required artifact schema/producer/path metadata, manifest privacy flags, `matrix-proof-summary.json` consistency with the manifest hash, verification posture, proof mode, proof capabilities, portable artifact root marker, privacy posture, public non-claims, native proof readback fields, full release project/packaged readback fields, and Shield local self-check score readback. The Shield score gate also binds `project/shield-evaluate.json.consumed_evidence_sha256` to the SHA-256 of the included `project/shield-evidence.json` artifact, so a score generated from different evidence does not verify.

`matrix-proof-summary.json` uses the closed public contract documented by `docs/matrix/schemas/matrix-proof-summary.v0.schema.json`. Unknown public fields fail verification with `summary_unknown_field:<json_path>` rather than being silently ignored. Both `proof_mode=native_minimal` and `proof_mode=full_release` compare public `proof_capabilities` and `trust_chain_hardening` fields with verifier-computed expectations. Native minimal records `execution_backend=dotnet_runner_chain`, `packaged_install=false`, and `powershell=false`; PowerShell full release records `execution_backend=powershell_release_units`, `packaged_install=true`, and `powershell=true`; native full release records `execution_backend=dotnet_full_release_runner_chain`, `packaged_install=true`, and `powershell=false`. The verifier computes `trust_chain_hardening.gates_satisfied` from manifest integrity, required artifact metadata, Agent Trial artifact posture, Shield score posture, and summary consistency; the e2e script does not hard-code that final gate posture.

Matrix uses manifest-bound verified reads before trusting public semantic fields. For `proof_mode=native_minimal`, native summary fields are read from the manifest-covered `project/matrix-summary.json` byte snapshot. For `proof_mode=full_release`, summary `project` fields are compared with manifest-covered `project/matrix-summary.json`, and summary `packaged` fields are compared with manifest-covered `packaged/matrix-packaged-summary.json`. Shield evaluation semantics are read through the same manifest-bound verified read path before score fields are trusted. Each semantic read compares byte length and SHA-256 with the manifest entry, rejects duplicate or missing manifest entries, rejects symlink/reparse artifacts, and parses the same verified byte snapshot as JSON.

For public first-run use, `carves test` is the short Runtime-owned alias. It is only a wrapper over the Matrix Agent Trial implementation; Runtime does not own the collector, scorer, verifier, manifest, result card, or history logic:

```powershell
carves test demo
carves test agent
carves test result
carves test verify
```

This tests local agent execution evidence posture. It is not a generic project unit-test runner, certification, benchmark, hosted verification, server receipt, or leaderboard submission.

On Windows, double-click `Start-CARVES-Agent-Test.cmd` from the repository or release folder if you want a menu that runs the same `carves test` path and pauses before closing. Command-line users can use `carves test demo` directly.

Use `trial demo` when you want the Matrix-level local Agent Trial smoke. It creates a local run under `./carves-trials/`, prepares the official starter pack, creates a git baseline, applies the built-in successful demo edit/report, collects evidence, runs strict Matrix trial verification, records local history, and prints the local score and result-card path:

```powershell
carves-matrix trial demo
```

Use `trial play` when you want to test your own agent. It creates the same local run shape and prints the exact agent instruction. In interactive mode it waits for you to press Enter after the agent writes `artifacts/agent-report.json`; in JSON or no-wait mode it prepares the workspace and exits with `ready_for_agent`:

```powershell
carves-matrix trial play
```

Both commands are local-only. They do not submit data, create a server receipt, enter a leaderboard, certify an agent, prove producer identity, or provide OS sandboxing.

Local trial runs use a stable UX layout:

```text
carves-trials/
  latest.json
  run-<timestamp-or-id>/
    workspace/
    bundle/
    history/
```

`latest.json` is a convenience pointer only. It records the latest run id, workspace, bundle, result-card path, history entry, status, and timestamps so users can recheck the last result without remembering paths:

```powershell
carves-matrix trial latest
carves-matrix trial verify
```

`trial verify` without `--bundle-root` reads `latest.json` only to find the latest bundle, then runs the same strict Matrix verifier against that bundle. The latest pointer is non-authoritative, not manifest-covered, not a receipt, not leaderboard evidence, and not certification.

Exit codes:

- `0`: bundle verified.
- `1`: verification failed.
- `2`: usage or argument error.

Failure taxonomy:

`matrix-verify.v0` exposes stable `reason_codes` at the top level, `reason_code` on every issue, and gate-level `reason_codes` under `trust_chain_hardening.gates[]`. The detailed `code` values remain precise verifier diagnostics, while `reason_code` is the stable automation-facing category.

| Reason code | Meaning | Representative detailed codes |
| --- | --- | --- |
| `missing_artifact` | The manifest, summary, required artifact entry, source semantic artifact, or referenced artifact file is absent. | `manifest_missing`, `summary_missing`, `required_artifact_entry_missing`, `artifact_missing`, `summary_source_manifest_entry_missing:<kind>`, `shield_evaluation_source_manifest_entry_missing:<kind>` |
| `hash_mismatch` | A hash or size no longer matches the manifest or proof-summary reference. | `artifact_hash_mismatch`, `artifact_size_mismatch`, `summary_artifact_manifest_sha256_mismatch`, `summary_source_manifest_hash_mismatch:<kind>`, `summary_source_manifest_size_mismatch:<kind>`, `shield_evaluation_source_manifest_hash_mismatch:<kind>`, `shield_evaluation_source_manifest_size_mismatch:<kind>` |
| `schema_mismatch` | Manifest structure, required metadata, closed summary fields, trust-chain hardening projection, source semantic read shape, or readable JSON shape does not match the expected contract. | `required_artifact_schema_mismatch`, `summary_artifact_manifest_issue_count_mismatch`, `artifact_manifest_entry_incomplete`, `summary_unknown_field:<json_path>`, `summary_trust_chain_hardening_gates_satisfied_mismatch`, `summary_source_manifest_entry_duplicate:<kind>`, `summary_source_reparse_point_rejected:<kind>`, `shield_evaluation_source_manifest_entry_duplicate:<kind>`, `shield_evaluation_source_reparse_point_rejected:<kind>` |
| `trial_version_mismatch` | Agent Trial result comparability fields do not match the result's prompt, pack, task, scoring, collector, or verifier version posture. | `trial_result_version_field_mismatch:<field>` |
| `trial_score_mismatch` | Agent Trial local score fields contradict the result's profile, collection status, missing-evidence posture, or required non-claims. | `trial_result_local_score_field_mismatch:<field>`, `trial_result_local_score_status_mismatch`, `trial_result_local_score_aggregate_not_suppressed` |
| `privacy_violation` | Bundle or artifact privacy flags are missing, not summary-only, or include a forbidden payload/public-claim flag. | `privacy_flags_missing`, `privacy_summary_only_not_true`, `privacy_forbidden_flag_true:<flag>` |
| `unverified_score` | The Shield evaluation artifact is present but does not expose an `ok` local self-check score readback with certification false, Standard label, Lite score/band fields, or a consumed-evidence hash matching the included Audit evidence artifact. | `shield_score_unverified`, `shield_evidence_hash_missing`, `shield_evidence_hash_mismatch` |
| `unsupported_version` | The manifest version is not supported by the current verifier. | `unsupported_schema` |

The existing script entry remains available as the full release proof lane for project-mode plus packaged-install evidence:

```powershell
pwsh ./scripts/matrix/matrix-proof-lane.ps1 -ArtifactRoot artifacts/matrix
```

For this migration slice, `matrix-proof-lane.ps1` is a thin wrapper over `CARVES.Matrix.Cli`. The deeper `matrix-e2e-smoke.ps1` and `matrix-packaged-install-smoke.ps1` scripts remain executable proof units that the Matrix shell invokes; they are not product evaluation engines and must not grow Guard, Handoff, Audit, or Shield business logic.

The PowerShell release lanes use the shared checked process helper at `scripts/matrix/matrix-checked-process.ps1`. `Invoke-MatrixCheckedProcess` drains stdout and stderr concurrently, applies bounded timeout handling, caps captured output, and attempts process-tree cleanup on timeout. This hardens the release lane process capture without making PowerShell a requirement for the Linux-native first-run proof or public verify path.

The native full-release lane is scoped in `docs/matrix/native-full-release-feasibility.md`, with public proof-summary values fixed in `docs/matrix/native-full-release-public-contract.md`. The current default full release proof still uses the PowerShell project-mode and packaged-install proof units.

The broader external pilot catalog is documented in `docs/matrix/external-repo-pilot-set.md` and materialized as `docs/matrix/examples/matrix-external-repo-pilot-set.v0.example.json`. Validate it with:

```powershell
pwsh ./scripts/matrix/matrix-external-pilot-set.ps1
```

The catalog covers small Node, small .NET, Python package, monorepo-like nested project, and dirty worktree shapes. It records setup summaries, expected Matrix behavior, and known limitations only; pilot artifacts remain summary-only and do not include source code, raw diffs, prompts, model responses, secrets, credentials, hosted uploads, leaderboard claims, or certification claims.

The cross-platform pilot verification lane is documented in `docs/matrix/cross-platform-verify-pilot.md` and runs with:

```powershell
pwsh ./scripts/matrix/matrix-cross-platform-verify-pilot.ps1 -ArtifactRoot artifacts/matrix/external-pilot-verify
```

It runs `carves-matrix verify` against summary-only pilot bundles, records `matrix-cross-platform-verify-pilot-checkpoint.json`, redacts runtime and artifact roots in public checkpoint metadata, and includes a failure probe that must emit the explicit `hash_mismatch` verification reason code.

Large log and larger patch metadata stress limits are documented in `docs/matrix/large-log-stress.md`. Audit keeps a bounded Guard JSONL tail window of 1000 lines, skips oversized Guard decision records above 131072 bytes with explicit diagnostics, and Matrix manifest verification reports large artifact hash or size changes with `hash_mismatch` reason codes.

## Outputs

The proof lane writes:

- `project-matrix-output.json`
- `packaged-matrix-output.json`
- `matrix-proof-summary.json`
- `matrix-artifact-manifest.json`

The nested project and packaged lanes produce Guard decisions, Handoff packets, Audit `shield-evidence.v0`, Shield evaluation JSON, and Shield badge artifacts. Matrix records the Shield evidence, evaluation, badge JSON, and badge SVG artifact names it consumed, but it does not alter Shield scoring.

`matrix-artifact-manifest.json` uses `matrix-artifact-manifest.v0` to record each summary artifact path, SHA-256 digest, file size, producer, creation timestamp, schema version, and privacy flags. The manifest privacy gate requires summary-only artifacts and marks verification as `privacy_gate_failed` if source, raw diff, prompt, model response, secret, credential, private payload, customer payload, hosted upload, certification, or public leaderboard flags are present as true. `matrix-proof-summary.json` references the manifest path, manifest SHA-256 digest, and verification posture; `verify` also checks proof summary proof mode, proof capabilities, portable artifact root marker, privacy posture, public non-claims, native readback fields, full release project/packaged/trust-chain readback fields, and the closed public field contract instead of making the manifest hash circular. In `full_release` mode, `packaged/matrix-packaged-summary.json` must be present in the manifest even though it remains optional for native/minimal bundles. See `docs/matrix/matrix-artifact-manifest-v0.md`.

Public proof metadata redacts local absolute roots by default. Manifest and summary files use portable or bundle-relative markers such as `.` and `<redacted-local-artifact-root>` rather than usernames, home directories, workspace mounts, or CI runner paths. Full release project and packaged script summaries also redact target repository, package root, tool root, and installed command paths before writing public JSON. Cross-platform pilot verification checkpoints and pilot bundles use redacted runtime roots and bundle-relative artifact roots. The verifier still reads from the artifact root supplied on the command line; redacted metadata is never used as a trusted filesystem root.

## Public Examples

The files under `docs/matrix/examples/` with `.schema-example.json` names are schema examples, not runnable verification bundles. They exist so readers can inspect and validate the public JSON shapes:

- `docs/matrix/examples/matrix-artifact-manifest.v0.schema-example.json`
- `docs/matrix/examples/matrix-proof-summary.v0.schema-example.json`

Do not pass these schema examples directly to `carves-matrix verify`; their hashes and artifact references are illustrative placeholders. Runnable example bundles, when present, use a `.runnable-bundle` directory name and must include the manifest plus every referenced artifact file needed by `carves-matrix verify`.

Shield Lite local challenge suites use `shield-lite-challenge.v0`. Matrix may record challenge suite or challenge result artifacts as local proof-lane evidence, including the starter pack at `docs/shield/examples/shield-lite-starter-challenge-pack.example.json`, but those artifacts remain local challenge results. Matrix does not create challenge truth, does not alter Shield scoring, and does not turn challenge results into certification. Any Matrix summary that references the starter pack must preserve the Shield label `local challenge result, not certified safe`.

The current proof summary also records trust-chain hardening categories:

- Audit evidence integrity, timeline, summary, and explainability checks.
- Guard deletion/replacement honesty plus decision-store append and retention durability.
- Shield evidence contract alignment, Lite/Standard self-check boundary, and badge path consistency.
- Handoff terminal-state semantics, reference freshness, and portability checks.
- Matrix-to-Shield provenance linkage, large-log boundaries, output-path boundaries, and release checkpoint coverage.
- Guard, Audit, and Shield usability plus coverage cleanup for the local proof lane.

With those categories complete, verifier-computed `trust_chain_hardening.gates_satisfied=true` means only that the local proof bundle passed its current manifest, required artifact, Agent Trial artifact, Shield score, and summary-consistency gates. For non-trial bundles, the Agent Trial gate is explicitly satisfied as non-applicable compatibility readback; for explicit trial verification or manifest-claimed trial artifacts, trial artifact failures make the gate and overall gate posture fail. The public self-check posture remains local workflow governance evidence rather than certification: Shield G/H/A and Lite outputs are backed by conservative evidence semantics, but they still do not establish producer identity, signatures, a transparency log, model safety benchmarking, hosted verification, public ranking, semantic correctness, automatic rollback, or operating-system sandboxing. Internal checkpoint documents retain exact CARD traceability for operator review.

## Verification Tests

Direct Matrix Core verifier coverage lives in `tests/Carves.Matrix.Tests/`. Those tests call `MatrixCliRunner` and `MatrixArtifactManifestWriter` in-process, without invoking `pwsh`, Matrix proof scripts, or release lanes. They cover missing required artifacts, hash mismatch, size mismatch, schema mismatch, privacy flag violations, unverified Shield score posture, manifest-bound summary and Shield evaluation semantic reads, closed proof-summary public fields, and stable automation-facing reason codes. Runtime integration tests still cover shell composition and release-readiness behavior.

## Boundary

Matrix is summary-only and local-first. It must not upload source code, raw diffs, prompts, model responses, secrets, credentials, or private file payloads.

Matrix does not claim producer identity, signatures, transparency-log backing, model safety benchmarking, hosted verification, public ranking, certification, semantic correctness proof, automatic rollback, or operating-system sandboxing.
