# CARVES Productized Pilot Guide

This guide records the Phase 9 productized external-project pilot route, the Phase 10 status entry that selects the current route stage, the Phase 11B target agent bootstrap pack, Phase 12 existing-target bootstrap repair, Phase 13 target commit hygiene, Phase 14 target commit plan, Phase 15 target commit closure, Phase 16 local dist handoff, Phase 17 product pilot proof, Phase 18 external consumer resource pack, Phase 19 CLI invocation contract, Phase 20 CLI activation plan, Phase 21 target dist binding plan, Phase 22 local dist freshness smoke, Phase 23 frozen dist target readback proof, Phase 26 real external repo pilot, Phase 27 external target residue policy, Phase 28 target ignore decision plan, Phase 29 target ignore decision record, Phase 30 target ignore decision record audit, Phase 31 target ignore decision record commit readback, Phase 32 alpha external-use readiness rollup, Phase 33 external target pilot start bundle, Phase 34 agent problem intake, Phase 35 agent problem triage ledger, Phase 36 agent problem follow-up candidates, Phase 37 agent problem follow-up decision plan, Phase 38 agent problem follow-up decision record, Phase 39 agent problem follow-up planning intake, and Phase 40 agent problem follow-up planning gate.

Use it when an operator or external agent needs a single command that explains how to take a target repo from first CARVES attachment to governed managed-workspace writeback.

## Command

From an attached or attachable target repo:

```powershell
carves pilot status
carves pilot status --json
carves agent start
carves agent start --json
carves agent boot
carves agent boot --json
carves pilot boot
carves pilot boot --json
carves pilot agent-start
carves pilot agent-start --json
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
carves pilot guide
carves pilot guide --json
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
carves inspect runtime-agent-thread-start
carves api runtime-agent-thread-start
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
carves inspect runtime-product-closure-pilot-guide
carves api runtime-product-closure-pilot-guide
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

## What It Does

The command projects a read-only sequence:

0. `carves agent start --json`
1. `carves init [target-path] --json`
2. `carves doctor --json`
3. `carves agent bootstrap --write`
4. `carves pilot invocation --json`
5. `carves pilot activation --json`
6. `carves pilot resources --json`
7. `carves agent handoff --json`
8. `carves discuss context`
9. `carves plan init [candidate-card-id]`
10. card and taskgraph approval commands
11. `carves plan issue-workspace <task-id>`
12. `carves plan submit-workspace <task-id> <reason...>`
13. `carves inspect runtime-workspace-mutation-audit <task-id>`
14. `carves review approve <task-id> <reason...>`
15. closure readbacks
16. `carves pilot commit-plan`
17. `carves pilot closure --json`
18. `carves pilot residue --json`
19. `carves pilot ignore-plan --json`
20. `carves pilot ignore-record --json`
21. `carves pilot dist-smoke --json`
22. `carves pilot dist-binding --json`
23. `carves pilot dist --json`
24. `carves pilot target-proof --json`
25. `carves pilot proof --json`
26. `carves discuss context`

The JSON form is intended for external agents. It exposes the same stage list with `stage_id`, command, authority class, purpose, and exit signal.

If the agent needs to know where the target repo currently sits in the sequence, run:

```powershell
carves pilot status --json
```

## What It Does Not Do

`carves pilot guide` does not mutate project truth.

It does not:

- initialize a repo
- create intent, card, taskgraph, task, review, or workspace records
- approve review
- write back files
- commit or push git changes

Every mutation still goes through the underlying governed command named by the guide.

## Recommended Agent Prompt Rule

When an agent is asked to start using CARVES in a target repo, tell it:

```text
Before planning or editing in a new thread, run `carves agent start --json`.
Treat `next_governed_command` as a legacy projection hint and prefer `available_actions` when present.
Run `carves pilot next --json`, `carves pilot status --json`, and the detailed problem/follow-up readbacks only when the start payload reports a gap or asks for deeper evidence.
Read Runtime-owned docs from the Runtime document root instead of copying them into the target repo.
Do not write target files directly unless a managed workspace lease or explicit governed command authorizes that path.
```

This is still a soft instruction at the prompt layer. The hard enforcement remains at Runtime-controlled planning, workspace, review, protected-root, and writeback gates.

`carves review approve` uses the same managed-workspace path policy surfaced by `carves inspect runtime-workspace-mutation-audit <task-id>`. Paths under `.ai/runtime/` are not blanket-approved or blanket-denied: task-scoped Runtime evidence such as accepted follow-up decision records may be written back only when the active lease explicitly allows the path and the mutation audit reports no `scope_escape`, `host_only`, or `deny` blockers. Host-only truth roots such as `.ai/tasks/`, `.ai/memory/`, `.ai/artifacts/reviews/`, and `.carves-platform/` remain blocked.

## Alpha External-Use Readiness

Before treating the frozen local Runtime dist as ready for external-project alpha use, run:

```powershell
carves pilot readiness --json
```

The rollup is ready when:

- `overall_posture=alpha_external_use_readiness_ready`
- `alpha_external_use_ready=true`
- blocking readiness checks report `ready=true`
- `product_pilot_proof_required_per_target=true`

This is Runtime-owned readiness. It does not replace target-side init, bootstrap, managed work, review, commit closure, residue, ignore decision, dist binding, target proof, or final product proof.

## Commit Plan

After a successful `review approve`, run `carves pilot commit-plan` before staging.

Commit official target truth and approved output files only after the surface lists them under `stage_paths`.

Do not commit local live state by default:

```text
.ai/runtime/live-state/
```

If the review/writeback path removes the managed workspace, that is expected. The accepted files have already been materialized into the target repo.

If the surface reports `operator_review_required_paths`, stop and classify them with the operator before staging.

Use `carves pilot commit-hygiene` only when you need the lower-level path classifications behind the plan.

After the operator-reviewed git commit, run `carves pilot closure --json` and confirm `commit_closure_complete=true`.

If closure is complete but `target_git_worktree_clean=false`, run:

```powershell
carves pilot residue --json
```

The residue policy is ready when:

- `residue_policy_ready=true`
- `product_proof_can_remain_complete=true`
- `stage_path_count=0`
- `operator_review_required_path_count=0`

Suggested `.gitignore` entries are review candidates only. The command does not mutate `.gitignore`, clean files, stage paths, or commit target changes.

## Target Ignore Decision Plan

Before editing `.gitignore` for CARVES residue, run:

```powershell
carves pilot ignore-plan --json
```

The ignore decision plan is ready when:

- `ignore_decision_plan_ready=true`
- `product_proof_can_remain_complete=true`
- each candidate still requires operator review

The plan gives three choices for each candidate:

- `keep_local`
- `add_to_gitignore_after_review`
- `manual_cleanup_after_review`

If the operator chooses to edit `.gitignore`, rerun `carves pilot commit-plan --json`, commit only reviewed target paths, then rerun `carves pilot closure --json`, `carves pilot residue --json`, `carves pilot ignore-plan --json`, and `carves pilot proof --json`.

## Target Ignore Decision Record

When the ignore decision plan reports missing entries, run:

```powershell
carves pilot ignore-record --json
```

If a durable decision is required, the operator records it explicitly:

```powershell
carves pilot record-ignore-decision keep_local --all --reason "operator accepted local CARVES residue"
```

The record is written under `.ai/runtime/target-ignore-decisions/`. It is target truth, so after recording it, rerun `carves pilot commit-plan --json`, commit the record through target git closure, then rerun `carves pilot closure --json`, `carves pilot residue --json`, `carves pilot ignore-plan --json`, `carves pilot ignore-record --json`, and `carves pilot proof --json`.

Phase 30 makes the same `ignore-record` readback audit the record set. Malformed JSON, invalid current-plan records, stale plan records, or conflicting decisions block `record_audit_ready` and therefore block final product proof.

Phase 31 also requires the decision record paths to be tracked and clean in the target git work tree. `decision_record_commit_ready=true`, `record_audit_ready=true`, and `decision_record_ready=true` must all hold before the record can satisfy product proof. If `uncommitted_decision_record_count` is non-zero, rerun `carves pilot commit-plan --json`, commit the listed target truth through the normal target commit closure path, then rerun `carves pilot ignore-record --json`.

Phase 32 adds `carves pilot readiness --json` and `carves pilot alpha --json` as thin read-only Runtime readiness rollups. They should be checked before asking external agents to plan or edit through CARVES, but they do not replace per-target proof.

Phase 33 adds `carves pilot start --json` and `carves pilot next --json` as thin read-only entry helpers for external agents. The current first command is `carves agent start --json`, which aggregates the start bundle, follow-up planning gate, pilot status, and handoff proof into one readback. `pilot start` and `pilot next` remain troubleshooting readbacks. They do not replace `pilot status`, `pilot guide`, target proof, product proof, or operator review. When these helpers are ready, non-blocking alpha-readiness advisory details are not projected as `gaps`; operators can still inspect the full advisory ledger through `carves pilot readiness --json`. Ready helper surfaces may still expose `next_governed_command`, but that field is now treated as a legacy projection hint; `available_actions` is the preferred next-step surface when present.

Phase 34 adds `carves pilot problem-intake --json` and `carves pilot report-problem <json-path> --json`. `problem-intake` shows the payload schema and stop triggers; `report-problem` records a bounded target runtime problem record plus pilot evidence. It does not authorize the blocked change.

Phase 35 adds `carves pilot triage --json`, `carves pilot problem-triage --json`, and `carves inspect runtime-agent-problem-triage-ledger`. The triage ledger groups recorded problem intake into an operator-facing friction queue. It is read-only and does not resolve records, create tasks, or authorize the blocked change.

Phase 36 adds `carves pilot follow-up --json`, `carves pilot problem-follow-up --json`, `carves pilot triage-follow-up --json`, and `carves inspect runtime-agent-problem-follow-up-candidates`. The follow-up candidates surface promotes repeated or blocking problem patterns into operator-review candidates. It is read-only and does not create cards, tasks, approvals, or continuation authority.

Phase 37 adds `carves pilot follow-up-plan --json`, `carves pilot problem-follow-up-plan --json`, `carves pilot triage-follow-up-plan --json`, and `carves inspect runtime-agent-problem-follow-up-decision-plan`. The follow-up decision plan projects accept/reject/wait choices for candidates. It is read-only and does not record durable decisions, create cards, tasks, approvals, or continuation authority.

Phase 38 adds `carves pilot follow-up-record --json`, `carves pilot follow-up-decision-record --json`, `carves pilot problem-follow-up-record --json`, `carves pilot record-follow-up-decision <decision> ...`, and `carves inspect runtime-agent-problem-follow-up-decision-record`. The follow-up decision record stores reviewed accept/reject/wait choices under `.ai/runtime/agent-problem-follow-up-decisions/`, audits malformed/invalid/stale/conflicting records, and requires accepted decisions to carry acceptance evidence plus a readback command. It still does not create cards, tasks, approvals, or continuation authority.

Phase 39 adds `carves pilot follow-up-intake --json`, `carves pilot follow-up-planning --json`, `carves pilot problem-follow-up-intake --json`, and `carves inspect runtime-agent-problem-follow-up-planning-intake`. The follow-up planning intake projects accepted, clean, committed decision records into formal planning inputs only while their candidate ids are still open. A completed or merged task with `metadata.source_candidate_card_id=<candidate>` consumes that candidate so it is not re-projected into `intent draft`. The surface still does not create cards, tasks, approvals, or continuation authority.

Phase 40 adds `carves pilot follow-up-gate --json`, `carves pilot follow-up-planning-gate --json`, `carves pilot problem-follow-up-gate --json`, and `carves inspect runtime-agent-problem-follow-up-planning-gate`. The follow-up planning gate checks accepted planning inputs against intent draft and the single active formal planning slot before `plan init`.

## Local Dist Freshness Smoke

Before claiming the local Runtime dist is current, run:

```powershell
carves pilot dist-smoke --json
```

The local dist freshness smoke is complete when:

- `overall_posture=local_dist_freshness_smoke_ready`
- `local_dist_freshness_smoke_ready=true`
- `manifest_source_commit_matches_source_head=true`
- `source_git_worktree_clean=true`

If the surface reports a stale or missing dist, refresh the local Runtime dist from a clean source tree before target binding.

## Target Dist Binding Plan

Before claiming stable external-project consumption, run:

```powershell
carves pilot dist-binding --json
```

The target dist binding plan is complete when:

- `overall_posture` is not `target_dist_binding_plan_blocked_by_missing_dist_resources`
- `dist_binding_plan_complete=true`
- `operator_binding_commands` names the frozen local dist wrapper path to use

If the surface reports `target_bound_to_live_source_tree`, the target is still attached to the live Runtime source checkout. Follow the operator-owned wrapper command from the surface instead of editing `.ai/runtime.json` or `.ai/runtime/attach-handshake.json` manually.

## Local Dist Handoff

After Runtime source work is committed, the local dist freshness smoke is ready, and the target dist binding plan has been read, run `carves pilot dist --json` from the target repo. Stable external consumption requires `stable_external_consumption_ready=true`.

If `runtime_root_kind=source_tree`, the repo is still attached to the live Runtime checkout. That is development-mode dogfood, not the stable external-project consumption posture.

## Frozen Dist Target Readback Proof

After local dist handoff reports `stable_external_consumption_ready=true`, run:

```powershell
carves pilot target-proof --json
```

The proof is complete when:

- `overall_posture=frozen_dist_target_readback_proof_complete`
- `frozen_dist_target_readback_proof_complete=true`
- `target_agent_bootstrap_ready=true`
- `target_bound_to_local_dist=true`
- `stable_external_consumption_ready=true`

This proves the current target repo can consume the frozen Runtime dist. It does not replace target commit closure or final product pilot proof.

## Product Pilot Proof

After frozen dist target readback proof, local dist handoff, and target commit closure are all satisfied, run `carves pilot proof --json`.

The proof is complete when:

- `overall_posture=product_pilot_proof_complete` or `product_pilot_proof_complete_with_local_residue`
- `product_pilot_proof_complete=true`
- `frozen_dist_target_readback_proof_complete=true`
- `stable_external_consumption_ready=true`
- `target_commit_closure_complete=true`

If the proof reports `product_pilot_proof_waiting_for_target_commit`, follow `carves pilot commit-plan --json`, commit the listed paths, rerun `carves pilot closure --json`, then rerun `carves pilot proof --json`.

If the proof reports `product_pilot_proof_waiting_for_operator_residue_policy`, run `carves pilot residue --json` and resolve its gaps before claiming pilot proof complete.

When formal planning does not exist yet, `pilot status`, `pilot next`, and `agent start` should keep the agent in discussion-first posture with `next_command=carves discuss context`. In both `intent_capture` and `ready_for_new_intent`, ask what the project is for, what outcome is wanted, and whether the user actually wants new engineering work. `agent start` must still use pilot status as the next-command source even if follow-up diagnostics still report accepted planning evidence; follow-up remains inspectable, but it must not steal the discussion-first lane when the scope is still unclear. Only after the operator/user provides a bounded scope should the agent move into `carves intent draft`. This posture does not authorize the agent to invent new product work.

## External Consumer Resource Pack

Before asking a generic external agent to plan or edit, run:

```powershell
carves pilot invocation --json
carves pilot activation --json
carves pilot resources --json
```

The resource pack is ready when:

- `overall_posture=external_consumer_resource_pack_ready`
- `resource_pack_complete=true`

The resource pack lists Runtime-owned docs, target-generated bootstrap projections, command entries, and boundaries. It explicitly keeps Runtime docs in the Runtime document root and prevents any specific target repo from becoming a Runtime closure prerequisite.

## CLI Invocation Contract

Before relying on a global `carves` alias, run:

```powershell
carves pilot invocation --json
```

The contract is ready when:

- `overall_posture=cli_invocation_contract_ready`
- `invocation_contract_complete=true`

The contract separates source-tree wrapper, frozen local-dist wrapper, Windows cmd shim, and future global alias usage. It is read-only and does not change planning, workspace, review, writeback, or commit authority.

## CLI Activation Plan

Before relying on a short `carves` command or asking an agent to use one, run:

```powershell
carves pilot activation --json
```

The activation plan is ready when:

- `overall_posture=cli_activation_plan_ready` or `overall_posture=cli_activation_plan_ready_with_detected_operator_activation`
- `activation_plan_complete=true`

The activation plan lists absolute wrapper, session alias, PATH entry, cmd shim, and optional tool install choices. It is read-only and does not mutate PATH, edit profiles, or install tools.

## Non-Claims

This guide does not claim:

- a complete first-run wizard
- public NuGet/package distribution
- OS/process sandboxing
- full ACP/MCP support
- remote worker orchestration
- automatic git commit or push
