# CARVES External Agent Quickstart

This is the short entry protocol for an external agent working inside a target repo that is attached to CARVES Runtime `0.6.1-beta`.

For operators, the product-level first command is now:

```text
Please read /path/to/CARVES.Runtime/START_CARVES.md and start CARVES in this project.
```

If you are running the terminal path yourself:

```bash
carves up <target-project>
```

After that succeeds, open the target project folder in the coding agent and say:

```text
start CARVES
```

`carves up` should report the same split handoff: the human opens the target project and says `start CARVES`; the agent reads the visible root pointer `CARVES_START.md` or `.carves/AGENT_START.md`, then uses the project-local launcher. Do not teach new users to run `host ensure` first; Host readiness is automated by `carves up` when the Runtime root is known and only becomes visible as troubleshooting when startup is blocked.

`START_CARVES.md` is the preferred agent-started entry when CARVES has already
been unpacked. It tells the agent the selected Runtime root. The agent should not search for CARVES globally or treat a global `carves` shim as authority. The agent should not classify the target as new or old by itself; `carves up` owns that classification and any startup blocking.
When JSON is available, the agent should read `target_project_classification`, `target_startup_mode`, `target_runtime_binding_status`, and `existing_project_handling` from `carves up --json`; an existing CARVES project must not be reinitialized as a new project. If `carves up` reports `rebind_required`, the agent must stop and show the output to the operator instead of editing `.ai/runtime.json` or `.ai/runtime/attach-handshake.json` by hand.

If the agent is unfamiliar with CARVES, paste the exact prompt printed by
`carves up`:

```text
start CARVES

Read CARVES_START.md first. Then run .carves/carves agent start --json from this project and follow CARVES output. Do not plan or edit before that readback.
```

It is valid to rerun `carves up <target-project>` after the first setup. An
already-initialized target should report readiness instead of failing as a dirty
target just because CARVES startup files are generated but not committed yet.

If the operator opens the Host URL in a browser, it should only show a
"CARVES Host is running" status pointer. That page is not the dashboard, not a
worker execution surface, and not the normal project startup path.

The G0 visible gateway contract is recorded in
[`runtime-project-recenter-carves-g0-visible-gateway-contract.md`](../runtime/runtime-project-recenter-carves-g0-visible-gateway-contract.md).
`carves`, `carves gateway`, `carves gateway status`, `carves status --watch`,
and Host `/` surfaces must make CARVES visibly running, waiting, or blocked
instead of silent. `carves shim` / `carves help shim` only print a safe global
shim pattern pinned by `CARVES_RUNTIME_ROOT`; they do not install files, mutate
PATH, or replace the project-local launcher. Treat these as product visibility
and invocation guidance only. This does not authorize API/SDK worker execution,
review approval, state sync, or direct `.ai` truth writeback.

Inside a target project after `carves up`, prefer `.carves/carves` over any
global `carves` command. If the operator asks whether CARVES is running, use:

```bash
.carves/carves gateway status
.carves/carves status --watch --iterations 1 --interval-ms 0
```

Run `.carves/carves gateway` only when the operator explicitly wants a foreground
gateway terminal. These visibility checks do not authorize worker execution or
lifecycle truth writeback.

Use the Runtime wrapper path confirmed by `carves pilot invocation --json` only for diagnostics or advanced distribution checks. For stable alpha work, prefer the frozen local dist wrapper.

Current `0.6.1-beta` Runtime posture opens the bounded startup, visible gateway, and Host-mediated external Codex App / Codex CLI worker lane. BYOK/provider SDK or API-backed worker execution is not opened by this candidate. Even when provider backends appear in reference or qualification surfaces, external agents must wait for Host worker policy, packet, evidence, result ingestion, and review approval instead of treating provider configuration as execution authority.

When the target project is being worked from WSL, keep the Runtime dist and the target project on the WSL filesystem and use the Unix dist wrapper directly:

```bash
<dist-root>/carves up /path/to/target-project
cd /path/to/target-project
.carves/carves agent start --json
```

The release dist wrapper runs `runtime-cli/carves.dll` directly. If the published CLI is missing, the wrapper fails clearly; it does not fall back to source execution. Use `carves host ensure --json` only when `carves up` or a diagnostic readback reports Host readiness as the blocker.

When `carves host ensure --json` reports `host_session_conflict`, do not keep retrying `host start` or `host ensure`. Use `carves host reconcile --replace-stale --json` first, then rerun the normal readiness check.

## First Command

```powershell
.carves/carves agent start --json
```

Read these fields before planning or editing:

- `thread_start_ready`
- `runtime_document_root`
- `startup_entry_source`
- `startup_boundary_ready`
- `startup_boundary_posture`
- `startup_boundary_gaps`
- `target_project_classification`
- `target_classification_owner`
- `target_startup_mode`
- `target_runtime_binding_status`
- `target_bound_runtime_root`
- `agent_target_classification_allowed`
- `agent_runtime_rebind_allowed`
- `current_stage_id`
- `next_governed_command`
- `next_command_source`
- `available_actions`
- `minimal_agent_rules`
- `stop_and_report_triggers`
- `troubleshooting_readbacks`

If `thread_start_ready=false` or `startup_boundary_ready=false`, do not plan or edit. Follow the listed `gaps` and `recommended_next_action`.

Do not classify the target as new or old yourself, and do not repair Runtime binding by hand. If `target_runtime_binding_status` is missing, mismatched, or requires rebind, stop and show CARVES output to the operator.

The current external-agent lane still exposes the legacy `next_governed_command` projection. The bounded hardening target for discussion-first intake and the minimum stateful-action gate is frozen in [runtime-conversation-first-minimum-stateful-action-gate-contract.md](../runtime/runtime-conversation-first-minimum-stateful-action-gate-contract.md). That contract defines the follow-on boundary; it does not claim that every legacy surface is already migrated.

## Before Each Step

```powershell
.carves/carves agent start --json
```

Use `available_actions` first when it is present. Keep `next_governed_command` only as the current compatibility projection hint.

The legacy projection field is:

```text
next_governed_command
```

Do not auto-run a stateful step from that field alone, and do not invent a parallel workflow when CARVES has already projected a bounded next action.

For compact reorientation after the required initialization report is already satisfied, prefer the aggregate readback:

```powershell
carves agent context --json
carves agent context <task-id> --json
carves api runtime-markdown-read-path-budget
carves api runtime-markdown-read-path-budget <task-id>
```

This aggregates thread start, bootstrap packet, task overlay, Markdown read-path budget, and context-pack pointers. It is read-only and does not materialize context-pack telemetry; run `carves context estimate <task-id>` only when a full context pack is needed.

The Markdown read-path budget keeps generated views such as `.ai/TASK_QUEUE.md` out of the default warm read path. Use broad Markdown reads only when the budget surface lists an escalation trigger or the task overlay names a targeted Markdown ref.

Use these troubleshooting readbacks only when `.carves/carves agent start --json` reports a gap, a blocker, or asks for deeper evidence:

```powershell
carves pilot problem-intake --json
carves pilot triage --json
carves pilot follow-up --json
carves pilot follow-up-plan --json
carves pilot follow-up-record --json
carves pilot follow-up-intake --json
carves pilot follow-up-gate --json
carves pilot next --json
```

Use execution audit queries only when execution history is directly relevant:

```powershell
carves api runtime-worker-execution-audit status:Failed
carves api runtime-worker-execution-audit task:<task-id>
carves api runtime-worker-execution-audit safety:blocked
```

Use the governance surface coverage audit only when validating alpha handoff wiring or investigating a missing Runtime governance readback:

```powershell
carves api runtime-governance-surface-coverage-audit
```

Use the default workflow proof only when validating that the default external-agent path remains short and wired:

```powershell
carves api runtime-default-workflow-proof
```

`pilot start` and `pilot next` are troubleshooting readbacks. When their own ready flags are true, non-blocking alpha-readiness advisory details stay out of their `gaps` list so an external agent does not treat optional readiness warnings as a blocked next step. Use `carves pilot readiness --json` only when the operator asks for the full readiness ledger.

When `pilot start` or `pilot next` is ready, its `recommended_next_action` is the bounded next-step explanation for the current projected action set. Do not run the troubleshooting readback chain first unless the projected step fails or the surface reports a gap.

If `current_stage_id=intent_capture`, the target is attached but the project goal is still not explicit enough for planning. Run `carves discuss context` first and stay in ordinary discussion while you ask what the project is for, what outcome is wanted, and whether the user actually wants engineering work. Run `carves intent draft --persist` only after the operator/user has answered with a bounded scope.

If `current_stage_id=ready_for_new_intent`, the previous product pilot proof is complete. Run `carves discuss context` first and stay in ordinary discussion while you ask what the project is for, what outcome is wanted, and whether new engineering work is actually requested. Run `carves intent draft --persist` only after the operator/user has answered with a new bounded scope. If no new scope is available, remain in discussion and do not open planning.

## Minimum Agent Loop

```text
agent start --json
-> prefer available_actions
-> treat next_governed_command as projection-only compatibility
-> if blocked, report-problem and stop
-> rerun agent start --json
-> continue until CARVES reports review, closure, or proof work
```

## Stop-And-Report Rule

Stop and preserve evidence when:

- `next_governed_command` is missing
- the next command fails
- CARVES reports a blocked posture
- you need to edit `.ai/tasks/`, `.ai/memory/`, `.ai/artifacts/reviews/`, or `.carves-platform/`
- the work lacks an acceptance contract
- the edit would fall outside the managed workspace lease or declared writable path
- you are tempted to rationalize a CARVES warning

When a stop trigger fires, create a bounded problem payload outside protected truth roots and run:

```powershell
carves pilot report-problem <json-path> --json
carves pilot triage --json
carves pilot follow-up --json
carves pilot follow-up-plan --json
carves pilot follow-up-record --json
carves pilot follow-up-intake --json
carves pilot follow-up-gate --json
```

Then stop and return the `problem_id`, `evidence_id`, command, output, relevant paths, triage lane, follow-up candidate status, follow-up decision posture, follow-up planning gate posture when available, and why you could not continue under the surfaced next action.

## Non-Authority

This quickstart does not authorize direct mutation.

It does not:

- initialize or attach a repo
- create cards or tasks
- issue a workspace
- approve review
- write back files
- stage or commit git changes
- retarget Runtime manifests
- edit `.gitignore`

All mutations still go through CARVES Host-routed commands and gates; the legacy next-command field is not standalone authority.

Problem intake is the only exception in this quickstart: it may write target runtime problem/evidence records, but it still does not authorize the blocked change.

`pilot triage` is read-only. It groups recorded problem intake for the operator and does not resolve records or authorize continuation.

`pilot follow-up` is read-only. It turns repeated or blocking problem patterns into operator-review candidates and does not create cards, tasks, approvals, or continuation authority.

`pilot follow-up-plan` is read-only. It projects accept/reject/wait choices for follow-up candidates and does not record decisions or create governed work. `pilot follow-up-record` shows whether durable operator decisions exist; `pilot follow-up-intake` shows which accepted, clean records may enter formal planning; `pilot follow-up-gate` tells whether the agent must run `intent draft --persist`, may run `plan init`, or must stop because an active planning slot already exists. Accepted records still have to enter the governed planning lane.
