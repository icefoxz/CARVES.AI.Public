# CARVES Frozen Dist Target Readback Proof

Use this guide when the operator needs to prove an external target repo is consuming CARVES Runtime from the frozen local alpha dist instead of the live Runtime source tree.

Phase 24 hardens the wrapper binding path used by this proof: the dist wrapper passes its own Runtime root into `init . --json`, so a target repo is not accidentally attached to itself.

## Command

```powershell
carves pilot target-proof --json
```

Alias:

```powershell
carves pilot external-proof --json
```

Inspect/API forms:

```powershell
carves inspect runtime-frozen-dist-target-readback-proof
carves api runtime-frozen-dist-target-readback-proof
```

## What It Proves

The proof is complete only when all of these are true:

- the current repo is initialized as a CARVES target
- `.ai/AGENT_BOOTSTRAP.md` and root `AGENTS.md` are present
- invocation and activation contracts are valid
- the local dist freshness smoke is ready
- the target dist binding plan says the target is bound to the local dist
- local dist handoff says `stable_external_consumption_ready=true`
- the current repo is a git repository

This is narrower than final product pilot proof. It proves the external target can consume the frozen Runtime package; it does not prove target feature work, target commit closure, push, release, or public distribution.

## Normal Flow

From the Runtime source repo:

```powershell
carves pilot dist-smoke --json
carves pilot dist-binding --json
```

From the external target repo:

```powershell
& "<LocalDistRoot>\carves.ps1" init . --json
& "<LocalDistRoot>\carves.ps1" pilot invocation --json
& "<LocalDistRoot>\carves.ps1" pilot activation --json
& "<LocalDistRoot>\carves.ps1" pilot dist-smoke --json
& "<LocalDistRoot>\carves.ps1" pilot dist-binding --json
& "<LocalDistRoot>\carves.ps1" pilot dist --json
& "<LocalDistRoot>\carves.ps1" pilot target-proof --json
```

If the target proof is complete, an external agent may treat CARVES as a stable frozen-dist governance source for planning guidance and readbacks. The agent still must follow `carves pilot status --json` before planning or editing.

After `init`, both `.ai/runtime.json` and `.ai/runtime/attach-handshake.json` should record `<LocalDistRoot>` as `runtime_root`.

## Failure Interpretation

- `runtime_not_initialized`: run the frozen dist wrapper `init . --json` from the target repo.
- `target_agent_bootstrap_not_ready`: run `carves agent bootstrap --write`.
- `local_dist_freshness_smoke_not_ready`: refresh or verify the Runtime dist from the source repo.
- `target_not_bound_to_frozen_local_dist`: use `carves pilot dist-binding --json` and follow the operator-owned wrapper command.
- `stable_external_consumption_not_ready`: run `carves pilot dist --json` from the target repo and follow its next action.
- `target_git_repository_not_detected`: run from the external target git root.

## Non-Claims

This guide does not authorize agents to edit attach manifests, shell profiles, PATH, global aliases, Runtime docs, target task truth, or target product files. It also does not claim public package distribution, signed installers, OS sandboxing, full ACP, full MCP, or remote worker orchestration.
