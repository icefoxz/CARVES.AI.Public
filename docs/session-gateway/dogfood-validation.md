# Session Gateway V1 Dogfood Validation

## Purpose

This document records the bounded Stage 4 dogfood-validation truth for Session Gateway v1.

It exists to prove that the Runtime-embedded gateway baseline, Runtime-hosted thin shell, and same-lane mutation forwarding can be used for self-use validation without opening a second control plane.

It also exists to keep dogfood proof and operator-run proof separate.

## Dogfood-Validated Posture

Stage 4 is considered landed when all of the following are true:

- Runtime can create or resume a session through the Session Gateway lane
- the same lane can classify `discuss`, `plan`, and `governed_run`
- ordered session events remain Runtime-owned projections
- the thin shell remains a projection shell over Runtime truth
- accepted-operation lookup remains available under the same Session Gateway namespace

Stage 5 extends that proof when:

- approve / reject / replan forwarding reuse the same Runtime-owned gateway lane
- governed mutations still resolve through Runtime review or task lifecycle truth
- narrow private alpha readiness is asserted only because mutation forwarding remains Runtime-owned
- operator-proof semantics are explicit enough to stop at `WAITING_OPERATOR_SETUP` instead of silently pretending a real operator run already happened

## Current Bounded Validation Set

The bounded dogfood proof for v1 now covers:

- `POST /api/session-gateway/v1/sessions`
- `GET /api/session-gateway/v1/sessions/{session_id}`
- `POST /api/session-gateway/v1/sessions/{session_id}/messages`
- `GET /api/session-gateway/v1/sessions/{session_id}/events`
- `GET /session-gateway/v1/shell`
- `GET /api/session-gateway/v1/operations/{operation_id}`
- `POST /api/session-gateway/v1/operations/{operation_id}/approve`
- `POST /api/session-gateway/v1/operations/{operation_id}/reject`
- `POST /api/session-gateway/v1/operations/{operation_id}/replan`

## Deferred Follow-Ons

This validation does not claim:

- a broad or public alpha
- a front-end-owned retry or approval lane
- any second control plane or second task truth root
