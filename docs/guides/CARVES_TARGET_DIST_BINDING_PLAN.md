# CARVES Target Dist Binding Plan

This guide records the Phase 21 target dist binding plan.

Phase 22 adds `carves pilot dist-smoke --json` as the read-only freshness proof that should pass before this binding plan is used for stable external-project claims. Phase 23 adds `carves pilot target-proof --json` as the read-only proof that the current target is initialized, bootstrapped, and bound to the fresh frozen dist. Phase 24 hardens the dist wrapper so `init . --json` binds the target to the wrapper Runtime root instead of accidentally binding the target to itself.

## Commands

```powershell
carves pilot dist-binding
carves pilot dist-binding --json
carves pilot bind-dist
carves pilot bind-dist --json
carves pilot dist-smoke
carves pilot dist-smoke --json
carves pilot target-proof
carves pilot target-proof --json
```

Equivalent inspect/API surfaces:

```powershell
carves inspect runtime-target-dist-binding-plan
carves api runtime-target-dist-binding-plan
carves inspect runtime-local-dist-freshness-smoke
carves api runtime-local-dist-freshness-smoke
carves inspect runtime-frozen-dist-target-readback-proof
carves api runtime-frozen-dist-target-readback-proof
```

## What This Solves

External target repos can be attached to the live Runtime source tree during dogfood. That is useful while CARVES.Runtime is being edited, but it is not the stable alpha external-project posture.

For stable external use, target repos should be attached to a frozen local dist such as:

```text
D:\Projects\CARVES.AI\.dist\CARVES.Runtime-0.6.1-beta
```

For WSL-local work, prefer the matching WSL filesystem path and wrapper:

```text
<dist-root>/carves
```

The WSL dist wrapper uses `runtime-cli/carves.dll` when present, so external-project commands avoid repeated source-tree `dotnet run --project` startup.

The binding plan tells the operator whether that dist exists and what commands should be run from the target repo to bind or verify it.

## Required Readback Order

From the target repo:

```powershell
carves pilot invocation --json
carves pilot activation --json
carves pilot dist-smoke --json
carves pilot dist-binding --json
carves pilot dist --json
carves pilot target-proof --json
carves pilot status --json
```

If no trusted `carves` alias exists yet, run the same commands through the absolute dist wrapper shown in `operator_binding_commands`.

## Expected Stable Outcome

```text
target_bound_to_local_dist=true
stable_external_consumption_ready=true
runtime_root_kind=local_dist
```

## Operator Retarget Pattern

When the binding plan reports `target_bound_to_live_source=true`, the operator may choose to run the projected dist wrapper command from the target repo:

```powershell
& "<LocalDistRoot>\carves.ps1" init . --json
```

The wrapper records `<LocalDistRoot>` as the Runtime authority for the child CLI process. A successful retarget must leave `.ai/runtime.json` and `.ai/runtime/attach-handshake.json` pointing at `<LocalDistRoot>`.

Then rerun:

```powershell
& "<LocalDistRoot>\carves.ps1" pilot dist-binding --json
& "<LocalDistRoot>\carves.ps1" pilot dist --json
& "<LocalDistRoot>\carves.ps1" pilot target-proof --json
```

On WSL, use:

```bash
"<LocalDistRoot>/carves" init . --json
"<LocalDistRoot>/carves" host ensure --json
"<LocalDistRoot>/carves" pilot dist-binding --json
"<LocalDistRoot>/carves" pilot dist --json
"<LocalDistRoot>/carves" pilot target-proof --json
```

## Agent Rules

- Do not edit `.ai/runtime.json` manually.
- Do not edit `.ai/runtime/attach-handshake.json` manually.
- Do not mutate PATH, shell profile, aliases, or tool installation as project work.
- Do not claim frozen dist freshness unless `carves pilot dist-smoke --json` reports ready.
- Do not claim stable external target consumption unless `carves pilot target-proof --json` reports `frozen_dist_target_readback_proof_complete=true`.
- Do not claim product pilot proof until `pilot proof --json` is complete.
- Do not confuse live source-tree dogfood with frozen dist external consumption.

## Non-Claims

This guide does not create a dist, refresh a dist, mutate target runtime bindings, install a tool, publish a package, or perform commits.
