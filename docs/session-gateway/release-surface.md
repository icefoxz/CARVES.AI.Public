# Session Gateway V1 Release Surface

## Purpose

This document freezes the bounded release posture for Session Gateway v1 after Stage 5 mutation forwarding and Phase B private alpha handoff land.

## Release Order

The line remains:

1. boundary and protocol docset
2. Runtime-embedded gateway implementation
3. thin `AgentShell.Web`
4. dogfood validation
5. narrow private alpha
6. private alpha handoff

## V1 Surface

Session Gateway v1 release scope is limited to:

- bounded conversation entry
- intent routing into `Discuss`, `Plan`, and `GovernedRun`
- Runtime-owned accepted-operation projection
- Runtime-owned review/replan/recovery projection
- thin-shell rendering over Runtime truth

Current embedded baseline:

- Runtime now exposes session create / read, message classify, and session event projection inside the existing host
- Runtime now exposes accepted-operation read-through under the same gateway namespace
- Runtime now serves a thin shell at `/session-gateway/v1/shell` that only projects and drives the existing gateway truth
- Runtime now has bounded dogfood validation over the same gateway lane and accepted-operation namespace
- Runtime now forwards approve / reject / replan through the same Runtime-owned Session Gateway operation lane
- narrow private alpha is now bounded-ready because mutation forwarding remains Runtime-owned and Strict Broker-only
- Runtime now exposes a bounded private-alpha handoff surface for operator setup, health visibility, maintenance entrypoints, and bug-report bundle collection
- Runtime now exposes an explicit operator-proof contract so repo-local readiness cannot be mistaken for operator-run or external-user completion
- Runtime now exposes a bounded repeatability-readiness surface for recovery commands, recent gateway task history, bundle access, provider visibility, and rerun/timeline references over the same lane
- Runtime now exposes a bounded internal-beta gate surface for explicit entry scope, proof-artifact linkage, and blocked-claim projection after the attached-repo scoping repair
- Runtime now exposes a bounded first-run operator packet surface for bootstrap-truth expectations, onboarding-acceleration boundaries, and proof-aware operator entry on the same lane
- Runtime now exposes a bounded internal-beta exit-contract surface for representative sample weighting, blocked-claim preservation, and explicit next-judgment projection over the same lane
- Runtime now exposes a bounded governance-assist surface for artifact weight ledger, change pressure, dynamic gate mode, and decomposition candidates over the same lane
- the former Studio family is retired from the active Runtime lane and preserved only as historical downstream framing; see `studio-retirement-routing.md`

## V1 Not Included

Session Gateway v1 does not include:

- Hybrid broker/executor mode
- front-end-owned task truth
- front-end-owned review queue
- front-end-owned patch application
- public broad release before dogfood proof

## Alpha Gate

Private alpha should only open after:

- the embedded Runtime gateway semantics are stable
- the event model is proven in dogfood use
- governance semantics remain unchanged under provider changes
- approval/rejection/replan/recovery flow through one Runtime-owned lane

Current posture:

- the Runtime-owned lane is proven
- mutation forwarding is landed
- the release posture is `governance_assist_observe_ready`
- bounded internal beta entry is explicit through the new Runtime-owned gate surface
- bounded first-run operator entry is explicit through the new Runtime-owned packet surface
- the release lane now blocks real-world completion claims until operator proof obligations are satisfied

## Maintenance Posture

All follow-on work on this line remains normal bounded maintenance work, not a reopened convergence wave.
