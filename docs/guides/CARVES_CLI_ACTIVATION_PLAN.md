# CARVES CLI Activation Plan

This guide records the Phase 20 CLI activation plan.

Use it after `carves pilot invocation --json` has identified the authoritative Runtime root and before assuming a short `carves` command is safe to use.

## Command

```powershell
carves pilot activation
carves pilot activation --json
carves pilot alias
carves pilot alias --json
```

Equivalent inspect/API surfaces:

```powershell
carves inspect runtime-cli-activation-plan
carves api runtime-cli-activation-plan
```

## Rule

Activation is convenience, not authority.

If the target bootstrap records an absolute wrapper path, that path remains the safest baseline:

```powershell
& "<RuntimeRoot>\carves.ps1" pilot activation --json
```

Only the operator should decide whether to persist PATH, profile, or tool changes.

## Recommended Order

```powershell
carves pilot invocation --json
carves pilot activation --json
carves pilot resources --json
carves pilot status --json
```

If `carves` itself is not trusted yet, run those commands through the absolute wrapper path.

## Activation Options

### Absolute Wrapper

```powershell
& "<RuntimeRoot>\carves.ps1" pilot status --json
```

No environment mutation. Best for generic agents.

### Session Alias

```powershell
Set-Alias carves "<RuntimeRoot>\carves.ps1"
```

Current PowerShell session only. Do not treat this as durable project truth.

### PATH Entry

```powershell
$env:Path = "<RuntimeRoot>;$env:Path"
```

Useful for frozen local dist roots. Persistent PATH changes are operator-owned.

### CMD Shim

```powershell
"<RuntimeRoot>\carves.cmd" pilot status --json
```

Useful for Windows tools that cannot invoke PowerShell scripts directly.

### Optional .NET Tool

```powershell
dotnet tool install --global CARVES.Runtime.Cli --add-source <package-root> --version 0.6.1-beta
```

Optional packaging lane only. It does not replace `pilot invocation`, `pilot activation`, or Runtime-root proof.

## Non-Claims

This guide does not mutate PATH, edit profiles, install tools, initialize a repo, plan, issue workspaces, approve review, write back files, stage, commit, push, pack, or publish.
