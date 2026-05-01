# CARVES Runtime Local Distribution

This guide records the local distribution workflow for using CARVES from external target repos without pointing those repos at the active Runtime development workspace.

## Why This Exists

External projects should consume a frozen Runtime distribution folder instead of the live `CARVES.Runtime` source checkout.

This keeps target repos stable while Runtime development continues.

Recommended layout:

```text
<carves-root>
<runtime-dist>
<target-project>
```

The source repo owns development. The dist folder is a generated artifact. Target repos should attach to the dist folder.

Version policy lives in `docs/release/runtime-versioning-policy.md`. A Runtime dist version names the frozen folder that external repos bind to; it does not automatically mean every included tool package has the same version or maturity.

## Create The Dist

From the Runtime source repo:

```powershell
.\scripts\pack-runtime-dist.ps1 -Version 0.6.2-beta
```

If the version folder already exists and you intentionally want to replace it:

```powershell
.\scripts\pack-runtime-dist.ps1 -Version 0.6.2-beta -Force
```

The script requires a clean Runtime source repo by default. Use `-AllowDirty` only for explicit diagnostic builds.

## Output Contract

The generated folder contains:

- `carves`, `carves.ps1`, and `carves.cmd`
- `runtime-cli/` with the published CLI dependency closure
- selected Runtime release docs under `docs/`
- interaction templates under `templates/interaction/`
- `.ai/PROJECT_BOUNDARY.md`
- `VERSION`
- `MANIFEST.json`

The release dist does not carry the Runtime source tree. The wrapper must run `runtime-cli/carves.dll`; if that file is missing, the release wrapper fails clearly instead of silently building from source.

The selected `.ai/` content is intentionally narrow. A release dist carries only the project boundary file from `.ai/`; it is not a copy of the Runtime development control plane.

The generated folder excludes:

- `.git/`
- `bin/` and `obj/`
- `TestResults/`
- `src/`
- `tests/`
- `scripts/`
- `CARVES.Runtime.sln`
- `.carves-platform/`
- runtime live state
- task/artifact/tmp operational residue
- `.ai/tasks/` card and task truth
- `.ai/runtime/` execution and context-pack history
- `.ai/artifacts/` worker, provider, review, and safety artifacts
- `.ai/execution/` execution envelopes
- `.ai/failures/` failure history
- `.ai/memory/execution/` development-time execution memory

Do not package development card history by default. Files under `.ai/tasks/`, `.ai/runtime/`, `.ai/artifacts/`, `.ai/execution/`, `.ai/failures/`, and `.ai/memory/execution/` are source-repo governance history, not external distribution payload. This also prevents very long generated names such as `MEM-T-CARD-...json` from making Windows extraction fail with `Path too long`.

The packaging script fails the release dist if any generated relative path is longer than the configured Windows extraction budget or if the output audit finds forbidden paths. Use `-DistKind dev` only when intentionally producing a development/source package.

## Target Repo Binding

A target repo should point its `.ai/runtime.json` and `.ai/runtime/attach-handshake.json` runtime root to the generated folder:

```json
"runtime_root": "D:\\Projects\\CARVES.AI\\.dist\\CARVES.Runtime-0.6.2-beta"
```

Then run from the target repo:

```powershell
<runtime-dist>\carves.ps1 init . --json
<runtime-dist>\carves.ps1 pilot readiness --json
<runtime-dist>\carves.ps1 pilot invocation --json
<runtime-dist>\carves.ps1 pilot activation --json
<runtime-dist>\carves.ps1 pilot dist-smoke --json
<runtime-dist>\carves.ps1 pilot dist-binding --json
<runtime-dist>\carves.ps1 pilot target-proof --json
<runtime-dist>\carves.ps1 pilot residue --json
<runtime-dist>\carves.ps1 pilot ignore-plan --json
<runtime-dist>\carves.ps1 pilot ignore-record --json
<runtime-dist>\carves.ps1 pilot follow-up-record --json
<runtime-dist>\carves.ps1 pilot follow-up-intake --json
<runtime-dist>\carves.ps1 pilot follow-up-gate --json
<runtime-dist>\carves.ps1 pilot status --json
```

The expected readback is:

- `runtime_initialized=true`
- `runtime_document_root` points at the dist folder
- `runtime_document_root_mode=attach_handshake_runtime_root`
- `product_closure_phase=phase_32_alpha_external_use_readiness_rollup_ready`
- `recommended_invocation_mode=local_dist_wrapper`
- `recommended_activation_lane=path_entry`

The dist wrapper injects its own Runtime root into the CLI process. `init . --json` must bind `.ai/runtime.json` and `.ai/runtime/attach-handshake.json` to the dist folder, not to the target repo that is running the command.

Before claiming the dist is current, verify the freshness smoke:

```powershell
<runtime-dist>\carves.ps1 pilot dist-smoke --json
```

The expected freshness readback is:

- `overall_posture=local_dist_freshness_smoke_ready`
- `local_dist_freshness_smoke_ready=true`
- `manifest_source_commit_matches_source_head=true`
- `source_git_worktree_clean=true`

Then verify the distribution handoff:

```powershell
<runtime-dist>\carves.ps1 pilot dist-smoke --json
<runtime-dist>\carves.ps1 pilot readiness --json
<runtime-dist>\carves.ps1 pilot dist-binding --json
<runtime-dist>\carves.ps1 pilot dist --json
<runtime-dist>\carves.ps1 pilot target-proof --json
```

The expected binding-plan readback is:

- `dist_binding_plan_complete=true`
- `target_bound_to_local_dist=true`
- `recommended_binding_mode=keep_current_local_dist_binding`

The expected dist handoff readback is:

- `overall_posture=local_dist_handoff_ready`
- `runtime_root_kind=local_dist`
- `stable_external_consumption_ready=true`
- `runtime_root_has_manifest=true`
- `runtime_root_has_version=true`
- `runtime_root_has_wrapper=true`

Then verify the external target readback proof:

```powershell
<runtime-dist>\carves.ps1 pilot target-proof --json
```

The expected target proof readback is:

- `overall_posture=frozen_dist_target_readback_proof_complete`
- `frozen_dist_target_readback_proof_complete=true`
- `target_agent_bootstrap_ready=true`
- `target_bound_to_local_dist=true`
- `stable_external_consumption_ready=true`

## Commit Hygiene

Do not commit target repo live state by default:

```text
.ai/runtime/live-state/
```

The target repo should commit only durable CARVES truth and approved project output.

Run `carves pilot commit-plan --json` before staging; it projects `stage_paths`, `excluded_paths`, and `operator_review_required_paths`.

After committing those paths, run `carves pilot closure --json` and confirm `commit_closure_complete=true`. `target_git_worktree_clean=false` can still be acceptable when only excluded local/tooling residue remains.

When local/tooling residue remains, run `carves pilot residue --json` and confirm `product_proof_can_remain_complete=true`. Then run `carves pilot ignore-plan --json` before deciding whether to keep the residue local, clean it manually, or add reviewed `.gitignore` entries. The ignore plan is read-only and does not mutate `.gitignore`. If the plan requires a durable decision, run `carves pilot ignore-record --json` and record the operator choice with `carves pilot record-ignore-decision <decision> --all --reason <reason>`, then commit the record through target commit closure. Final proof requires `decision_record_commit_ready=true`; untracked or dirty decision record paths keep the pilot at the commit-readback stage.

After local dist handoff, target proof, and target commit closure are satisfied, run `carves pilot proof --json` and confirm `product_pilot_proof_complete=true`.

## Non-Claims

This local dist is not a public package registry, NuGet release, full ACP/MCP server, OS sandbox, or remote worker orchestrator. It is a frozen local Runtime artifact for stable external-project consumption.
