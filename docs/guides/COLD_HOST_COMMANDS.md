# Cold Host Commands

## Purpose

This guide defines the supported cold-command launcher for source-tree host commands when a resident host is not running or when mutable repo `obj/bin` outputs may be held by build-server processes.

## Supported Entry Point

Use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 <command> [...]
```

Examples:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 status --summary
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 inspect runtime-governance-program-reaudit
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 api operational-summary
```

## Why This Exists

Raw source-tree `dotnet run --project src/CARVES.Runtime.Host/Carves.Runtime.Host.csproj -- ...` writes through the repo's normal `obj/bin` paths before the host process starts. If a build-server process such as `VBCSCompiler` is still holding those outputs, cold `inspect/api/status` can fail before CARVES gets control.

The supported launcher avoids that mutable repo-output path by:

- building the host project into an isolated temp generation under `%TEMP%\carves-runtime-host\<repo-hash>\cold-commands\cold-build-*`
- disabling build servers for that build
- disabling shared compilation for that build
- executing the generated `Carves.Runtime.Host.dll` from that isolated generation

## Guarantees

- resident-host truth ownership is unchanged
- the launcher does not create a second control plane
- stale repo `obj/bin` locks do not automatically block cold host reads
- cleanup can prune stale cold-command generations later

## Stage 5 Trial Packaging Alignment

For Runtime Agent v1 Stage 5 trial packaging:

- `scripts/carves-host.ps1` is the supported source-tree wrapper for bounded trial start and attach entry
- its temp `cold-build-*` generations are launch artifacts, not bootstrap truth
- when it runs `attach`, bootstrap truth still lands in the target repo through existing surfaces such as `.ai/config/system.json`, `.ai/runtime.json`, and `.ai/runtime/attach-handshake.json`
- after attach, continue on the same Runtime-owned lane through `status`, `inspect runtime-first-run-operator-packet`, or later governed surfaces
- if a command truly requires a resident host, the launcher still fails closed and tells the operator to use the projected host action: normally `host start`, and `host reconcile --replace-stale --json` when a stale resident-host conflict is detected

## Limits

- this does not hide real compile errors in the current source tree
- this does not make a resident host optional for commands that explicitly require one
- this is the supported path for source-tree cold commands; raw `dotnet run --project ...` can still hit repo-output lock contention
