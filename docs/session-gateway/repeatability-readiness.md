# Session Gateway V1 Repeatability Readiness

## Purpose

This document records the bounded Phase C truth for Session Gateway v1.

It exists to prove that private alpha is not only deliverable once, but can be repeated through the same Runtime-owned lane using the existing recovery, review, artifact, provider, and timeline surfaces.

## Current Posture

Repeatability readiness is considered landed when all of the following stay true:

- private alpha handoff remains `private_alpha_deliverable_ready`
- operator proof obligations remain explicit and blocking
- restart / recover / re-inspect / bundle / rerun all stay on the same Runtime-owned lane
- recent gateway work remains queryable from Runtime task/review/artifact truth
- recent delegated runs keep their projected `acceptance_contract` binding queryable from the same Runtime-owned lens
- rerun guidance does not widen proof claims beyond `repo_local_proof`

Current bounded posture:

- `overall_posture = repeatable_private_alpha_ready`
- `proof_source` remains repo-local until the operator produces real-world evidence
- the line remains `Strict Broker-only`

## What Repeatability Means Here

Phase C does not mean broad release or external-user proof.

It means a bounded operator can:

1. restart the resident host
2. inspect current private-alpha and repeatability truth
3. recover using the existing Runtime maintenance entry points
4. inspect recent Session Gateway task/review/artifact history
5. inspect whether recent delegated runs still project the same acceptance contract that task truth expects
5. collect the same bug-report bundle
6. rerun the bounded lane without inventing a second control plane

## Runtime-Owned Entry Points

Use these Runtime-owned entry points:

- `inspect runtime-session-gateway-repeatability`
- `inspect runtime-session-gateway-private-alpha-handoff`
- `inspect runtime-session-gateway-dogfood-validation`
- `api runtime-session-gateway-repeatability`
- `api operational-summary`
- `repair`
- `rebuild`
- `reset --derived`

Route references remain:

- `/session-gateway/v1/shell`
- `/api/session-gateway/v1/sessions`
- `/api/session-gateway/v1/sessions/{session_id}/messages`
- `/api/session-gateway/v1/sessions/{session_id}/events`
- `/api/session-gateway/v1/operations/{operation_id}`

## Evidence Expectations

Repeatability readiness still does not count as `operator_run_proof` or `external_user_proof`.

Runtime must still stop at the existing operator-proof contract unless the operator provides:

- a real repo path
- the real run command
- event and operation identifiers
- log excerpts
- result or failure artifacts
- a human verdict

## Non-Goals

This phase does not claim:

- a second control plane
- a second task or review truth root
- front-end-owned rerun or recovery authority
- external-user completion
- beta-scale product expansion
