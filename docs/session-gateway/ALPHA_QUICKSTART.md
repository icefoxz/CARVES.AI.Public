# Session Gateway V1 Alpha Quickstart

## Purpose

This is the shortest bounded path for a private alpha operator to confirm that the Runtime-owned Session Gateway lane is live.

## Quickstart Flow

1. Start the resident host and read the base URL:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 host start --interval-ms 200
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 host status
```

2. Open the thin shell:

- `/session-gateway/v1/shell`

3. Create or resume a session on the Runtime-owned lane:

- `POST /api/session-gateway/v1/sessions`

4. Submit a message and confirm classification:

- `POST /api/session-gateway/v1/sessions/{session_id}/messages`
- expected intents: `discuss`, `plan`, `governed_run`

5. Read ordered events:

- `GET /api/session-gateway/v1/sessions/{session_id}/events`

6. For governed runs, inspect the same-lane operation:

- `GET /api/session-gateway/v1/operations/{operation_id}`

7. If the scenario touches review or replanning, keep the forwarding on the same Runtime-owned lane:

- `POST /api/session-gateway/v1/operations/{operation_id}/approve`
- `POST /api/session-gateway/v1/operations/{operation_id}/reject`
- `POST /api/session-gateway/v1/operations/{operation_id}/replan`

8. Before claiming anything beyond repo-local readiness, read the operator proof contract on the same operation:

- `GET /api/session-gateway/v1/operations/{operation_id}`
- expect `operator_proof_contract.current_proof_source = repo_local_proof`
- expect `operator_proof_contract.current_operator_state = WAITING_OPERATOR_SETUP`

9. For bounded internal beta entry, inspect the Runtime-owned first-run packet and gate:

- minimum onboarding bundle: `README.md -> AGENTS.md -> inspect runtime-first-run-operator-packet`

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-first-run-operator-packet
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-session-gateway-internal-beta-gate
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-session-gateway-internal-beta-exit-contract
```

That readback keeps bootstrap truth, onboarding acceleration boundaries, current proof posture, and current sample weighting on the same bounded lane before the operator treats the session as a real first-run.

For the first real internal proof attempt, record the same run in:

- [internal-operator-proof-runbook.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/internal-operator-proof-runbook.md)
- [INTERNAL_OPERATOR_PROOF_PACKET_TEMPLATE.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/INTERNAL_OPERATOR_PROOF_PACKET_TEMPLATE.md)

## Expected Readback

The quickstart is considered aligned when:

- the shell renders without introducing a second control plane
- session create/read works
- message classification remains Runtime-owned
- event ordering remains append-only
- accepted-operation lookup stays under the same gateway namespace
- first-run packet posture stays `first_run_packet_ready`
- operator-proof semantics stay explicit and human-readable

## If Something Looks Wrong

Collect the bundle from [BUG_REPORT_BUNDLE.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/BUG_REPORT_BUNDLE.md) before attempting manual repair.
