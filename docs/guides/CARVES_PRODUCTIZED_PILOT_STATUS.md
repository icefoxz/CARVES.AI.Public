# CARVES Productized Pilot Status

This guide records the Phase 10 productized pilot status entry, the Phase 11B target agent bootstrap-pack closure, Phase 12 existing-target bootstrap repair, Phase 13 target commit hygiene, Phase 14 target commit plan, Phase 15 target commit closure, Phase 16 local dist handoff, Phase 17 product pilot proof, Phase 18 external consumer resource pack, Phase 19 CLI invocation contract, Phase 20 CLI activation plan, Phase 21 target dist binding plan, Phase 22 local dist freshness smoke, Phase 23 frozen dist target readback proof, Phase 26 real external repo pilot, Phase 27 external target residue policy, Phase 28 target ignore decision plan, Phase 29 target ignore decision record, Phase 30 target ignore decision record audit, Phase 31 target ignore decision record commit readback, Phase 32 alpha external-use readiness rollup, Phase 33 external target pilot start bundle, Phase 34 agent problem intake, Phase 35 agent problem triage ledger, Phase 36 agent problem follow-up candidates, Phase 37 agent problem follow-up decision plan, Phase 38 durable follow-up decision records, Phase 39 follow-up planning intake, and Phase 40 follow-up planning gate.

Use it before asking an external agent to plan or edit a target repo:

```powershell
carves pilot status
carves pilot status --json
carves pilot preflight
carves pilot preflight --json
carves pilot start
carves pilot start --json
carves pilot problem-intake
carves pilot problem-intake --json
carves pilot triage
carves pilot triage --json
carves pilot problem-triage --json
carves pilot follow-up
carves pilot follow-up --json
carves pilot problem-follow-up --json
carves pilot triage-follow-up --json
carves pilot follow-up-plan
carves pilot follow-up-plan --json
carves pilot problem-follow-up-plan --json
carves pilot triage-follow-up-plan --json
carves pilot follow-up-record
carves pilot follow-up-record --json
carves pilot follow-up-intake
carves pilot follow-up-intake --json
carves pilot follow-up-gate
carves pilot follow-up-gate --json
carves pilot record-follow-up-decision <decision> --all --reason <text>
carves pilot report-problem <json-path>
carves pilot report-problem <json-path> --json
carves pilot list-problems
carves pilot inspect-problem <problem-id>
carves pilot next
carves pilot next --json
carves pilot readiness
carves pilot readiness --json
carves pilot alpha
carves pilot alpha --json
carves pilot invocation
carves pilot invocation --json
carves pilot activation
carves pilot activation --json
carves pilot alias
carves pilot alias --json
carves pilot dist-smoke
carves pilot dist-smoke --json
carves pilot dist-freshness
carves pilot dist-freshness --json
carves pilot dist-binding
carves pilot dist-binding --json
carves pilot bind-dist
carves pilot bind-dist --json
carves pilot target-proof
carves pilot target-proof --json
carves pilot external-proof
carves pilot external-proof --json
carves pilot resources
carves pilot resources --json
carves pilot commit-hygiene
carves pilot commit-hygiene --json
carves pilot commit-plan
carves pilot commit-plan --json
carves pilot closure
carves pilot closure --json
carves pilot residue
carves pilot residue --json
carves pilot ignore-plan
carves pilot ignore-plan --json
carves pilot ignore-record
carves pilot ignore-record --json
carves pilot record-ignore-decision keep_local --all --reason "operator accepted local CARVES residue"
carves pilot dist
carves pilot dist --json
carves pilot proof
carves pilot proof --json
```

Equivalent inspect/API surfaces:

```powershell
carves inspect runtime-product-closure-pilot-status
carves api runtime-product-closure-pilot-status
carves inspect runtime-external-target-pilot-start
carves api runtime-external-target-pilot-start
carves inspect runtime-external-target-pilot-next
carves api runtime-external-target-pilot-next
carves inspect runtime-agent-problem-intake
carves api runtime-agent-problem-intake
carves inspect runtime-agent-problem-triage-ledger
carves api runtime-agent-problem-triage-ledger
carves inspect runtime-agent-problem-follow-up-candidates
carves api runtime-agent-problem-follow-up-candidates
carves inspect runtime-agent-problem-follow-up-decision-plan
carves api runtime-agent-problem-follow-up-decision-plan
carves inspect runtime-agent-problem-follow-up-decision-record
carves api runtime-agent-problem-follow-up-decision-record
carves inspect runtime-agent-problem-follow-up-planning-intake
carves api runtime-agent-problem-follow-up-planning-intake
carves inspect runtime-agent-problem-follow-up-planning-gate
carves api runtime-agent-problem-follow-up-planning-gate
carves inspect runtime-alpha-external-use-readiness
carves api runtime-alpha-external-use-readiness
carves inspect runtime-cli-invocation-contract
carves api runtime-cli-invocation-contract
carves inspect runtime-cli-activation-plan
carves api runtime-cli-activation-plan
carves inspect runtime-local-dist-freshness-smoke
carves api runtime-local-dist-freshness-smoke
carves inspect runtime-target-dist-binding-plan
carves api runtime-target-dist-binding-plan
carves inspect runtime-frozen-dist-target-readback-proof
carves api runtime-frozen-dist-target-readback-proof
carves inspect runtime-external-consumer-resource-pack
carves api runtime-external-consumer-resource-pack
carves inspect runtime-target-commit-hygiene
carves api runtime-target-commit-hygiene
carves inspect runtime-target-commit-plan
carves api runtime-target-commit-plan
carves inspect runtime-target-commit-closure
carves api runtime-target-commit-closure
carves inspect runtime-target-residue-policy
carves api runtime-target-residue-policy
carves inspect runtime-target-ignore-decision-plan
carves api runtime-target-ignore-decision-plan
carves inspect runtime-target-ignore-decision-record
carves api runtime-target-ignore-decision-record
carves inspect runtime-local-dist-handoff
carves api runtime-local-dist-handoff
carves inspect runtime-product-pilot-proof
carves api runtime-product-pilot-proof
```

## What It Answers

The status command answers:

- is the target repo initialized with CARVES Runtime?
- what product closure phase is current?
- which pilot stage is current?
- what is the next governed command?
- are there visible gaps?
- is a managed workspace active?
- is a task waiting for review/writeback?
- is writeback closed and commit plan now the next operator action?
- has the target repo reached clean post-commit closure?
- if only excluded local/tooling residue remains, can product proof still remain complete?
- if ignored residue is desirable, what operator-reviewed `.gitignore` decision plan is available?
- has the operator decision for the current ignore plan been durably recorded, audited, and committed?
- is the local Runtime dist fresh relative to the clean Runtime source HEAD?
- is the target repo consuming CARVES from a frozen local dist root?
- is the target repo initialized, bootstrapped, and bound to the fresh frozen Runtime dist?
- are frozen dist target readback proof, local dist handoff, and target commit closure all satisfied as one final pilot proof?
- if product pilot proof is already complete, is the next step a new operator-scoped intent rather than another proof rerun?
- can the agent distinguish source-tree, local-dist, cmd shim, and future global alias invocation?
- can the operator see safe activation choices without letting the agent mutate PATH or profiles?
- can the operator see the frozen local Runtime dist binding path without letting the agent edit attach manifests?
- can the external consumer resource pack be read without treating a specific target repo as Runtime closure truth?
- is the Runtime alpha external-use readiness rollup ready before an external agent starts target work?
- can repeated or blocking problem reports be surfaced as operator-review follow-up candidates?
- can follow-up candidates be projected into explicit accept/reject/wait decision choices?
- have operator follow-up decisions been durably recorded and audited?
- can accepted, clean follow-up decisions enter formal planning intake?
- does the follow-up planning gate require intent draft, allow plan init, or block on the single active planning slot?

## Typical Use

For an external agent:

```text
Run `carves agent start --json` first.
Read `thread_start_ready`, `current_stage_id`, `next_governed_command`, and `gaps`.
Only run `next_governed_command`.
Use the detailed pilot readbacks only when the start payload reports a gap or a blocker.
Do not edit target files unless the current stage has issued a managed workspace lease.
```

For a human operator:

```powershell
carves pilot status
carves pilot guide
```

`pilot status` and `pilot preflight` tell you where you are. `pilot guide` shows the whole route.

## Read-Only Boundary

`carves pilot status` and `carves pilot preflight` do not mutate truth.

It does not:

- initialize Runtime
- create intent/card/task truth
- issue or submit workspaces
- approve review
- write back files
- commit git changes

If the next action is a mutation, the status output names the command that owns that mutation.

## Commit Plan

When the current stage is `target_commit_plan`, run:

```powershell
carves pilot commit-plan
carves pilot commit-plan --json
```

Stage only paths listed under `stage_paths`:

- `official_target_truth`
- `target_output_candidate` that matches approved Runtime writeback or explicit operator-approved target output

Do not commit local live state by default:

```text
.ai/runtime/live-state/
```

If the surface reports `operator_review_required_paths`, review those paths before any `git add` or commit.

Use `carves pilot commit-hygiene --json` when you need the lower-level classification details behind the commit plan.

## Commit Closure

After committing the planned paths, run:

```powershell
carves pilot closure --json
carves pilot status --json
```

Product commit closure is complete when:

- `commit_closure_complete=true`
- `pilot status` reports `current_stage_id=target_commit_closure`

`target_git_worktree_clean=true` is stronger evidence. If it is false while `commit_closure_complete=true`, only excluded local/tooling residue remains outside the product commit.

After commit closure is complete, `pilot status` verifies target residue, ignore-decision plan, and ignore-decision record readbacks, then advances to `current_stage_id=local_dist_freshness_smoke` when the local Runtime dist freshness smoke is not yet ready.

## Target Residue Policy

When commit closure is complete but local/tooling residue remains, run:

```powershell
carves pilot residue --json
```

Residue policy is complete when:

- `residue_policy_ready=true`
- `product_proof_can_remain_complete=true`
- `stage_path_count=0`
- `operator_review_required_path_count=0`

The surface may suggest `.gitignore` entries such as `.ai/runtime/attach.lock.json` or `.carves-platform/`. These are review candidates only. The command does not write `.gitignore`, clean files, stage paths, or commit target changes.

## Target Ignore Decision Plan

Before editing `.gitignore` for CARVES residue, run:

```powershell
carves pilot ignore-plan --json
```

The ignore decision plan is complete when:

- `ignore_decision_plan_ready=true`
- `product_proof_can_remain_complete=true`
- candidates list operator-approved options rather than commands to execute automatically

Valid decisions are:

- `keep_local`
- `add_to_gitignore_after_review`
- `manual_cleanup_after_review`

If `.gitignore` is edited, rerun the normal commit plan and closure path before claiming final proof.

## Target Ignore Decision Record

When `ignore_decision_required=true`, run:

```powershell
carves pilot ignore-record --json
```

If the record surface reports missing decisions, record the operator choice explicitly:

```powershell
carves pilot record-ignore-decision keep_local --all --reason "operator accepted local CARVES residue"
```

The decision record is target truth under `.ai/runtime/target-ignore-decisions/`. After it is written, rerun commit-plan and commit closure before final proof.

Phase 30 makes this same readback audit the record set. It reports `record_audit_ready`, malformed record count, invalid record count, and conflicting decision entry count. Malformed, invalid, stale, or conflicting records do not satisfy current product proof.

Phase 31 also reports `decision_record_commit_ready`, dirty decision record count, untracked decision record count, and uncommitted decision record count. A newly written record does not satisfy final proof until its path is tracked and clean in the target git work tree.

Phase 32 adds `carves pilot readiness --json` and `carves pilot alpha --json` as read-only Runtime readiness rollups. The rollup answers whether the frozen local Runtime dist and Runtime-owned guidance are ready for bounded external-project alpha use; it does not replace target-side pilot proof.

## Local Dist Freshness Smoke

Before `pilot dist-binding`, run:

```powershell
carves pilot dist-smoke --json
```

Local dist freshness is ready when:

- `overall_posture=local_dist_freshness_smoke_ready`
- `local_dist_freshness_smoke_ready=true`
- `manifest_source_commit_matches_source_head=true`
- `source_git_worktree_clean=true`

When this stage is required, `pilot status` reports:

- `overall_posture=pilot_status_local_dist_freshness_smoke_required`
- `current_stage_id=local_dist_freshness_smoke`
- `next_command=carves pilot dist-smoke --json`

After this smoke is ready, `pilot status` can advance to `current_stage_id=target_dist_binding_plan` when the target is not yet bound to a frozen local Runtime dist.

## Target Dist Binding Plan

Before `pilot dist`, run:

```powershell
carves pilot dist-binding --json
```

Target dist binding plan readiness is complete when:

- `overall_posture` is not `target_dist_binding_plan_blocked_by_missing_dist_resources`
- `dist_binding_plan_complete=true`
- `operator_binding_commands` lists the dist wrapper commands to use from the target repo

The surface is read-only. Agents must not patch `.ai/runtime.json` or `.ai/runtime/attach-handshake.json` manually to retarget Runtime.

## Local Dist Handoff

For stable external-project work, target repos should point at a frozen local Runtime dist rather than the live Runtime source tree.

Run:

```powershell
carves pilot dist --json
```

Stable external consumption is ready when:

- `overall_posture=local_dist_handoff_ready`
- `runtime_root_kind=local_dist`
- `stable_external_consumption_ready=true`
- `runtime_root_has_manifest=true`
- `runtime_root_has_version=true`
- `runtime_root_has_wrapper=true`

If the surface reports `local_dist_handoff_live_source_attached`, the target is usable for development dogfood but should be retargeted through the operator-owned command shown by `carves pilot dist-binding --json` before treating the setup as stable external consumption.

## Frozen Dist Target Readback Proof

After local dist handoff is ready, run:

```powershell
carves pilot target-proof --json
```

Target readback proof is complete when:

- `overall_posture=frozen_dist_target_readback_proof_complete`
- `frozen_dist_target_readback_proof_complete=true`
- `target_agent_bootstrap_ready=true`
- `target_bound_to_local_dist=true`
- `stable_external_consumption_ready=true`

When this stage is required, `pilot status` reports:

- `overall_posture=pilot_status_frozen_dist_target_readback_proof_required`
- `current_stage_id=frozen_dist_target_readback_proof`
- `next_command=carves pilot target-proof --json`

This proof is narrower than product pilot proof. It proves stable external Runtime consumption only.

## Product Pilot Proof

After the target is committed, local dist handoff is ready, and frozen dist target readback proof is complete, run:

```powershell
carves pilot proof --json
```

Product pilot proof is complete when:

- `overall_posture=product_pilot_proof_complete` or `product_pilot_proof_complete_with_local_residue`
- `product_pilot_proof_complete=true`
- `frozen_dist_target_readback_proof_complete=true`
- `stable_external_consumption_ready=true`
- `target_commit_closure_complete=true`

If `product_pilot_proof_complete=false`, follow `recommended_next_action` before declaring the external-project pilot closed.

Before formal planning exists, `pilot status` may report:

- `overall_posture=pilot_status_intent_capture_required`
- `current_stage_id=intent_capture`
- `next_command=carves discuss context`

This is the discussion-first intake posture. The target is attached, but the project purpose or requested outcome is still not explicit enough for planning. Stay in ordinary discussion, clarify what the project is for and whether engineering work is actually requested, then run `carves intent draft` only after the operator/user provides a bounded scope.

After product pilot proof is complete, `pilot status` advances to:

- `overall_posture=pilot_status_product_pilot_proof_complete`
- `current_stage_id=ready_for_new_intent`
- `next_command=carves discuss context`

This is a post-proof handoff posture. It keeps the agent in ordinary discussion first so it can ask for the next project purpose, constraints, and desired outcome. Only after the operator/user provides a bounded scope should the agent move into governed intent capture with `carves intent draft`. It does not authorize the agent to invent new work or reopen proof.

## External Consumer Resource Pack

Before a generic external agent starts planning or editing in a new thread, run:

```powershell
carves agent start --json
```

If the start payload asks for resource-pack details, run:

```powershell
carves pilot invocation --json
carves pilot activation --json
carves pilot resources --json
```

Resource-pack readiness is complete when:

- `overall_posture=external_consumer_resource_pack_ready`
- `resource_pack_complete=true`

This readback lists Runtime-owned docs, target-generated bootstrap paths, and command entries. It also records that Runtime docs stay under the Runtime document root and should not be copied into target product truth.

## CLI Invocation Contract

Before a generic external agent relies on `carves`, run:

```powershell
carves pilot invocation --json
```

Invocation readiness is complete when:

- `overall_posture=cli_invocation_contract_ready`
- `invocation_contract_complete=true`

This readback separates source-tree wrapper, local-dist wrapper, Windows cmd shim, and future global alias usage. It is read-only and does not grant mutation authority.

## CLI Activation Plan

Before relying on a short `carves` command, run:

```powershell
carves pilot activation --json
```

Activation readiness is complete when:

- `overall_posture=cli_activation_plan_ready` or `overall_posture=cli_activation_plan_ready_with_detected_operator_activation`
- `activation_plan_complete=true`

This readback lists absolute wrapper, session alias, PATH entry, cmd shim, and optional tool install lanes. It is read-only and does not mutate shell state.

## Non-Claims

This guide does not claim:

- full first-run wizard automation
- OS/process sandboxing
- full ACP/MCP transport support
- automatic git commit or push
