# CARVES Target Agent Bootstrap Pack

This guide records the Phase 11B target repo bootstrap pack and the Phase 12 existing-target repair entry. Current generated bootstrap text also points agents at the Phase 15 target commit closure readback after staging and committing, the Phase 16 local dist handoff readback for stable external-project consumption, the Phase 17 final product pilot proof aggregate, the Phase 18 external consumer resource pack, the Phase 19 CLI invocation contract, the Phase 20 CLI activation plan, the Phase 21 target dist binding plan, the Phase 22 local dist freshness smoke, the Phase 23 frozen dist target readback proof, the Phase 27 target residue policy, the Phase 28 target ignore decision plan, the Phase 29 target ignore decision record, the Phase 30 target ignore decision record audit, the Phase 31 target ignore decision record commit readback, the Phase 32 alpha external-use readiness rollup, the Phase 34 agent problem intake surface, the Phase 35 agent problem triage ledger, the Phase 36 agent problem follow-up candidates, Phase 37 agent problem follow-up decision plan, Phase 38 durable follow-up decision records, Phase 39 planning intake, and Phase 40 planning gate.

Use it when an external target repo is attached to CARVES and must be understandable to generic agents such as IDE assistants, Codex, Claude, or other CLI agents.

## Generated Files

Attach/init creates:

```text
.carves/carves
.carves/AGENT_START.md
.carves/agent-start.json
.ai/AGENT_BOOTSTRAP.md
```

Attach/init also creates this file when it does not already exist:

```text
AGENTS.md
```

Existing root `AGENTS.md` is target-owned and is not overwritten.

`.carves/carves` is a project-local launcher. It points to the selected Runtime
wrapper so a generic agent can invoke CARVES from the target folder without
guessing PATH state or trusting a global `carves` alias.

`.carves/AGENT_START.md` is the short human-readable instruction for generic
agents. `.carves/agent-start.json` is the matching machine-readable projection
with the Runtime root, wrapper path, target root, first command, stop triggers,
and current `null_worker` worker-execution boundary.

## Root AGENTS.md Handling

If root `AGENTS.md` is absent, bootstrap materialization creates a minimal file
that points the agent to `.carves/AGENT_START.md` and `.carves/carves agent
start --json`.

If root `AGENTS.md` already exists, CARVES preserves it. The bootstrap surface
reports whether that file already contains a CARVES entry. When it does not,
the surface returns a suggested patch for operator review instead of editing the
target-owned file.

This is intentional. CARVES should be discoverable, but it must not silently
take over a project's existing agent instructions.

## Existing Target Repair

If a repo was attached before the bootstrap pack existed, run:

```powershell
carves agent bootstrap
carves agent bootstrap --write
```

The first command is read-only. The `--write` form creates only missing bootstrap files and skips existing files. It does not rerun attach and does not overwrite target-owned `AGENTS.md`.

## Required Agent Entry

Agents entering a target repo should first read:

```text
.carves/AGENT_START.md
```

Then run:

```powershell
.carves/carves agent start --json
```

Inside a target project, prefer `.carves/carves` over any global `carves`
command. If the operator asks whether CARVES is running, use the project-local
visibility checks:

```powershell
.carves/carves gateway status
.carves/carves status --watch --iterations 1 --interval-ms 0
```

Run `.carves/carves gateway` only when the operator explicitly wants a foreground
gateway terminal. These commands are visibility surfaces only; they do not
dispatch worker automation, approve review, sync state, or write lifecycle truth.

Then follow `available_actions` first, or `recommended_next_action` / `next_governed_command` when CARVES only exposes the legacy projection. Use the detailed readbacks below only when the start payload reports a gap or asks for deeper evidence:

```powershell
.carves/carves pilot readiness --json
.carves/carves pilot invocation --json
.carves/carves pilot activation --json
.carves/carves pilot dist-smoke --json
.carves/carves pilot dist-binding --json
.carves/carves pilot target-proof --json
.carves/carves pilot resources --json
.carves/carves pilot problem-intake --json
.carves/carves pilot triage --json
.carves/carves pilot follow-up --json
.carves/carves pilot follow-up-plan --json
.carves/carves pilot follow-up-record --json
.carves/carves pilot follow-up-intake --json
.carves/carves pilot follow-up-gate --json
.carves/carves pilot residue --json
.carves/carves pilot ignore-plan --json
.carves/carves pilot ignore-record --json
.carves/carves pilot status --json
```

If `.carves/carves`, `.carves/AGENT_START.md`, or
`.carves/agent-start.json` is missing in an older attached repo, run
`carves agent bootstrap --write` from an already verified Runtime wrapper to
repair the project-local launcher and bootstrap projections.

To verify the frozen local Runtime dist is current before binding, run:

```powershell
carves pilot dist-smoke --json
```

To verify Runtime-owned alpha external-use readiness before external agents plan or edit, run:

```powershell
carves pilot readiness --json
```

To see the stop-and-report schema before mutating project files, run:

```powershell
carves pilot problem-intake --json
```

To see recorded problem intake grouped for operator triage, run:

```powershell
carves pilot triage --json
```

To see repeated or blocking patterns that may need operator-reviewed governed follow-up, run:

```powershell
carves pilot follow-up --json
```

To see accept/reject/wait choices for follow-up candidates, run:

```powershell
carves pilot follow-up-plan --json
carves pilot follow-up-record --json
carves pilot follow-up-intake --json
carves pilot follow-up-gate --json
```

When a stop trigger fires, submit a bounded problem payload instead of editing protected truth:

```powershell
carves pilot report-problem <json-path> --json
```

Then rerun:

```powershell
carves pilot triage --json
carves pilot follow-up --json
carves pilot follow-up-plan --json
carves pilot follow-up-record --json
carves pilot follow-up-intake --json
carves pilot follow-up-gate --json
```

To see the operator-owned binding or retarget plan for a frozen local Runtime dist, run:

```powershell
carves pilot dist-binding --json
```

To verify that the target is bound to a frozen local Runtime dist, run:

```powershell
carves pilot dist --json
```

To verify that the initialized target, bootstrap files, freshness smoke, binding plan, and local dist handoff line up, run:

```powershell
carves pilot target-proof --json
```

To verify final external-project pilot closure, run:

```powershell
carves pilot residue --json
carves pilot ignore-plan --json
carves pilot ignore-record --json
carves pilot proof --json
```

The agent must read:

- `current_stage_id`
- `next_command`
- `gaps`

Then it must follow the named governed command instead of inventing a parallel workflow.

## Required Rules

- Do not rerun `carves init` when `.ai/runtime.json` already exists, unless `pilot status` reports `attach_target`.
- Do not edit `.ai/` official truth manually.
- Do not copy Runtime docs into target truth; read them from the Runtime document root or through CARVES surfaces.
- Do not claim Runtime alpha external-use readiness unless `carves pilot readiness --json` reports `alpha_external_use_ready=true`.
- Do not rationalize a CARVES blocked posture; run `carves pilot problem-intake --json`, submit `carves pilot report-problem <json-path> --json`, run `carves pilot triage --json`, run `carves pilot follow-up --json`, run `carves pilot follow-up-plan --json`, run `carves pilot follow-up-record --json`, run `carves pilot follow-up-intake --json`, run `carves pilot follow-up-gate --json`, then stop.
- Do not treat `carves pilot triage --json` as approval authority; it is a read-only friction ledger for operator review.
- Do not treat `carves pilot follow-up --json` as approval authority; it is a read-only candidate surface for operator review.
- Do not treat `carves pilot follow-up-plan --json` as decision authority; it is a read-only decision plan and does not record durable operator choices.
- Do not treat `carves pilot follow-up-record --json` as planning authority; it only proves durable operator choices and does not create governed work.
- Do not treat `carves pilot follow-up-intake --json` as card/task authority; run `carves pilot follow-up-gate --json` and follow its `next_governed_command` before any `carves plan init [candidate-card-id]`.
- Do not assume a global `carves` alias is authoritative until `carves pilot invocation --json` confirms the intended Runtime root.
- Do not edit shell profiles, machine PATH, or global tool installation as project work; use `carves pilot activation --json` as read-only operator guidance.
- Do not claim frozen dist freshness unless `carves pilot dist-smoke --json` reports `local_dist_freshness_smoke_ready=true`.
- Do not edit `.ai/runtime.json` or `.ai/runtime/attach-handshake.json` to retarget Runtime manually; use `carves pilot dist-binding --json` as read-only operator guidance.
- Do not claim external target frozen-dist readiness unless `carves pilot target-proof --json` reports `frozen_dist_target_readback_proof_complete=true`.
- Do not commit `.ai/runtime/live-state/` by default.
- Prefer `local_dist_handoff_ready` before treating the setup as stable external-project consumption.
- Run `carves pilot commit-plan` before staging or committing target changes.
- Run `carves pilot closure --json` after committing target changes.
- Run `carves pilot residue --json` when commit closure is complete but local/tooling residue remains.
- Run `carves pilot ignore-plan --json` before editing `.gitignore` for CARVES local/tooling residue; treat its patch preview as an operator-review candidate only.
- Run `carves pilot ignore-record --json` and `carves pilot record-ignore-decision <decision> --all --reason <reason>` when the ignore plan requires a durable operator decision; after writing the record, commit it through target commit closure until `decision_record_commit_ready=true`.
- Run `carves pilot proof --json` before declaring an external-project pilot closed.
- Do not copy managed workspace files manually into the target repo.
- Use `carves plan submit-workspace` and `carves review approve` for workspace return and writeback.
- Use CARVES intent and plan commands before durable formal planning.
- If `pilot status` reports `target_agent_bootstrap_missing`, run `carves agent bootstrap --write` before continuing.

## Non-Claims

This pack is a portable repo-level bootstrap layer. It does not provide OS sandboxing, full ACP/MCP, remote worker orchestration, or automatic git commit/push.
