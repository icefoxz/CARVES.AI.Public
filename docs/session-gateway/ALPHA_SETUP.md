# Session Gateway V1 Alpha Setup

## Purpose

This document defines the bounded setup path for the private alpha handoff.

The setup must preserve:

- local-first posture
- single-user operation
- a single Runtime control kernel
- Strict Broker-only Session Gateway semantics
- explicit operator proof obligations

## Preconditions

- build the Runtime host once
- start the resident host from the Runtime repo root
- confirm Runtime governance remains closed before handing the lane to a private alpha operator

## Startup Path

1. Build the host:

```powershell
dotnet build src/CARVES.Runtime.Host/Carves.Runtime.Host.csproj --no-restore -p:UseSharedCompilation=false -m:1
```

2. Start the resident host:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 host start --interval-ms 200
```

3. Read the first-run operator packet before treating setup as a real project entry:

- minimum onboarding bundle: `README.md -> AGENTS.md -> inspect runtime-first-run-operator-packet`

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-first-run-operator-packet
```

4. Confirm the handoff surface:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-session-gateway-private-alpha-handoff
```

5. Confirm provider and runtime visibility:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 api operational-summary
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 worker health --no-refresh
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 status --summary
```

6. Read the operator proof contract before doing anything that claims real-world completion:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-first-run-operator-packet
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-session-gateway-private-alpha-handoff
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-session-gateway-internal-beta-gate
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-session-gateway-internal-beta-exit-contract
```

For bounded internal beta admission, also read:

- [internal-beta-gate.md](./internal-beta-gate.md)
- [internal-beta-exit-contract.md](./internal-beta-exit-contract.md)
- [../runtime/runtime-first-run-operator-packet.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/runtime/runtime-first-run-operator-packet.md)

For the first real internal proof attempt, use the governed runbook and packet:

- [internal-operator-proof-runbook.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/internal-operator-proof-runbook.md)
- [INTERNAL_OPERATOR_PROOF_PACKET_TEMPLATE.md](/D:/Projects/CARVES.AI/CARVES.Runtime/docs/session-gateway/INTERNAL_OPERATOR_PROOF_PACKET_TEMPLATE.md)

Expected bounded posture:

- first-run packet posture is `first_run_packet_ready`
- current proof source is `repo_local_proof`
- current operator state is `WAITING_OPERATOR_SETUP`
- real-world proof is still missing

That is correct. Private alpha setup is not complete until the operator supplies real project and run evidence.

## Setup Guardrails

- Do not launch a second planner, second executor, or second review queue.
- Do not route provider credentials or repo mutation authority into the shell.
- Do not treat private alpha setup as public release enablement.
- Do not treat repo-local readiness as operator-run completion.

## Maintenance Entry Points

Use only the existing Runtime maintenance path:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 repair
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 rebuild
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 reset --derived
```
