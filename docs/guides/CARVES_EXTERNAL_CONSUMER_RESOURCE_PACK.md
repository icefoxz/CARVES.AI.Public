# CARVES External Consumer Resource Pack

This guide records the Phase 18 resource pack for external projects and generic agents. Phase 19 adds the CLI invocation contract that should be read before relying on a generic `carves` alias. Phase 20 adds the read-only activation plan that explains operator-owned alias and PATH choices. Phase 21 adds the read-only target dist binding plan that shows how to bind a target repo to a frozen local Runtime dist without manual manifest edits. Phase 22 adds the read-only local dist freshness smoke that proves the dist is current before binding. Phase 23 adds the read-only target proof that verifies the current target is initialized, bootstrapped, and bound to that fresh frozen dist. Phase 27 adds the read-only target residue policy that explains excluded local/tooling residue after commit closure. Phase 28 adds the read-only target ignore decision plan that turns residue suggestions into operator-reviewed keep-local, cleanup, or `.gitignore` candidates without mutating the target. Phase 29 adds the durable target ignore decision record for operator-reviewed choices. Phase 30 adds the audit that prevents malformed, invalid, stale, or conflicting decision records from satisfying proof. Phase 31 adds the commit readback that prevents untracked or dirty decision record paths from satisfying proof. Phase 32 adds the Runtime-owned alpha external-use readiness rollup. Phase 33 adds external-agent start/next. Phase 34 adds structured agent problem intake. Phase 35 adds the read-only agent problem triage ledger. Phase 36 adds read-only agent problem follow-up candidates. Phase 37 adds the read-only agent problem follow-up decision plan. Phase 38 adds durable follow-up decision records. Phase 39 adds accepted-record planning intake. Phase 40 adds the follow-up planning gate before `plan init`. The current first-thread command remains `carves agent start --json`; compact reorientation can use the single short-context aggregate `carves agent context --json`.

Use it when the question is:

```text
What does an external project need in order to call CARVES without copying Runtime docs or turning the target repo into the Runtime truth owner?
```

## Command

From an attached target repo or Runtime repo:

```powershell
carves pilot resources
carves pilot resources --json
carves agent start
carves agent start --json
carves agent context
carves agent context --json
carves agent context <task-id> --json
carves api runtime-markdown-read-path-budget
carves api runtime-markdown-read-path-budget <task-id>
carves api runtime-worker-execution-audit status:Failed
carves api runtime-worker-execution-audit task:<task-id>
carves api runtime-governance-surface-coverage-audit
carves api runtime-default-workflow-proof
carves agent boot
carves agent boot --json
carves pilot boot
carves pilot boot --json
carves pilot agent-start
carves pilot agent-start --json
carves pilot readiness
carves pilot readiness --json
carves pilot alpha
carves pilot alpha --json
carves pilot start
carves pilot start --json
carves pilot problem-intake
carves pilot problem-intake --json
carves pilot triage
carves pilot triage --json
carves pilot problem-triage
carves pilot problem-triage --json
carves pilot friction-ledger
carves pilot friction-ledger --json
carves pilot follow-up
carves pilot follow-up --json
carves pilot problem-follow-up
carves pilot problem-follow-up --json
carves pilot triage-follow-up
carves pilot triage-follow-up --json
carves pilot follow-up-plan
carves pilot follow-up-plan --json
carves pilot problem-follow-up-plan
carves pilot problem-follow-up-plan --json
carves pilot triage-follow-up-plan
carves pilot triage-follow-up-plan --json
carves pilot follow-up-record
carves pilot follow-up-record --json
carves pilot follow-up-decision-record --json
carves pilot problem-follow-up-record --json
carves pilot follow-up-intake
carves pilot follow-up-intake --json
carves pilot follow-up-planning --json
carves pilot problem-follow-up-intake --json
carves pilot follow-up-gate
carves pilot follow-up-gate --json
carves pilot follow-up-planning-gate --json
carves pilot problem-follow-up-gate --json
carves pilot record-follow-up-decision <decision> --all --reason <text>
carves pilot report-problem <json-path>
carves pilot report-problem <json-path> --json
carves pilot list-problems
carves pilot inspect-problem <problem-id>
carves pilot next
carves pilot next --json
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
carves pilot residue
carves pilot residue --json
carves pilot ignore-plan
carves pilot ignore-plan --json
carves pilot ignore-record
carves pilot ignore-record --json
carves pilot record-ignore-decision keep_local --all --reason "operator accepted local CARVES residue"
```

Equivalent inspect/API surfaces:

```powershell
carves inspect runtime-external-consumer-resource-pack
carves api runtime-external-consumer-resource-pack
carves inspect runtime-alpha-external-use-readiness
carves api runtime-alpha-external-use-readiness
carves inspect runtime-cli-invocation-contract
carves api runtime-cli-invocation-contract
carves inspect runtime-cli-activation-plan
carves api runtime-cli-activation-plan
carves inspect runtime-target-residue-policy
carves api runtime-target-residue-policy
carves inspect runtime-target-ignore-decision-plan
carves api runtime-target-ignore-decision-plan
carves inspect runtime-target-ignore-decision-record
carves api runtime-target-ignore-decision-record
carves inspect runtime-local-dist-freshness-smoke
carves api runtime-local-dist-freshness-smoke
carves inspect runtime-target-dist-binding-plan
carves api runtime-target-dist-binding-plan
carves inspect runtime-frozen-dist-target-readback-proof
carves api runtime-frozen-dist-target-readback-proof
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
carves inspect runtime-agent-thread-start
carves api runtime-agent-thread-start
carves inspect runtime-agent-short-context
carves api runtime-agent-short-context
carves inspect runtime-agent-short-context <task-id>
carves api runtime-agent-short-context <task-id>
carves inspect runtime-markdown-read-path-budget
carves api runtime-markdown-read-path-budget
carves inspect runtime-markdown-read-path-budget <task-id>
carves api runtime-markdown-read-path-budget <task-id>
carves inspect runtime-worker-execution-audit
carves api runtime-worker-execution-audit
carves inspect runtime-worker-execution-audit <query>
carves api runtime-worker-execution-audit <query>
carves inspect runtime-governance-surface-coverage-audit
carves api runtime-governance-surface-coverage-audit
carves inspect runtime-default-workflow-proof
carves api runtime-default-workflow-proof
```

## Runtime-Owned Resources

The resource pack treats these as Runtime-owned:

```text
carves.ps1
carves.cmd
docs/guides/CARVES_CLI_ACTIVATION_PLAN.md
docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md
docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md
docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md
docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md
docs/guides/CARVES_CLI_DISTRIBUTION.md
docs/guides/CARVES_PRODUCTIZED_PILOT_GUIDE.md
docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md
docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md
docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md
docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md
docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md
docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md
docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md
docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md
docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md
docs/guides/CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md
docs/guides/CARVES_RUNTIME_LOCAL_DIST.md
docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md
docs/runtime/runtime-governed-agent-handoff-proof.md
docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md
docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md
docs/runtime/carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md
docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md
docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md
docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md
docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md
docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md
docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md
docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md
docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md
docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md
docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md
docs/runtime/carves-product-closure-phase-27-external-target-residue-policy.md
docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md
docs/runtime/carves-product-closure-phase-26a-product-closure-projection-cleanup.md
docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md
docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md
docs/runtime/carves-product-closure-phase-23-frozen-dist-target-readback-proof.md
docs/runtime/carves-product-closure-phase-22-local-dist-freshness-smoke.md
docs/runtime/carves-product-closure-phase-21-target-dist-binding-plan.md
docs/runtime/carves-product-closure-phase-20-cli-activation-plan.md
docs/runtime/carves-product-closure-phase-19-cli-invocation-contract.md
docs/runtime/carves-product-closure-phase-18-external-consumer-resource-pack.md
```

External projects read these from the Runtime document root recorded by attach. They should not copy them into target product truth.

## Target-Generated Resources

The target repo may contain generated bootstrap projections:

```text
.ai/runtime.json
.ai/runtime/attach-handshake.json
.ai/AGENT_BOOTSTRAP.md
AGENTS.md
```

`.ai/runtime.json` and `.ai/runtime/attach-handshake.json` are attach truth. They are created by `carves init [target-path] --json`.

`.ai/AGENT_BOOTSTRAP.md` and `AGENTS.md` are bootstrap projections. They are created only when missing by `carves agent bootstrap --write`; existing `AGENTS.md` remains target-owned.

## Agent Entry

Tell external agents:

```text
Before planning or editing in a new thread, run `carves agent start --json`, treat `next_governed_command` as a legacy projection hint, and prefer `available_actions` when present. After required initialization is satisfied, use `carves agent context --json` or `carves agent context <task-id> --json` for compact reorientation across thread start, bootstrap packet, Markdown read-path budget, task overlay, and context-pack pointers. Use `carves api runtime-markdown-read-path-budget` when deciding whether a Markdown file should be opened, deferred, or treated as escalation-only. Use `carves api runtime-worker-execution-audit status:Failed` or `carves api runtime-worker-execution-audit task:<task-id>` only when execution-history audit evidence is directly relevant. Use `carves api runtime-governance-surface-coverage-audit` only when validating alpha handoff wiring or investigating a missing Runtime governance readback. Use `carves api runtime-default-workflow-proof` only when proving the default path remains short and wired. Use `carves pilot report-problem <json-path> --json` when a stop trigger fires. Use `carves pilot start --json`, `carves pilot next --json`, `carves pilot problem-intake --json`, `carves pilot triage --json`, `carves pilot follow-up --json`, `carves pilot follow-up-plan --json`, `carves pilot follow-up-record --json`, `carves pilot follow-up-intake --json`, `carves pilot follow-up-gate --json`, `carves pilot readiness --json`, `carves pilot invocation --json`, `carves pilot activation --json`, `carves pilot dist-smoke --json`, `carves pilot dist-binding --json`, `carves pilot target-proof --json`, `carves pilot resources --json`, `carves pilot residue --json`, `carves pilot ignore-plan --json`, `carves pilot ignore-record --json`, and `carves pilot status --json` only when the start payload reports a gap or asks for deeper evidence.
Read Runtime-owned docs from the Runtime document root. Do not copy Runtime docs into target truth.
Do not treat `next_governed_command` from `agent start` as standalone mutation authority.
```

## Boundary Rules

- Runtime-owned docs stay under the Runtime document root.
- Alpha external-use readiness is a Runtime rollup, not target product proof.
- Generic aliases are convenience only; verify invocation with `carves pilot invocation --json` before relying on them.
- Activation is operator-owned convenience; agents should not edit PATH, profiles, or tool installation as project work.
- Dist freshness is read-only; agents should not edit the dist folder manually to make smoke pass.
- Dist binding is operator-owned; agents should not edit `.ai/runtime.json` or `.ai/runtime/attach-handshake.json` manually.
- Problem intake records target runtime problem/evidence only; agents should use it when blocked instead of editing protected truth roots.
- Problem triage is read-only; it groups recorded problem intake into an operator-facing friction ledger and does not resolve, approve, or authorize continuation.
- Problem follow-up candidates are read-only; they promote repeated or blocking friction into operator-review candidates and do not create cards, tasks, approvals, or continuation authority.
- Problem follow-up decision plan is read-only; it projects accept/reject/wait choices and does not record decisions or create governed work.
- Problem follow-up decision records are operator-reviewed target truth; they audit malformed, invalid, stale, and conflicting choices and still do not create cards or tasks.
- Problem follow-up planning intake is read-only; it projects accepted, clean, committed decision records into formal planning inputs without creating cards or tasks.
- Problem follow-up planning gate is read-only; it checks accepted planning inputs against intent draft and the single active planning slot before `plan init`.
- Frozen dist target proof is read-only; agents should not claim stable external consumption until `frozen_dist_target_readback_proof_complete=true`.
- Target bootstrap files guide agents but do not own planning, task, review, or writeback truth.
- Official target truth enters through Runtime commands and host-owned writeback.
- Final target git closure remains operator-reviewed through `carves pilot commit-plan`, `carves pilot closure`, `carves pilot residue`, `carves pilot ignore-plan`, `carves pilot ignore-record`, and `carves pilot proof`.
- Ignore decisions are read-only until an operator explicitly chooses keep-local, cleanup, or a reviewed `.gitignore` edit.
- Durable ignore decision records are target truth under `.ai/runtime/target-ignore-decisions/` and must be committed through the normal target commit closure path.
- The ignore decision record readback also audits malformed, invalid, stale, and conflicting records before proof can remain complete.
- The ignore decision record readback also requires decision record paths to be tracked and clean in the target git work tree before proof can remain complete.
- A real target repo can be used as dogfood evidence, but no specific target repo blocks Runtime closure.

## Non-Claims

This resource pack does not initialize, plan, review, write back, stage, commit, push, tag, release, pack, copy, repair, or retarget anything. It does not claim OS sandboxing, full ACP/MCP, public package distribution, or remote worker orchestration.
