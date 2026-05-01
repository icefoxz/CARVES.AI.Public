# CARVES.AI Public

CARVES.AI is a local AI-coding control plane for agent startup, visible gateway status, and bounded local workflow checks.

This repository is a clean public source snapshot. It does not include the private development repository history, live task/runtime truth, runtime host state, Codex state, local artifacts, or archive/checkpoint history.

## Status

Current public snapshot:

```text
0.6.2-beta source snapshot
```

This is still beta software. It is suitable for local source inspection, local build, and bounded local startup experiments. It is not a hosted service, not a signed release, and not API/SDK worker execution authority.

## Public Product Map

The public source snapshot contains these local workflow tools:

- CARVES.Guard: local diff and decision checks. Start with [docs/guard/README.md](docs/guard/README.md), [Chinese beginner guide](docs/guard/wiki/guard-beginner-guide.zh-CN.md), [English beginner guide](docs/guard/wiki/guard-beginner-guide.en.md), and [GitHub Actions guide](docs/guard/wiki/github-actions.zh-CN.md).
- Handoff: continuity packets for carrying bounded next-session context.
- Audit: local evidence discovery and summary surfaces.
- Shield: local evidence evaluation over Audit output.
- Matrix: a local AI coding workflow governance self-check that chains Guard, Handoff, Audit, and Shield into a summary-only proof bundle. See the Matrix beginner quickstart in [docs/matrix/quickstart.en.md](docs/matrix/quickstart.en.md) and [docs/matrix/quickstart.zh-CN.md](docs/matrix/quickstart.zh-CN.md).

Quick command examples:

```bash
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test demo --json
carves test demo
carves test agent
carves-guard init
carves-audit evidence --json --output .carves/shield-evidence.json
carves-shield evaluate .carves/shield-evidence.json
carves-matrix trial plan --workspace ./carves-trials/latest
carves-matrix proof --lane native-minimal --artifact-root artifacts/matrix/native --configuration Release --json
carves-matrix verify artifacts/matrix/native --json
```

For the Matrix Agent Trial beginner path, start with the source-checkout `carves test demo` path. The parallel Windows package entry is documented in [docs/matrix/agent-trial-node-windows-playable-quickstart.md](docs/matrix/agent-trial-node-windows-playable-quickstart.md).

The Linux-native Matrix first run does not require PowerShell. The full release proof remains available through:

```bash
pwsh ./scripts/matrix/matrix-proof-lane.ps1
```

CI examples live in [.github/workflows/matrix-proof.yml](.github/workflows/matrix-proof.yml). Current limits are documented in [docs/matrix/known-limitations.md](docs/matrix/known-limitations.md). Matrix is not a model safety benchmark, does not rate model safety, and does not automatically roll back arbitrary writes.

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
- Local build success is not a signing or hosted-verification claim.
- Release tags, GitHub Releases, NuGet.org publication, and package signing remain separate operator-owned steps.

## License

Apache-2.0. See [LICENSE](LICENSE).
