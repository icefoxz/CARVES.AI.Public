# CARVES.AI Public

CARVES.AI is a local AI-coding control plane for agent startup, visible gateway status, and bounded local workflow checks.

This repository is a clean public source snapshot. It does not include the private development repository history, live `.ai` task truth, runtime host state, Codex state, local artifacts, or archive/checkpoint history.

## Status

Current public snapshot:

```text
0.6.1-beta source snapshot
```

This is still beta software. It is suitable for local source inspection, local build, and bounded local startup experiments. It is not a hosted service, not a signed release, and not API/SDK worker execution authority.

## Requirements

- .NET SDK 10.0
- Git
- PowerShell 7+ for PowerShell scripts

## Build From Source

```bash
dotnet build CARVES.Runtime.sln --configuration Release
```

Run the CLI from source:

```bash
./carves help
./carves gateway status
```

On Windows:

```powershell
.\carves.ps1 help
.\carves.ps1 gateway status
```

## Start CARVES In A Project

The safe agent entry is:

```text
START_CARVES.md
```

Give your coding agent the absolute path to this file and ask it to read it. The agent should run:

```bash
<carves-root>/carves up <target-project>
```

Then it should open the target project and follow the generated `CARVES_START.md` / `.carves/carves agent start --json` instructions.

## What This Public Snapshot Excludes

- Private git history from the development repository.
- Live `.ai/tasks`, `.ai/runtime`, `.ai/memory`, `.ai/artifacts`, and similar control-plane truth.
- `.carves-platform` host/runtime state.
- Codex local state.
- Build outputs, logs, trials, local packages, and generated release archives.
- Internal archive/checkpoint phase history.

## Current Boundaries

- The dashboard is not a polished product surface.
- Provider-backed API/SDK worker execution is not opened by this snapshot.
- Local build success is not a signing or hosted-verification claim.
- Release tags, GitHub Releases, NuGet.org publication, and package signing remain separate operator-owned steps.

## License

Apache-2.0. See [LICENSE](LICENSE).
