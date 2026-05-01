# CARVES Init First Run

This guide records the Phase 3 first-run onboarding entry.

`carves init` is a thin operator-facing wrapper around the existing Runtime attach flow. It makes the first run explicit without creating a second planner, second task graph, or hidden onboarding truth.

Product note: `carves init` is no longer the recommended new-user first command.
Use `carves up <project>` for the product startup path. `init` remains a
lower-level attach/init primitive used by `carves up` and by diagnostics.

## Command

```powershell
carves init [path]
carves init [path] --json
```

If `path` is omitted, the current directory is used.

`--repo-root <path>` remains a global override and takes precedence over the positional path.

## What Init Does

`carves init`:

1. Resolves the target path to an existing git/workspace repo.
2. Checks whether a resident host with `attach-flow` capability is reachable.
3. Reuses the existing host-owned `attach` command to materialize repo-local Runtime bootstrap truth.
4. Records the Runtime source root in the attach handshake and runtime manifest.
5. Prints the first-run next commands after attach succeeds.

## What Init Does Not Do

`carves init` does not:

- create a new project directory
- run `git init`
- create business goals, cards, tasks, or taskgraph truth
- synthesize acceptance contracts
- bypass one-active-planning-card governance
- introduce a second control plane

Formal planning still starts later through:

```powershell
carves plan init [candidate-card-id]
```

## Expected States

Missing target path:

```text
Target repo: not_found
Action: no_changes
Next: run git init or pass --repo-root <path>
```

Existing folder that is not a repo/workspace:

```text
Target repo: not_repository_workspace
Action: no_changes
Next: run git init or pass --repo-root <path>
```

Target repo found but resident host stopped:

```text
Target repo: found
Host readiness: not_running
Action: no_changes
Next: carves host start
```

Target repo found and resident host connected:

```text
Target repo: found
Host readiness: connected
Action: initialized_runtime
Next: carves doctor
```

If Runtime was already initialized, the action is `attached_existing_runtime`.

## First-Run Command Sequence

After a successful init:

```powershell
carves doctor
carves agent handoff
carves inspect runtime-first-run-operator-packet
carves plan init [candidate-card-id]
```

This keeps onboarding, handoff guidance, first-run packet reading, and formal planning on the existing Runtime-owned lane.

## Runtime Document Root

An attached target repo does not need to contain Runtime docs.

After successful init, Runtime-owned readbacks can resolve doctrine and proof documents from the Runtime source root recorded in:

- `.ai/runtime/attach-handshake.json`
- `.ai/runtime.json`

Expected attached-target values are:

- `runtime_document_root_mode=attach_handshake_runtime_root` when the attach handshake is available.
- `runtime_document_root_mode=runtime_manifest_root` when the manifest provides the fallback.

The target repo still owns its local planning card, task truth, acceptance evidence, and project files.

## JSON Contract

`carves init --json` returns:

- `schema_version`
- `tool_readiness`
- `command_entry`
- `cli_version`
- `working_directory`
- `target_path`
- `target_repo`
- `target_repo_path`
- `target_repo_readiness`
- `runtime_readiness_before`
- `runtime_readiness_after`
- `host_readiness`
- `action`
- `next_action`
- `is_initialized`
- `gaps`
- `commands`

## Relationship To Doctor And Status

Use `init` when the question is:

```text
Attach or confirm this target repo on the governed first-run lane.
```

Use `doctor` when the question is:

```text
Which readiness layer is blocking operation?
```

Use `status` when the question is:

```text
What is the current target repo state?
```
