# First Project Pilot Runbook

This runbook is the governed path for `CARD-242`: attach a real repo, initialize `.ai/`, push one approved card into a task, execute a first bounded run, and capture pilot evidence.

## Preconditions

- The machine host repo is available and buildable.
- The target repo is a git repo.
- You can run CARVES commands from both the host repo and the target repo.

## Source-Tree Trial Wrapper

For Stage 5 trial packaging, the bounded source-tree wrapper is:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\carves-host.ps1 <command> [...]
```

Use it when you want the supported source-tree packaged entry without turning wrapper state into bootstrap truth.
The wrapper may build an isolated temp generation, but attach and bootstrap truth still land through the existing Runtime-owned surfaces.

## 1. Start the machine host

From the machine-host repo:

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host start --interval-ms 200
```

Checkpoint:

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host status
```

## 2. Attach the target repo

From the target repo directory:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- attach
```

Checkpoint files:

- `.ai/runtime.json`
- `.ai/runtime/attach-handshake.json`

Health check:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- status --summary
```

Bounded Stage 6 readback:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- inspect runtime-first-run-operator-packet
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- inspect runtime-agent-delivery-readiness
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- inspect runtime-agent-validation-bundle
```

## 3. Create and approve the first initialization card

Prepare a payload:

```json
{
  "card_id": "CARD-900",
  "title": "Project initialization task",
  "goal": "Capture the project's purpose, goals, boundary, and proof posture before the first bounded run.",
  "acceptance": [
    "project identity is explicit",
    "project purpose and goals are explicit",
    "boundary and ownership are explicit",
    "the first taskgraph draft can be generated"
  ]
}
```

Create and approve:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- create-card-draft C:\temp\card-create.json
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- approve-card CARD-900 "pilot approved"
```

Checkpoint:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- inspect-card CARD-900
```

## 4. Create and approve the first taskgraph draft

Prepare a taskgraph payload:

```json
{
  "draft_id": "TG-CARD-900-001",
  "card_id": "CARD-900",
  "tasks": [
    {
      "task_id": "T-CARD-900-001",
      "title": "Project initialization task",
      "description": "Establish the initial governed project shape before the first bounded execution run.",
      "scope": [".ai/", "docs/"],
      "acceptance": [
        "project purpose and goals are explicit",
        "boundary and ownership are explicit",
        "run truth exists"
      ]
    }
  ]
}
```

Create and approve:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- create-taskgraph-draft C:\temp\taskgraph-draft.json
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- approve-taskgraph-draft TG-CARD-900-001 "pilot taskgraph approved"
```

Checkpoint:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- task inspect T-CARD-900-001 --runs
```

## 5. Create the first execution run

Use a bounded dry-run first:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- task run T-CARD-900-001 --dry-run
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- task inspect T-CARD-900-001 --runs
```

Checkpoint truth:

- `.ai/execution/T-CARD-900-001/task.json`
- `.ai/runtime/runs/T-CARD-900-001/RUN-....json`

## 6. Ingest a result envelope

Write `.ai/execution/T-CARD-900-001/result.json`, then ingest:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- task ingest-result T-CARD-900-001
```

Checkpoint surfaces:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- inspect replan T-CARD-900-001
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- inspect execution-memory T-CARD-900-001
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- inspect attach-proof T-CARD-900-001
```

## 7. Record pilot evidence

Prepare a feedback payload and record it:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- pilot record-evidence C:\temp\pilot-evidence.json
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- pilot list-evidence
```

Checkpoint files:

- `.ai/runtime/proofs/attach-to-task/T-CARD-900-001.json`
- `.ai/runtime/pilot-evidence/PILOT-....json`

## 8. If the runtime is dirty

Use the existing runtime repair surfaces:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- repair
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- rebuild
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- reset --derived
```

Then verify:

```powershell
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- verify runtime
dotnet run --project D:\Projects\CARVES.AI\CARVES.Runtime\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- status --summary
```

## 9. Clean shutdown

From the machine-host repo:

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host stop "pilot run complete"
```
