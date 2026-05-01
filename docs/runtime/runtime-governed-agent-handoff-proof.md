# Runtime Governed Agent Handoff Proof

This proof ties the multi-layer agent collaboration plan into one bounded Runtime path.

It proves a practical rail, not broad production sandboxing.

## Validation Path

1. Formal planning entry starts the durable flow.
2. One active planning card owns the current planning slot.
3. Materialized task truth carries an acceptance contract.
4. Runtime recommends the strongest appropriate working mode.
5. Adapter handoff uses CLI-first, ACP-second, or MCP-optional without changing authority.
6. Writable work happens through a task-bound workspace or Mode E brokered execution.
7. Review preflight projects packet, evidence, protected-path, and mutation-audit blockers.
8. Official truth ingress remains planner review and host writeback.

## Constraint Classes

| Class | Enforcement | Applies to |
| --- | --- | --- |
| `soft_advisory` | read-side guidance | bootstrap docs, Mode A, early planning guidance |
| `hard_runtime_gate` | Runtime-enforced before writeback | formal planning slot, acceptance contract, workspace lease, Mode E packet/preflight |
| `vendor_optional` | acceleration, not baseline | Codex/Claude config, IDE permission surfaces, ACP/MCP transports |
| `deferred` | not claimed | OS sandboxing, full ACP server, full MCP server, remote pack orchestration |

## Required Cold Readbacks

- `agent handoff`
- `init --json`
- `doctor --json`
- `agent handoff --json`
- `inspect runtime-agent-working-modes`
- `inspect runtime-adapter-handoff-contract`
- `inspect runtime-protected-truth-root-policy`
- `inspect runtime-workspace-mutation-audit <task-id>`
- `inspect runtime-brokered-execution <task-id>`
- `inspect runtime-governed-agent-handoff-proof`
- `pilot start [--json]`
- `inspect runtime-external-target-pilot-start`
- `pilot next [--json]`
- `inspect runtime-external-target-pilot-next`
- `pilot problem-intake [--json]`
- `inspect runtime-agent-problem-intake`
- `pilot triage [--json]`
- `inspect runtime-agent-problem-triage-ledger`
- `pilot follow-up [--json]`
- `inspect runtime-agent-problem-follow-up-candidates`
- `pilot follow-up-plan [--json]`
- `inspect runtime-agent-problem-follow-up-decision-plan`
- `pilot follow-up-record [--json]`
- `inspect runtime-agent-problem-follow-up-decision-record`
- `pilot follow-up-intake [--json]`
- `inspect runtime-agent-problem-follow-up-planning-intake`
- `pilot follow-up-gate [--json]`
- `inspect runtime-agent-problem-follow-up-planning-gate`
- `pilot invocation [--json]`
- `inspect runtime-cli-invocation-contract`
- `pilot activation [--json]`
- `inspect runtime-cli-activation-plan`
- `pilot dist-smoke [--json]`
- `inspect runtime-local-dist-freshness-smoke`
- `pilot dist-binding [--json]`
- `inspect runtime-target-dist-binding-plan`
- `pilot target-proof [--json]`
- `inspect runtime-frozen-dist-target-readback-proof`
- `pilot guide [--json]`
- `inspect runtime-product-closure-pilot-guide`
- `pilot status [--json]`
- `inspect runtime-product-closure-pilot-status`
- `pilot resources [--json]`
- `inspect runtime-external-consumer-resource-pack`
- `pilot commit-hygiene [--json]`
- `inspect runtime-target-commit-hygiene`
- `pilot commit-plan [--json]`
- `inspect runtime-target-commit-plan`
- `pilot closure [--json]`
- `inspect runtime-target-commit-closure`
- `pilot residue [--json]`
- `inspect runtime-target-residue-policy`
- `pilot ignore-plan [--json]`
- `inspect runtime-target-ignore-decision-plan`
- `pilot ignore-record [--json]`
- `inspect runtime-target-ignore-decision-record`
- `pilot dist [--json]`
- `inspect runtime-local-dist-handoff`
- `pilot proof [--json]`
- `inspect runtime-product-pilot-proof`
- `intent draft`
- `plan init [candidate-card-id]`
- `plan submit-workspace <task-id> [reason...]`

## Product Closure Phase 0 Baseline

The current product closure baseline is frozen in:

- `docs/runtime/carves-product-closure-phase-0-baseline.md`

This baseline records `CARD-706` through `CARD-718` as the bounded governed agent handoff chain and keeps follow-up work focused on productization, onboarding, distribution, dogfood proof, and managed workspace hardening.

## Product Closure Phase 1 CLI Distribution

The Phase 1 product closure step is recorded in:

- `docs/runtime/carves-product-closure-phase-1-cli-distribution.md`
- `docs/guides/CARVES_CLI_DISTRIBUTION.md`

This phase adds the source-tree `carves.ps1` / `carves.cmd` wrappers and .NET tool package metadata so the governed handoff can be invoked as `carves agent handoff` without teaching users or agents the source project path.

## Product Closure Phase 2 Readiness Separation

The Phase 2 readiness boundary is recorded in:

- `docs/runtime/carves-product-closure-phase-2-readiness-separation.md`
- `docs/guides/CARVES_READINESS_BOUNDARY.md`

This phase adds `carves doctor [--json]` so tool readiness, target repo readiness, and resident host readiness are projected separately before operators or agents interpret `status` as a generic product-health command.

## Product Closure Phase 3 Minimal Init Onboarding

The Phase 3 first-run onboarding boundary is recorded in:

- `docs/runtime/carves-product-closure-phase-3-minimal-init-onboarding.md`
- `docs/guides/CARVES_INIT_FIRST_RUN.md`

This phase adds `carves init [path] [--json]` as a bounded first-run wrapper around the existing host-owned attach lane. It fails with no changes when the repo boundary or resident host is unavailable, and it points successful operators to `carves doctor`, `carves agent handoff`, `carves inspect runtime-first-run-operator-packet`, and `carves plan init [candidate-card-id]`.

## Product Closure Phase 4 External Target Dogfood Proof

The Phase 4 external-target proof is recorded in:

- `docs/runtime/carves-product-closure-phase-4-external-target-dogfood-proof.md`
- `docs/guides/CARVES_EXTERNAL_TARGET_DOGFOOD.md`

This phase proves an existing external target repo can run the governed first-run sequence from `init` through `doctor`, `agent handoff`, first-run packet readback, guided intent capture, and `plan init`. Runtime-owned documents are resolved from the Runtime document root recorded during attach, so target repos do not need to copy Runtime docs or become Runtime proof owners.

## Product Closure Phase 5 Real Project Pilot

The Phase 5 real-project pilot is recorded in:

- `docs/runtime/carves-product-closure-phase-5-real-project-pilot.md`
- `docs/guides/CARVES_REAL_PROJECT_PILOT.md`

This phase proves the same governed handoff path on the real sibling target repo `CARVES.AgentCoach`. It also tightens attached-target first-run packet readback so Runtime internal beta gate findings are summarized instead of expanded as target-owned warning noise.

## Product Closure Phase 6 Official Truth Writeback

The Phase 6 real-project writeback proof is recorded in:

- `docs/runtime/carves-product-closure-phase-6-official-truth-writeback.md`
- `docs/guides/CARVES_REAL_PROJECT_WRITEBACK.md`

This phase proves that the real sibling target repo can move from active planning into official approved card truth, approved taskgraph truth, pending task truth, and a projected acceptance contract. It also hardens the source-tree wrapper so Windows Application Control blocks on staged temp assemblies fall back to source project execution instead of stranding the operator.

## Product Closure Phase 7 Managed Workspace Execution

The Phase 7 real-project managed workspace proof is recorded in:

- `docs/runtime/carves-product-closure-phase-7-managed-workspace-execution.md`
- `docs/guides/CARVES_REAL_PROJECT_WORKSPACE.md`

This phase proves that the real sibling target repo can issue a task-bound managed workspace lease for `T-CARD-001-001`, project Mode D hardening as active, and resolve Runtime doctrine from the attached Runtime document root instead of requiring the target repo to copy Runtime docs.

## Product Closure Phase 8 Managed Workspace Writeback

The Phase 8 real-project managed workspace writeback proof is recorded in:

- `docs/runtime/carves-product-closure-phase-8-managed-workspace-writeback.md`
- `docs/guides/CARVES_REAL_PROJECT_WORKSPACE_WRITEBACK.md`

This phase proves that the real sibling target repo can submit workspace output through `plan submit-workspace`, project mutation-audit readiness, and materialize approved files through Runtime-owned `review approve` writeback instead of manual copy.

## Product Closure Phase 9 Productized Pilot Guide

The Phase 9 productized pilot guide closure is recorded in:

- `docs/runtime/carves-product-closure-phase-9-productized-pilot-guide.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_GUIDE.md`

This phase adds `carves pilot guide [--json]` and `runtime-product-closure-pilot-guide` as read-only productization entries for the proven external-project loop. It gives agents and operators one ordered path from `init` through managed workspace writeback and commit hygiene without granting the guide command mutation authority.

## Product Closure Phase 10 Productized Pilot Status

The Phase 10 productized pilot status closure is recorded in:

- `docs/runtime/carves-product-closure-phase-10-productized-pilot-status.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md`

This phase adds `carves pilot status [--json]`, `carves pilot preflight [--json]`, and `runtime-product-closure-pilot-status` as read-only stage-selection entries for the external-project loop. It tells agents which governed command owns the next step without automatically mutating target truth.

## Product Closure Phase 11B Target Agent Bootstrap Pack

The Phase 11B target agent bootstrap closure is recorded in:

- `docs/runtime/carves-product-closure-phase-11b-target-agent-bootstrap-pack.md`
- `docs/guides/CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md`

This phase materializes `.ai/AGENT_BOOTSTRAP.md` during attach/init and creates root `AGENTS.md` only when the target repo does not already have one. It gives generic external agents a stable first instruction to run `carves pilot status --json` before planning or editing, without granting the bootstrap files authority to overwrite target-owned instructions or bypass Runtime gates.

## Product Closure Phase 12 Existing Target Bootstrap Repair

The Phase 12 existing-target bootstrap repair closure is recorded in:

- `docs/runtime/carves-product-closure-phase-12-existing-target-bootstrap-repair.md`
- `docs/guides/CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md`

This phase adds `carves agent bootstrap [--write]` and `runtime-target-agent-bootstrap-pack` so already attached target repos can inspect and materialize missing bootstrap files without rerunning attach. The repair path creates only missing files, skips existing files, and lets `pilot status` block further agent work with `carves agent bootstrap --write` when required.

## Product Closure Phase 13 Target Commit Hygiene

The Phase 13 target commit hygiene closure is recorded in:

- `docs/runtime/carves-product-closure-phase-13-target-commit-hygiene.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md`

This phase adds `carves pilot commit-hygiene [--json]` and `runtime-target-commit-hygiene` so the final target-repo commit step is classified by CARVES before staging. It separates official target truth, target output candidates, local/tooling residue, and unclassified dirty paths without staging, committing, cleaning, or deleting anything.

## Product Closure Phase 14 Target Commit Plan

The Phase 14 target commit plan closure is recorded in:

- `docs/runtime/carves-product-closure-phase-14-target-commit-plan.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md`

This phase adds `carves pilot commit-plan [--json]` and `runtime-target-commit-plan` so the final target-repo commit step gets a deterministic stage/exclude/review plan and command preview. It still does not stage, commit, clean, delete, rewrite, or push anything.

## Product Closure Phase 15 Target Commit Closure

The Phase 15 target commit closure is recorded in:

- `docs/runtime/carves-product-closure-phase-15-target-commit-closure.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md`

This phase adds `carves pilot closure [--json]` and `runtime-target-commit-closure` so the external-project pilot can prove the target repo is clean after the operator-reviewed product proof commit. It still does not stage, commit, clean, delete, rewrite, push, tag, or release anything.

## Product Closure Phase 16 Local Dist Handoff

The Phase 16 local dist handoff closure is recorded in:

- `docs/runtime/carves-product-closure-phase-16-local-dist-handoff.md`
- `docs/guides/CARVES_RUNTIME_LOCAL_DIST.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md`

This phase adds `carves pilot dist [--json]` and `runtime-local-dist-handoff` so external target repos can prove whether they consume CARVES from a frozen local Runtime dist instead of the live Runtime source checkout. It still does not pack, copy, repair, retarget, stage, commit, push, tag, release, or update anything.

## Product Closure Phase 17 Product Pilot Proof

The Phase 17 product pilot proof closure is recorded in:

- `docs/runtime/carves-product-closure-phase-17-product-pilot-proof.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md`

This phase adds `carves pilot proof [--json]` and `runtime-product-pilot-proof` so external target repos can read one aggregate posture for local dist handoff plus target commit closure. It still does not initialize, plan, review, write back, stage, commit, push, tag, release, pack, copy, repair, or retarget anything.

## Product Closure Phase 18 External Consumer Resource Pack

The Phase 18 external consumer resource pack closure is recorded in:

- `docs/runtime/carves-product-closure-phase-18-external-consumer-resource-pack.md`
- `docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md`

This phase adds `carves pilot resources [--json]` and `runtime-external-consumer-resource-pack` so external target repos and generic agents can read the Runtime-owned docs, target-generated bootstrap projections, command entries, and boundaries before planning or editing. It keeps AgentCoach or any other target repo as optional dogfood evidence rather than a Runtime closure prerequisite.

## Product Closure Phase 19 CLI Invocation Contract

The Phase 19 CLI invocation contract closure is recorded in:

- `docs/runtime/carves-product-closure-phase-19-cli-invocation-contract.md`
- `docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md`

This phase adds `carves pilot invocation [--json]` and `runtime-cli-invocation-contract` so external target repos and generic agents can distinguish source-tree wrapper, frozen local-dist wrapper, Windows cmd shim, and future global alias usage before planning or editing. It is read-only and does not install aliases, publish packages, or change Runtime authority.

## Product Closure Phase 20 CLI Activation Plan

The Phase 20 CLI activation plan closure is recorded in:

- `docs/runtime/carves-product-closure-phase-20-cli-activation-plan.md`
- `docs/guides/CARVES_CLI_ACTIVATION_PLAN.md`

This phase adds `carves pilot activation [--json]`, `carves pilot alias [--json]`, and `runtime-cli-activation-plan` so external target repos and generic agents can see operator-owned activation choices such as absolute wrapper, session alias, PATH entry, cmd shim, and optional tool install. It is read-only and does not mutate PATH, edit shell profiles, install tools, or change Runtime authority.

## Product Closure Phase 21 Target Dist Binding Plan

The Phase 21 target dist binding plan closure is recorded in:

- `docs/runtime/carves-product-closure-phase-21-target-dist-binding-plan.md`
- `docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md`

This phase adds `carves pilot dist-binding [--json]`, `carves pilot bind-dist [--json]`, and `runtime-target-dist-binding-plan` so external target repos can see the operator-owned path for binding or retargeting to a frozen local Runtime dist. It is read-only and does not edit target runtime manifests, attach handshakes, PATH, shell profiles, staging, commits, packs, or releases.

## Product Closure Phase 22 Local Dist Freshness Smoke

The Phase 22 local dist freshness smoke closure is recorded in:

- `docs/runtime/carves-product-closure-phase-22-local-dist-freshness-smoke.md`
- `docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md`

This phase adds `carves pilot dist-smoke [--json]`, `carves pilot dist-freshness [--json]`, and `runtime-local-dist-freshness-smoke` so Runtime can prove the frozen local alpha dist includes current resources and matches the clean Runtime source HEAD before external targets use the binding plan. It is read-only and does not pack, copy, publish, retarget, stage, commit, or release anything.

## Product Closure Phase 23 Frozen Dist Target Readback Proof

The current frozen dist target readback proof closure is recorded in:

- `docs/runtime/carves-product-closure-phase-23-frozen-dist-target-readback-proof.md`
- `docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md`

This phase adds `carves pilot target-proof [--json]`, `carves pilot external-proof [--json]`, and `runtime-frozen-dist-target-readback-proof` so an external target can prove it is initialized, bootstrapped, and bound to the fresh frozen Runtime dist before agents treat it as stable external consumption. It is read-only and does not initialize, repair, retarget, stage, commit, pack, publish, or release anything.

## Product Closure Phase 24 Wrapper Runtime Root Binding

The wrapper Runtime root binding hardening is recorded in:

- `docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md`

This phase keeps the Phase 23 readback surface but fixes the binding path behind it: the PowerShell wrapper passes its own Runtime root into the CLI process, and `init . --json` uses that root as the attach authority for an external target. This prevents a real target from being initialized with a self-referential Runtime root.

## Product Closure Phase 25 External Target Product Proof Closure

The external target product proof closure hardening is recorded in:

- `docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md`

This phase keeps the existing commit-plan and product-proof surfaces but classifies attach/init-generated target truth as stageable official target truth, while keeping `.ai/runtime/attach.lock.json` excluded as local coordination residue. This lets a real frozen-dist scratch target move from `target-proof complete` to `pilot proof complete` after an operator-reviewed bootstrap commit.

## Product Closure Phase 26A Projection Cleanup

The Phase 26A cleanup keeps the active product closure projection aligned to Phase 25:

- `product_closure_phase=phase_25_external_target_product_proof_closure_ready`
- current closure document path is `docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md`
- current closure guide path is `docs/guides/CARVES_PRODUCTIZED_PILOT_GUIDE.md`

This is a projection/read-model cleanup, not a new external execution surface. It prevents downstream operators or agents from reading Phase 23 as the active product closure state after Phase 25 proof closure is complete.

## Product Closure Phase 26 Real External Repo Pilot

The real external repo pilot proof is recorded in:

- `docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md`

This phase proves the frozen local Runtime dist against the real sibling consumer repo `CARVES.Operator`, not a scratch target and not `CARVES.AgentCoach`. The target reached frozen-dist `target-proof`, deterministic `commit-plan`, target bootstrap commit, `closure`, and aggregate `pilot proof` completion.

The active product closure projection after Phase 26 was:

- `product_closure_phase=phase_26_real_external_repo_pilot_complete`
- current closure document path is `docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md`

## Product Closure Phase 27 External Target Residue Policy

The external target residue policy is recorded in:

- `docs/runtime/carves-product-closure-phase-27-external-target-residue-policy.md`

This phase adds `runtime-target-residue-policy` and `carves pilot residue [--json]` so external agents can distinguish product-proof dirty work from excluded local/tooling residue. It projects whether product proof can remain complete, lists residue paths, and suggests `.gitignore` review candidates without mutating the target repo.

The active product closure projection after Phase 27 was:

- `product_closure_phase=phase_27_external_target_residue_policy_ready`
- current closure document path is `docs/runtime/carves-product-closure-phase-27-external-target-residue-policy.md`

## Product Closure Phase 28 Target Ignore Decision Plan

The external target ignore decision plan is recorded in:

- `docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md`

This phase adds `runtime-target-ignore-decision-plan` and `carves pilot ignore-plan [--json]` so external agents can turn residue-policy suggested `.gitignore` entries into operator-reviewed decision candidates. It exposes keep-local, cleanup, and reviewed `.gitignore` choices plus a patch preview, but it does not mutate `.gitignore`, stage, clean, delete, commit, or push.

The active product closure projection after Phase 28 was:

- `product_closure_phase=phase_28_target_ignore_decision_plan_ready`
- current closure document path is `docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md`

## Product Closure Phase 29 Target Ignore Decision Record

The external target ignore decision record is recorded in:

- `docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md`

This phase adds `runtime-target-ignore-decision-record`, `carves pilot ignore-record [--json]`, and `carves pilot record-ignore-decision <decision> ...` so external agents can prove an operator-reviewed keep-local, cleanup, or `.gitignore` decision was durably recorded. The record lives under `.ai/runtime/target-ignore-decisions/` and must be committed through target commit closure after it is written. It does not mutate `.gitignore`, clean residue, stage, commit, or push by itself.

The active product closure projection after Phase 29 was:

- `product_closure_phase=phase_29_target_ignore_decision_record_ready`
- current closure document path is `docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md`

## Product Closure Phase 30 Target Ignore Decision Record Audit

The external target ignore decision record audit is recorded in:

- `docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md`

This phase keeps the same `runtime-target-ignore-decision-record`, `carves pilot ignore-record [--json]`, and `carves pilot record-ignore-decision <decision> ...` entry points, but the readback now audits the decision records before they can satisfy proof. Malformed JSON, invalid current-plan records, stale plan records, and conflicting decisions are projected explicitly. Only well-formed, current-plan, non-conflicting records can make `decision_record_ready=true`.

The active product closure projection after Phase 30 was:

- `product_closure_phase=phase_30_target_ignore_decision_record_audit_ready`
- current closure document path is `docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md`

## Product Closure Phase 31 Target Ignore Decision Record Commit Readback

The external target ignore decision record commit readback is recorded in:

- `docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md`

This phase keeps the same `runtime-target-ignore-decision-record` and `carves pilot ignore-record [--json]` entry points, but the readback now requires valid decision record paths to be tracked and clean in the target git work tree before they can satisfy product proof. A newly written but uncommitted decision record reports `target_ignore_decision_record_waiting_for_record_commit`, lists the uncommitted paths, and points the operator back through commit-plan, target commit closure, residue, ignore-plan, ignore-record, and proof.

The active product closure projection after Phase 31 was:

- `product_closure_phase=phase_31_target_ignore_decision_record_commit_readback_ready`
- current closure document path is `docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md`

## Product Closure Phase 32 Alpha External-Use Readiness Rollup

The alpha external-use readiness rollup is recorded in:

- `docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md`

This phase adds `runtime-alpha-external-use-readiness`, `carves pilot readiness [--json]`, and `carves pilot alpha [--json]` so operators and external agents can tell whether CARVES Runtime `0.2.0-beta.1` is ready for bounded external-project alpha use from the frozen local dist. It aggregates frozen dist freshness, external consumer resources, governed agent handoff, productized pilot guide, Session Gateway private-alpha handoff, and repeatability readiness. Per-target product proof remains required per target and does not block Runtime-owned readiness.

The active product closure projection is now:

- `product_closure_phase=phase_32_alpha_external_use_readiness_rollup_ready`
- current closure document path is `docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md`

## Product Closure Phase 33 External Target Pilot Start Bundle

The external target pilot start bundle is recorded in:

- `docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md`
- `docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md`

This phase adds `runtime-external-target-pilot-start`, `runtime-external-target-pilot-next`, `carves pilot start [--json]`, and `carves pilot next [--json]` so external agents can read a compact start packet, guardrails, stop triggers, and the next governed command before planning or editing. Current agent-facing bootstrap compresses the first-thread readback behind `carves agent start --json`; the `pilot start` and `pilot next` surfaces remain lower-level troubleshooting readbacks.

## Product Closure Phase 34 Agent Problem Intake

The agent problem intake surface is recorded in:

- `docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md`
- `docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md`

This phase adds `runtime-agent-problem-intake`, `carves pilot problem-intake [--json]`, and `carves pilot report-problem <json-path> [--json]` so external agents can stop on a CARVES blocker and submit bounded problem/evidence records without mutating protected truth roots or authorizing the blocked change.

## Product Closure Phase 35 Agent Problem Triage Ledger

The agent problem triage ledger is recorded in:

- `docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md`
- `docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md`

This phase adds `runtime-agent-problem-triage-ledger`, `carves pilot triage [--json]`, `carves pilot problem-triage [--json]`, and `carves pilot friction-ledger [--json]` so recorded problem intake can be grouped into an operator-facing friction queue. It does not resolve records, create cards, create tasks, or authorize continuation.

## Product Closure Phase 36 Agent Problem Follow-Up Candidates

The agent problem follow-up candidates surface is recorded in:

- `docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md`

This phase adds `runtime-agent-problem-follow-up-candidates`, `carves pilot follow-up [--json]`, `carves pilot problem-follow-up [--json]`, and `carves pilot triage-follow-up [--json]` so repeated or blocking problem patterns can be promoted to operator-review follow-up candidates. It does not create cards, create tasks, approve reviews, resolve records, or authorize an agent to continue past a CARVES blocker.

The phase closure projection was:

- `product_closure_phase=phase_36_agent_problem_follow_up_candidates_ready`
- phase closure document path is `docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md`

## Product Closure Phase 37 Agent Problem Follow-Up Decision Plan

The agent problem follow-up decision plan surface is recorded in:

- `docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md`

This phase adds `runtime-agent-problem-follow-up-decision-plan`, `carves pilot follow-up-plan [--json]`, `carves pilot problem-follow-up-plan [--json]`, and `carves pilot triage-follow-up-plan [--json]` so follow-up candidates can be projected into explicit accept/reject/wait choices before any accepted pattern enters the governed planning lane. It does not record durable decisions, create cards, create tasks, approve reviews, resolve records, or authorize an agent to continue past a CARVES blocker.

The phase closure projection was:

- `product_closure_phase=phase_37_agent_problem_follow_up_decision_plan_ready`
- phase closure document path is `docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md`

## Product Closure Phase 38 Agent Problem Follow-Up Decision Record

The agent problem follow-up decision record surface is recorded in:

- `docs/runtime/carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md`

This phase adds `runtime-agent-problem-follow-up-decision-record`, `carves pilot follow-up-record [--json]`, `carves pilot follow-up-decision-record [--json]`, `carves pilot problem-follow-up-record [--json]`, and `carves pilot record-follow-up-decision <decision> ...` so follow-up candidate decisions can be durably recorded before accepted patterns enter the governed planning lane. Records live under `.ai/runtime/agent-problem-follow-up-decisions/` and must pass malformed/invalid/stale/conflict and git commit readbacks. It does not create cards, create tasks, approve reviews, resolve records, or authorize an agent to continue past a CARVES blocker.

## Product Closure Phase 39 Agent Problem Follow-Up Planning Intake

The agent problem follow-up planning intake surface is recorded in:

- `docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md`

This phase adds `runtime-agent-problem-follow-up-planning-intake`, `carves pilot follow-up-intake [--json]`, `carves pilot follow-up-planning [--json]`, and `carves pilot problem-follow-up-intake [--json]` so accepted, clean, committed follow-up decision records are projected as formal planning inputs. It does not create cards, create tasks, approve reviews, mutate `.ai` truth, or authorize direct task truth edits.

The active product closure projection is now:

- `product_closure_phase=phase_39_agent_problem_follow_up_planning_intake_ready`

## Phase 40: Agent Problem Follow-Up Planning Gate

This phase adds `runtime-agent-problem-follow-up-planning-gate`, `carves pilot follow-up-gate [--json]`, `carves pilot follow-up-planning-gate [--json]`, and `carves pilot problem-follow-up-gate [--json]` so accepted follow-up planning inputs are checked against intent draft and the single active formal planning slot before `plan init`. It does not create cards, create tasks, approve reviews, mutate `.ai` truth, or authorize multiple active planning cards.

Closure readbacks:

- `product_closure_phase=phase_40_agent_problem_follow_up_planning_gate_ready`
- `carves inspect runtime-agent-problem-follow-up-planning-gate`
- `carves api runtime-agent-problem-follow-up-planning-gate`
- `carves pilot follow-up-gate --json`
- current closure document path is `docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md`

## Non-Claims

This proof does not claim:

- an agent cannot damage its disposable workspace
- CARVES has completed OS/process sandboxing
- ACP or MCP transports are fully implemented
- vendor-native permission files are the portable baseline
- a second planner or second control plane exists

The proof is bounded to Runtime-owned planning, task truth, acceptance contract, workspace/packet mediation, review evidence, mutation audit, and host writeback.
