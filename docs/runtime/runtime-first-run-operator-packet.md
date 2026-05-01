# Runtime First-Run Operator Packet

## Purpose

This document freezes the Runtime-owned packet that should guide first entry into CARVES for a real project.

The packet exists so internal beta entry does not jump directly from a readable gate to open-ended operator interpretation. It keeps bootstrap truth, onboarding acceleration, and proof posture on one bounded entry lane.

## Core Claim

A real project should have one bounded first-run packet that says:

- what the operator must provide
- what Runtime may infer
- what AI may accelerate
- what evidence is still required

Current Runtime-owned projection:

- `inspect runtime-first-run-operator-packet`
- `api runtime-first-run-operator-packet`

Current bounded posture:

- `overall_posture = first_run_packet_ready`
- `current_proof_source = repo_local_proof`
- `current_operator_state = WAITING_OPERATOR_SETUP`

That posture means the packet is ready to guide first-run operator entry on the existing Runtime lane.

It does not mean bootstrap, onboarding, or proof are complete.

The internal beta gate posture is surfaced inside the packet, but operator-proof waiting states do not by themselves make this packet unreadable. Missing packet documents or broken packet inputs still block the packet.

## First-Run Packet Shape

At minimum, the packet should answer:

- project identification
- bootstrap inputs
- required operator actions
- allowed AI assistance
- exit criteria

The current bounded packet also ties together:

- `internal-beta-gate.md`
- `runtime-trusted-bootstrap-truth-schema.md`
- `runtime-onboarding-acceleration-contract.md`
- [ALPHA_SETUP.md](/docs/session-gateway/ALPHA_SETUP.md)
- [ALPHA_QUICKSTART.md](/docs/session-gateway/ALPHA_QUICKSTART.md)

## Entry Commands

Use the packet as the first bounded readback:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-first-run-operator-packet
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-session-gateway-internal-beta-gate
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-session-gateway-private-alpha-handoff
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-session-gateway-repeatability
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 host status
```

Read those on the same Runtime-owned lane before claiming a real first-run is bounded and aligned.

For project attach and first-run bootstrap entry:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 attach <repo-root> --repo-id <repo-id>
```

Attach may be requested from gateway or frontend surfaces, but bootstrap writeback remains Host-routed.

## Runtime Agent v1 Stage 5 Alignment

For Runtime Agent v1 Stage 5 startup and attach trial path:

- resident Host startup and `attach` remain the bounded entry lane before this packet is read as a real first-run bundle
- this packet is the next Runtime-owned bootstrap readback after attach, not a replacement for attach or a second initialization wizard
- attach may prepare repo-local runtime truth and readiness projection, but bootstrap truth still stays on the existing Runtime-owned lane
- friendly CLI or other bridge surfaces may point operators here, but they do not own packet truth or bootstrap writeback

For Runtime Agent v1 Stage 5 trial packaging and governed bootstrap flow:

- source-tree wrapper paths such as `scripts/carves-host.ps1` may materialize isolated launch builds, but those builds are not trusted bootstrap truth
- trial packaging still lands on the same Host command surface and the same attach/bootstrap truth files
- the packet remains the post-attach readback, not installer-side state or packaging-owned onboarding logic

For Runtime Agent v1 Stage 5 first-run guidance and minimum onboarding:

- the minimum repo-local bootstrap bundle remains `README.md -> AGENTS.md -> inspect runtime-first-run-operator-packet`
- attach/bootstrap writeback and first guided project setup stay on the same Runtime-owned lane after that bundle is read
- friendly CLI, Workbench, and shell entry may point operators to this bundle, but they do not own bootstrap truth or hidden onboarding state

## Guided Planning Alignment

After the minimum onboarding bundle is read:

- a guided planning surface may help the operator stabilize project purpose, boundary, validation artifact, and first slice through Runtime-owned planning truth
- guided planning may project candidate cards or pending decisions before the first initialization card is frozen
- the first official initialization card still enters through the existing Host-routed card path rather than UI-local planning state
- guided planning accelerates clarification; it does not replace attach, bootstrap truth, or Host-owned writeback

The bounded Stage 5 anchor is:

- `runtime-agent-governed-first-run-guidance-minimum-onboarding-contract.md`

## Default First-Run Flow

The first-run operator packet should normally look like:

1. identify project and entry source
2. establish trusted bootstrap truth
3. capture project purpose, goals, boundary, and proof posture in the initialization card
4. review import/config mapping
5. confirm required operator actions
6. allow bounded onboarding acceleration
7. stop or continue according to proof posture

## Minimum Onboarding Bundle

The minimum onboarding bundle for a friend or trial operator is:

1. `README.md`
2. `AGENTS.md`
3. `inspect runtime-first-run-operator-packet`

After that bundle is read, the next bounded actions are:

1. keep attach/bootstrap writeback on the existing Runtime-owned lane
2. capture project purpose, goals, boundary, and proof posture through the first initialization card
3. continue through existing Host-routed taskgraph and execution surfaces instead of hidden onboarding state

## Stage 0 ingress boundary alignment

For Runtime Agent v1 Stage 0:

- frontends and gateway may collect and submit first-run inputs
- first-run truth mutation is valid only through Host-routed commands
- attach and initialization writeback are not frontend-owned lifecycle actions

Host-routed commands remain the write path for first-run mutation:

- `attach`
- `create-card-draft`
- `approve-card`
- `create-taskgraph-draft`
- `approve-taskgraph-draft`
- `review-task`
- `sync-state`

## Allowed AI Assistance

On this bounded lane, AI may help by:

- summarizing the current project shape after trusted bootstrap truth is explicit
- suggesting initial tasks or next questions on the same Runtime-owned lane
- proposing proof obligations and evidence gaps without self-certifying completion
- assembling the bounded first-run reading bundle and entry commands

## Initialization Task Shape

The first governed task after bootstrap truth is established should normally be a project initialization task.

It should capture:

- project identity
- project purpose
- project goals or success criteria
- boundary and ownership
- required operator inputs and missing facts
- initial working assumptions
- proof posture

It should usually output:

- a first approved initialization card
- a taskgraph draft derived from that card
- the next bounded questions or tasks

This task is the bridge from bootstrap truth into later onboarding acceleration.
It is not a substitute for trusted bootstrap truth itself.

## Non-Claims

This packet does **not** claim:

- that `carves init` is implemented
- that `init --import` or `init --config` are implemented
- that `onboard` can establish trusted bootstrap truth by itself
- that operator-proof obligations disappear after reading the packet
- that Runtime has opened a second onboarding root, second control plane, or client-owned truth lane
