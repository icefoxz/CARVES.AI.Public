# CARVES Runtime

Language: [中文](README.zh-CN.md)

CARVES Runtime is a local AI Agent governance runtime for starting, binding, inspecting, and checking agent work in a project. It is a local AI coding workflow governance self-check for Guard, Handoff, Audit, Shield, and Matrix behavior.

This repository is a clean public source snapshot. It does not include the private development repository history, live task/runtime truth, runtime host state, Codex state, local artifacts, or archive/checkpoint history.

## Status

Current public snapshot:

```text
0.6.2-beta source snapshot
```

This is still beta software. It is suitable for local source inspection, local build, and bounded local startup experiments. It is not a hosted service, not a signed release, not a complete autonomous agent platform, and not API/SDK worker execution authority.

## Runtime First Run

Build the Runtime CLI from source:

```bash
dotnet build CARVES.Runtime.sln --configuration Release
```

Check the local CLI and visible gateway surface:

```bash
./carves help
./carves gateway status
```

Bind CARVES Runtime to a target project:

```bash
./carves up <target-project>
```

The safe agent entry is [START_CARVES.md](START_CARVES.md). Give your coding agent the absolute path to that file, then follow the generated `CARVES_START.md` / `.carves/carves agent start --json` instructions in the target project.

## Runtime Governance Capabilities

This source snapshot includes these Runtime governance capabilities:

- Guard: Runtime patch boundary checks for local diffs. Start with [docs/guard/README.md](docs/guard/README.md), [beginner guide](docs/guard/wiki/guard-beginner-guide.en.md), and [GitHub Actions guide](docs/guard/wiki/github-actions.en.md).
- Handoff: Runtime session continuity packets for carrying bounded next-session context.
- Audit: Runtime evidence readback over local Guard decisions and Handoff packets.
- Shield: Runtime local governance self-check over summary evidence.
- Matrix: Runtime proof and trial lanes that check Guard, Handoff, Audit, and Shield can work together without changing the Runtime-first position.

The documentation index starts at [docs/INDEX.md](docs/INDEX.md).

Capability command examples:

```bash
carves test demo --json
carves test agent
carves-guard init
carves-audit evidence --json --output .carves/shield-evidence.json
carves-shield evaluate .carves/shield-evidence.json
carves-matrix trial plan --workspace ./carves-trials/latest
carves-matrix proof --lane native-minimal --artifact-root artifacts/matrix/native --configuration Release --json
carves-matrix verify artifacts/matrix/native --json
```

`carves test demo` is a local smoke/trial path for the Runtime CLI. It is not the first Runtime entry. The parallel Windows package entry is documented in [docs/matrix/agent-trial-node-windows-playable-quickstart.md](docs/matrix/agent-trial-node-windows-playable-quickstart.md).

The Linux-native Matrix first run does not require PowerShell. The full release proof remains available through:

```bash
pwsh ./scripts/matrix/matrix-proof-lane.ps1
```

CI examples live in [.github/workflows/matrix-proof.yml](.github/workflows/matrix-proof.yml). Current limits are documented in [docs/matrix/known-limitations.md](docs/matrix/known-limitations.md). Matrix is not a model safety benchmark, does not rate model safety, and does not automatically roll back arbitrary writes.

Matrix proof artifacts are summary-only local evidence. Start with the [Matrix beginner quickstart](docs/matrix/quickstart.en.md) before using the full release path.

## Disclaimer

CARVES.AI is provided as-is. You are responsible for reviewing, approving, and validating any action taken with or through CARVES.AI. Do not use it on sensitive, production, regulated, confidential, customer-owned, or business-critical systems without your own authorization, backups, security controls, and recovery plan.

See [DISCLAIMER.md](DISCLAIMER.md).

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
- Live task, runtime, memory, artifact, and similar control-plane truth.
- `.carves-platform` host/runtime state.
- Codex local state.
- Build outputs, logs, trials, local packages, and generated release archives.
- Internal archive/checkpoint phase history.

## Current Boundaries

- The dashboard is not a polished product surface.
- Provider-backed API/SDK worker execution is not opened by this snapshot.
- Internal Runtime planning and Host truth details are maintainer concepts, not a requirement for the first public run.
- Local build success is not a signing or hosted-verification claim.
- Release tags, GitHub Releases, NuGet.org publication, and package signing remain separate operator-owned steps.

## License

Apache-2.0. See [LICENSE](LICENSE).
