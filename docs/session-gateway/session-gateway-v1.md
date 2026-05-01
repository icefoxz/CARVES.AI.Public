# Session Gateway V1

## Purpose

This document freezes the bounded `Session Gateway v1` scope after Runtime governance closure.

It defines what Session Gateway v1 is allowed to do, what it must delegate back into Runtime, and what must stay out of scope until the embedded gateway semantics are proven.

## Current Posture

Session Gateway v1 starts from these repo-local truths:

- Runtime governance convergence is closed.
- Runtime remains the only control kernel and truth owner.
- Session Gateway is a bounded conversation-entry surface.
- AgentShell remains a thin shell over Runtime truth.
- v1 is `Strict Broker only`.

## Frozen Principles

- Runtime owns task, review, artifact, approval, and evidence truth.
- Session Gateway brokers conversation and routes intent into existing Runtime-owned surfaces.
- AgentShell must not become a second executor, planner, scheduler, or state root.
- Front-end code must not own repo write, git, shell, or provider-key authority.
- Provider changes must not alter governance semantics.
- Multiple devices may project the same lane, but only CARVES remains the official truth ingress for project-state-changing actions.
- governed gateway ingress remains a thin request layer over Runtime-owned mutation forwarding

## V1 User Paths

Session Gateway v1 only needs to support three user-visible paths:

1. `Discuss`
   - read-oriented conversation over existing Runtime truth
2. `Plan`
   - proposal-oriented entry that routes into Runtime-owned planning surfaces
3. `Governed Run`
   - execution-oriented entry that still routes into Runtime-owned task, review, and writeback truth

## Docset Map

This frozen v1 docset is:

- [session-gateway-v1-post-closure-execution-plan.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md)
- [session-api.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/session-api.md)
- [event-model.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/event-model.md)
- [intent-routing.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/intent-routing.md)
- [gateway-boundary.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/gateway-boundary.md)
- [gate-policy.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/gate-policy.md)
- [release-surface.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/release-surface.md)
- [dogfood-validation.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/dogfood-validation.md)
- [operator-proof-contract.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/operator-proof-contract.md)
- [governance-assist.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/governance-assist.md)
- [studio-retirement-routing.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/studio-retirement-routing.md)
- [capability-forge-retirement-routing.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/capability-forge-retirement-routing.md)
- [repeatability-readiness.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/repeatability-readiness.md)
- [ALPHA_SETUP.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/ALPHA_SETUP.md)
- [ALPHA_QUICKSTART.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/ALPHA_QUICKSTART.md)
- [KNOWN_LIMITATIONS.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/KNOWN_LIMITATIONS.md)
- [BUG_REPORT_BUNDLE.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/BUG_REPORT_BUNDLE.md)
- [runtime-central-interaction-point-and-official-truth-ingress.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/runtime/runtime-central-interaction-point-and-official-truth-ingress.md)
- [runtime-agent-governed-gateway-ingress-contract.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/runtime/runtime-agent-governed-gateway-ingress-contract.md)
- [runtime-agent-gateway-task-context-handoff-into-host-contract.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/runtime/runtime-agent-gateway-task-context-handoff-into-host-contract.md)
- [runtime-agent-governed-thin-frontend-bridge-boundary-contract.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/runtime/runtime-agent-governed-thin-frontend-bridge-boundary-contract.md)

## Non-Goals

Session Gateway v1 does not claim:

- a second control plane
- a second task truth root
- Hybrid front-end routing
- front-end-owned patch/review/approval truth
- direct front-end repo mutation authority
- an already shipped AgentShell product

## Exit To Implementation

Only after this docset is frozen should follow-on cards open Runtime-embedded gateway implementation work.

That first implementation slice is now landed as a bounded Runtime baseline:

- session create / read
- message classification into `Discuss`, `Plan`, and `GovernedRun`
- ordered session event projection
- no second control plane and no front-end-owned execution lane

The next bounded slice is also now landed as a Runtime-hosted thin shell:

- a minimal `AgentShell.Web` projection page served by the existing host
- bounded local UI state only
- no front-end-owned review, approval, or task truth

The next bounded slice is now also landed as dogfood validation:

- the same Runtime-owned gateway lane now proves create/resume, discuss/plan/governed_run classification, ordered events, shell projection, and accepted-operation lookup
- review / reject / replan forwarding is now also landed on the same Runtime-owned gateway lane
- narrow private alpha readiness is now bounded-ready because the forwarding path remains Runtime-owned

The next bounded Stage 3 freeze now also lands the governed ingress contract:

- external-agent and frontend-originated gateway traffic remains a thin ingress layer only
- accepted-operation lookup is a forwarding aid, not a second mutation ledger
- official review / approval / replan / writeback authority remains Runtime-owned

The next bounded Stage 3 freeze now also lands task-context handoff into Host:

- gateway may carry one bounded `target_task_id` into accepted-operation posture
- Host remains the owner of task overlay, memory binding, and mutation routing
- Session Gateway does not become a second task bootstrap or task-routing authority

The next bounded Stage 5 freeze now also lands the thin frontend bridge boundary:

- canonical CLI bridge, browser Workbench, and Runtime-hosted shell stay on the same Runtime-owned Host and Session Gateway lane
- bounded bridge-local state is limited to rendering, drafts, session hints, and route continuity helpers
- Runtime still does not claim an installable product shell or a second control plane in this slice

The next bounded slice is now also landed as private alpha handoff:

- Runtime now exposes a bounded `runtime-session-gateway-private-alpha-handoff` inspect/api surface
- the alpha operator docset now covers setup, quickstart, known limitations, and bug-report bundle collection
- the line remains Strict Broker-only and Runtime-owned

The next bounded slice is now also landed as an operator proof contract:

- Runtime now distinguishes `synthetic_fixture`, `repo_local_proof`, `operator_run_proof`, and `external_user_proof`
- the lane now projects explicit operator wait states and blocking proof events
- private alpha can now say when the operator must act and why repo-local truth is not enough

The next bounded slice is now also landed as repeatability readiness:

- Runtime now exposes a bounded `runtime-session-gateway-repeatability` inspect/api surface
- recent gateway task history, recovery entry points, bundle commands, provider visibility, and rerun/timeline references stay Runtime-owned
- repeatability readiness does not widen proof claims beyond the existing operator-proof contract

The next bounded slice is now also landed as governance assist:

- Runtime now exposes a bounded `runtime-session-gateway-governance-assist` inspect/api surface
- artifact weight ledger, change pressure, dynamic gate mode, and decomposition candidates stay Runtime-owned and read-only
- governance assist remains observe/assist only and does not introduce a second planner or enforcement gate

## Guided Planning Follow-On Alignment

For guided planning follow-on work:

- Session Gateway may continue to broker natural-language `Discuss` and `Plan` turns while preserving focus continuity such as `focus_card_id`, selected planning slice, or short-lived draft hints
- Runtime remains the owner of Scope Frame, Pending Decision, Candidate Card, grounded-card handoff, and official card writeback truth
- Session Gateway does not become a second candidate-card store, a second planner, or a UI-owned card lifecycle lane
- a downstream shell may render graph interaction over the same Runtime lane, but confirmed writeback still returns through Host-routed card and task mutations

- the former `CARVES Intelligence Studio` Runtime family is retired from the active Runtime lane; see `studio-retirement-routing.md`
- historical Studio framing docs may remain for downstream reference, but they are no longer active Runtime release truth
- the former Runtime-owned `CARVES Capability Forge / Tool Forge` family is retired from the active Runtime lane; see `capability-forge-retirement-routing.md`
- historical Capability Forge framing docs may remain for downstream reference, but they are no longer active Runtime release truth
