# Session Gateway V1 Known Limitations

## Proof Semantics

Private alpha is now explicit about proof-source limits:

- repo-local readiness is not operator-run proof
- operator-run proof is not external-user proof
- the system will project operator wait states before it claims real-world completion

This is intentional.

## Current Boundary

Session Gateway v1 is still deliberately narrow.

Current limits:

- local-first only
- single-user only
- single Runtime control kernel only
- single execution cell only
- Strict Broker-only only

## Not Included

Session Gateway v1 does not include:

- a second planner
- a second scheduler
- a second task truth root
- client-owned provider authority
- client-owned patch, review, or approval truth
- public broad release posture

## Operational Limits

- provider visibility is surfaced, but provider authority still stays inside Runtime
- maintenance and reset stay host-routed
- private alpha handoff is bounded and operator-supervised
- shell state is projection-only and disposable

## Escalation Rule

If a private alpha task cannot be completed without widening these boundaries, stop and open a new bounded maintenance card instead of patching around the boundary.
