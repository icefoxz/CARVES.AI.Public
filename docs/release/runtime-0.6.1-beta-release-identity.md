# CARVES Runtime 0.6.1-beta Release Identity

Checkpoint id: `runtime-0.6.1-beta`

Runtime dist version: `0.6.1-beta`

Runtime CLI package version: `0.6.1-beta`

Date: 2026-05-01

Status: bounded beta packaging patch candidate for local dist / local package consumption, not public release or hosted verification readiness.

## Positioning

CARVES Runtime `0.6.1-beta` stays on the `0.6` startup and visible gateway line.

This candidate does not open a new worker execution capability. It fixes the formal beta release package boundary so the archive behaves like a release dist instead of a development checkout.

## Why This Candidate Is 0.6.1-beta

`0.6.0-beta` opened the current startup and visible gateway capability line.

`0.6.1-beta` is a same-line patch because it fixes packaging and release validation for that line:

- release dist is now the default pack profile;
- release wrappers require `runtime-cli/carves.dll` and fail clearly when it is missing;
- release inputs fail closed instead of being silently skipped;
- release output is audited for forbidden source, test, build, and control-plane state paths;
- current user-facing startup and local dist docs now point at `0.6.1-beta`.

This is why the candidate is:

```text
0.6.1-beta
```

and not:

```text
0.7.0-beta
```

## Stable Local Entry

The current bounded local posture remains:

```powershell
carves up <target-project>
carves gateway status
carves status --watch --iterations 1 --interval-ms 0
```

Inside a target project after startup, prefer the project-local launcher:

```bash
.carves/carves agent start --json
.carves/carves gateway status
```

Actual worker execution still has to pass Host policy, packet creation, evidence capture, result ingestion, and review approval. Startup readiness, gateway visibility, and local packaging do not authorize API/SDK worker execution.

## Distribution Posture

`0.6.1-beta` is a release-dist candidate:

- local `.nupkg`;
- local tool-path install/update;
- local frozen dist binding;
- generated dist folder `.dist/CARVES.Runtime-0.6.1-beta`;
- audited release dist without `src/`, tests, build residue, or Runtime live control-plane truth.

It does not itself claim:

- NuGet.org publication;
- signed packages;
- public Git tag creation;
- GitHub release creation;
- hosted verification;
- public certification;
- public leaderboard or benchmark identity.

## Release Proof

Minimum local proof for this candidate:

```powershell
dotnet build CARVES.Runtime.sln --configuration Release
dotnet pack ./src/CARVES.Runtime.Cli/carves.csproj -c Release -o <package-root>
pwsh ./scripts/pack-runtime-dist.ps1 -Version 0.6.1-beta -Force
pwsh ./scripts/assert-runtime-release-dist.ps1 -DistRoot <dist-root>
```

Required startup smoke after dist generation:

```bash
<dist-root>/carves help
<dist-root>/carves gateway status
<dist-root>/carves status --watch --iterations 1 --interval-ms 0
<dist-root>/carves pilot dist-smoke --json
```

Version/BOM source of truth:

```text
docs/release/runtime-0.6.1-beta-version-bom.md
```

## Known Limitations

- This is still a bounded beta posture.
- The visible gateway is a CLI/Host status surface, not a finished dashboard.
- BYOK/provider SDK or API-backed worker execution remains closed.
- Host long-cycle stability is still a separate hardening and operator-time decision.
- Remote publication, signing, GitHub release, tag creation, and hosted verification remain operator-gated.
