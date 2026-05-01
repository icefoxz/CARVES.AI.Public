# CARVES Real Project Pilot

This guide records the Phase 5 real external project pilot path.

Use it after the Phase 4 external-target flow is available and you want to test CARVES against a real target repo instead of a temporary integration sandbox.

## Target

The current pilot target is:

```text
D:\Projects\CARVES.AI\CARVES.AgentCoach
```

This target is a pilot repo. Its first job is to prove CARVES-controlled onboarding and planning, not to start broad product implementation.

## Command Sequence

Start the Runtime host from the Runtime repo:

```powershell
.\carves.ps1 host start --interval-ms 200
```

From the target repo:

```powershell
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 init . --json
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 doctor --json
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 pilot readiness --json
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 pilot invocation --json
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 pilot activation --json
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 pilot dist-smoke --json
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 pilot dist-binding --json
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 pilot target-proof --json
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 pilot residue --json
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 pilot ignore-plan --json
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 pilot ignore-record --json
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 agent handoff --json
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 pilot guide
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 pilot status
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 inspect runtime-first-run-operator-packet
```

Then start guided planning:

```powershell
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 intent draft
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 intent focus candidate-first-slice
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 intent decision first_validation_artifact resolved
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 intent decision first_slice_boundary resolved
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 intent candidate candidate-first-slice ready_to_plan
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 plan init candidate-first-slice
& D:\Projects\CARVES.AI\CARVES.Runtime\carves.ps1 plan status
```

## Expected Readback

Expected target readiness:

- `init` reports `initialized_runtime` or `attached_existing_runtime`
- `doctor` reports `target_repo_readiness=runtime_initialized`
- `doctor` reports `host_readiness=connected` when the resident host is running
- `doctor` reports `is_ready=true`

Expected handoff:

- `product_closure_phase=phase_32_alpha_external_use_readiness_rollup_ready`
- `runtime_document_root_mode=attach_handshake_runtime_root` or `runtime_manifest_root`
- `runtime_document_root` points at the Runtime repo during source dogfood, or at the local dist folder for stable external consumption
- `is_valid=true`

Expected pilot guide:

- `Product closure phase: phase_32_alpha_external_use_readiness_rollup_ready`
- `Overall posture: productized_pilot_guide_ready`
- stage sequence includes `attach_target`, `workspace_submit`, `review_writeback`, `target_commit_plan`, `target_commit_closure`, `target_residue_policy`, `target_ignore_decision_plan`, `target_ignore_decision_record`, `local_dist_freshness_smoke`, `target_dist_binding_plan`, `local_dist_handoff`, `frozen_dist_target_readback_proof`, and `product_pilot_proof`

Expected pilot status:

- `Product closure phase: phase_32_alpha_external_use_readiness_rollup_ready`
- `Current stage` names the target repo's current pilot stage
- `Next command` names a governed command rather than a direct file edit

Expected residue policy:

- `residue_policy_ready=true` after target commit closure is complete and only excluded local/tooling residue remains
- `product_proof_can_remain_complete=true`
- suggested `.gitignore` entries are operator-review candidates, not automatic mutations

Expected ignore decision plan:

- `ignore_decision_plan_ready=true` when residue policy is ready and the target still has reviewed ignore candidates
- `product_proof_can_remain_complete=true`
- `gitignore_patch_preview` is a read-only patch preview, not an automatic `.gitignore` write

Expected ignore decision record audit and commit readback:

- `record_audit_ready=true` after current-plan records are well formed and non-conflicting
- `decision_record_commit_ready=true` after decision record paths are tracked and clean in target git
- malformed, invalid, stale, or conflicting records do not satisfy current product proof
- untracked or dirty decision record paths do not satisfy current product proof

Expected first-run packet:

- `Overall posture: first_run_packet_ready`
- Runtime-owned docs are read from the Runtime document root
- attached-target internal beta readback is summarized, not expanded into Runtime internal evidence noise

Expected planning:

- `planning_posture=ready_to_plan` after required intent decisions are resolved
- `plan init` creates one active planning packet
- `plan status` reports `active_planning_card_fill_state=ready_to_export`

## Pilot Commit Hygiene

In the target repo, commit durable bootstrap/planning truth:

- `.ai/`
- `.carves-platform/policies/`
- `.carves-platform/providers/`

Do not commit live host/runtime-state files:

- `.carves-platform/host/`
- `.carves-platform/runtime-state/`

Those are local runtime state, not durable project truth.

## Operator Rule

After this pilot reaches `ready_to_export`, the next governed action is not broad implementation. The next action is:

```powershell
carves plan export-card <json-path>
```

Then materialize task truth, bind acceptance contracts, choose a working mode, and only then allow task-scoped edits.

The Phase 6 writeback continuation is recorded in:

- `docs/guides/CARVES_REAL_PROJECT_WRITEBACK.md`
- `docs/runtime/carves-product-closure-phase-6-official-truth-writeback.md`
- `docs/guides/CARVES_REAL_PROJECT_WORKSPACE.md`
- `docs/runtime/carves-product-closure-phase-7-managed-workspace-execution.md`

## Non-Claims

This guide does not claim:

- the target product is implemented
- a global installer is complete
- attach metadata is portable across machines
- arbitrary agents are hard-sandboxed
- ACP or MCP transports are complete
