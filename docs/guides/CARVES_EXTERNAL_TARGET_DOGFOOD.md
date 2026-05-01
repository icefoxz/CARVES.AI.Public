# CARVES External Target Dogfood

This guide records the Phase 4 external target repo dogfood path.

Use it when the target repo is not `CARVES.Runtime`, but you want CARVES to attach, explain governance, and enter formal planning from that target directory.

## Prerequisites

- The Runtime repo is available locally.
- The target path is an existing git/workspace repo.
- The resident Runtime host is running and exposes the attach flow.
- The `carves` command is available through the source-tree wrapper, local tool install, or `PATH`.

## Command Sequence

From the Runtime repo, start the host if needed:

```powershell
carves host start
```

From the target repo:

```powershell
carves init . --json
carves doctor --json
carves agent handoff --json
carves inspect runtime-first-run-operator-packet
```

Then enter formal planning:

```powershell
carves intent draft
carves intent focus candidate-first-slice
carves intent decision first_validation_artifact resolved
carves intent decision first_slice_boundary resolved
carves intent candidate candidate-first-slice ready_to_plan
carves plan init candidate-first-slice
carves plan status
```

## Runtime Document Root

An attached target repo should not copy Runtime docs into itself.

After `carves init`, CARVES records the Runtime source root in:

- `.ai/runtime/attach-handshake.json`
- `.ai/runtime.json`

Runtime-owned surfaces use those records to resolve doctrine and proof documents from the Runtime document root.

Expected attached-target readbacks:

- `runtime_document_root_mode=attach_handshake_runtime_root` when the attach handshake is present.
- `runtime_document_root_mode=runtime_manifest_root` when the manifest provides the fallback.
- `runtime_document_root` points at the Runtime repo, not the target repo.

## Operator Rule

Use:

- `carves init` to attach an existing repo.
- `carves doctor` to separate tool, target, and host readiness.
- `carves agent handoff` to read the agent governance rail.
- `carves inspect runtime-first-run-operator-packet` to read first-run guidance.
- `carves intent ...` and `carves plan init ...` to enter formal planning.

Do not treat `init` or `agent handoff` as approval to edit arbitrary target files. Durable work still needs a planning card, acceptance contract, task-bound execution, review evidence, and host writeback.

## Non-Claims

This guide does not claim:

- target project creation
- automatic product-goal synthesis
- public package distribution
- full external-user onboarding polish
- OS/process sandboxing
- complete ACP/MCP transport support
