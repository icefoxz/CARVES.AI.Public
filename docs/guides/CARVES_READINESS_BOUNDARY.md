# CARVES Readiness Boundary

This guide records the Phase 2 product-closure readiness separation, Phase 3 first-run init boundary, and Phase 4 external target dogfood boundary.

CARVES now separates three readiness questions that were previously easy to confuse:

1. Is the `carves` CLI entry available?
2. Is the current or specified target repo a CARVES-managed repo?
3. Is the resident CARVES host running?

Use:

```powershell
carves init [path]
carves doctor
carves doctor --json
```

## Readiness Layers

| Layer | Meaning | Primary command |
| --- | --- | --- |
| `tool` | The CLI wrapper/package can run and print guidance. | `carves doctor` |
| `target_repo` | The current or `--repo-root` project is discoverable and has Runtime bootstrap files. | `carves status` |
| `host` | The resident CARVES host is running and can serve host-required operations. | `carves host status` |
| `agent_handoff` | External agents can read the governed handoff contract. | `carves agent handoff` |
| `first_run` | An existing target repo can be attached through the Runtime-owned onboarding lane. | `carves init [path]` |
| `runtime_document_root` | Attached target readbacks can find Runtime-owned doctrine without copying docs into the target repo. | `carves agent handoff --json` |

## Operator Rule

Use `doctor` when the question is:

```text
Is this CARVES setup ready enough to operate?
```

Use `status` when the question is:

```text
What is the current target repo state?
```

Use `agent handoff` when the question is:

```text
What should an external agent obey before planning or editing?
```

Use `init` when the question is:

```text
Can this existing target repo be attached on the governed first-run lane?
```

## Expected Doctor States

No target repo:

```text
Tool readiness: available
Target repo: not_found
Runtime readiness: not_checked
Host readiness: not_checked
Next: run git init or pass --repo-root <path>
```

Target repo exists but is not attached:

```text
Tool readiness: available
Target repo: found
Target repo readiness: missing_runtime
Runtime readiness: missing
Host readiness: not_running
Next: carves host start
```

Target repo attached but host stopped:

```text
Tool readiness: available
Target repo readiness: runtime_initialized
Host readiness: not_running
Next: carves host start
```

Ready enough for normal operation:

```text
Tool readiness: available
Target repo readiness: runtime_initialized
Host readiness: connected
Next: carves status
```

## Init Boundary

Phase 3 adds `carves init [path] [--json]` as a minimal first-run attach wrapper.

`init` still depends on the target repo boundary and resident host:

- if the path is missing or not a repo/workspace, `init` reports `Action: no_changes`
- if the resident host is stopped, `init` reports `Action: no_changes`
- if host attach succeeds, `init` reports `initialized_runtime` or `attached_existing_runtime`

`init` does not create goals, cards, tasks, or acceptance contracts. It points durable work to `carves plan init [candidate-card-id]`.

## External Target Dogfood Boundary

Phase 4 proves that an attached external target repo can run:

```powershell
carves init . --json
carves doctor --json
carves agent handoff --json
carves inspect runtime-first-run-operator-packet
carves intent draft
carves plan init [candidate-card-id]
```

When the current repo is an attached target, Runtime-owned surfaces should project a `runtime_document_root_mode` of `attach_handshake_runtime_root` or `runtime_manifest_root`. That means Runtime docs are read from the Runtime repo while target repo planning and task truth stay local to the target repo.

## Non-Claims

Phase 4 does not claim public package distribution, a project-creation wizard, full external-user onboarding polish, managed workspace lease hardening beyond existing audit/preflight surfaces, OS/process sandboxing, or full ACP/MCP transports.
