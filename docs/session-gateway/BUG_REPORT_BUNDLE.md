# Session Gateway V1 Bug Report Bundle

## Purpose

Collect this bundle before filing a private alpha bug.

The goal is to preserve Runtime-owned evidence without inventing a second issue lane.

## Required Commands

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-session-gateway-private-alpha-handoff
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-session-gateway-dogfood-validation
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-governance-program-reaudit
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 api operational-summary
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 worker health --no-refresh
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 status --summary
```

## When The Failure Involves Session Or Event Flow

Also capture:

- the session id
- the operation id, if present
- `GET /api/session-gateway/v1/sessions/{session_id}/events`
- `GET /api/session-gateway/v1/operations/{operation_id}`
- the current `operator_proof_contract` block from that operation payload
- whether the lane was in `WAITING_OPERATOR_SETUP`, `WAITING_OPERATOR_RUN`, `WAITING_OPERATOR_EVIDENCE`, or `WAITING_OPERATOR_VERDICT`

## When The Failure Involves Repair Or Recovery

Capture the current maintenance suggestion before changing anything:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 repair
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 rebuild
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 reset --derived
```

Do not attach ad-hoc local edits as a substitute for Runtime-owned evidence.
