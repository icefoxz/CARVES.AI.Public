# CARVES CLI Invocation Contract

This guide records the Phase 19 CLI invocation contract.

Use it when an operator or external agent needs to decide which `carves` command path is authoritative before planning or editing a target repo.

## Command

```powershell
carves pilot invocation
carves pilot invocation --json
```

Equivalent inspect/API surfaces:

```powershell
carves inspect runtime-cli-invocation-contract
carves api runtime-cli-invocation-contract
```

## Rule

Do not assume a global `carves` alias is authoritative until `carves pilot invocation --json` confirms the intended Runtime root and invocation lane.

If the target bootstrap records an absolute wrapper path, prefer that path:

```powershell
& "<RuntimeRoot>\carves.ps1" pilot invocation --json
```

## Lanes

### Source-Tree Wrapper

```powershell
& "<RuntimeSourceRoot>\carves.ps1" pilot status --json
```

Use for Runtime development and controlled dogfood. This is not the stable external-project baseline.

### Local Dist Wrapper

```powershell
& "<RuntimeDistRoot>\carves.ps1" pilot status --json
```

Use for attached external projects during the alpha line. The dist root should include `MANIFEST.json`, `VERSION`, `carves.ps1`, and the Runtime docs.

### CMD Shim

```powershell
"<RuntimeRoot>\carves.cmd" pilot status --json
```

Use only for Windows shell or tooling compatibility. It must route to the same Runtime authority as the PowerShell wrapper.

### Future Global Alias

```powershell
carves pilot status --json
```

Use only after the operator has configured the alias and the invocation readback confirms the expected Runtime root.

## Required Readbacks

```powershell
carves pilot invocation --json
carves pilot resources --json
carves agent handoff --json
carves pilot status --json
carves pilot guide --json
```

## Boundary

- This guide is Runtime-owned and read from the Runtime document root.
- It does not mutate target truth.
- It does not initialize, plan, issue workspaces, approve review, write back files, stage, commit, push, pack, or publish.
- It does not make `dotnet run --project ...` the external-agent baseline.
- It does not replace CARVES planning, workspace, review, or writeback gates.
