# CARVES Runtime 0.6.2-beta Release Identity

Checkpoint id: `runtime-0.6.2-beta`

Runtime dist version: `0.6.2-beta`

Runtime CLI package version: `0.6.2-beta`

Date: 2026-05-02

Status: public source snapshot beta candidate for local build, local startup, GitHub Actions proof, and operator-controlled GitHub pre-release publication.

## Positioning

CARVES Runtime `0.6.2-beta` stays on the `0.6` startup and visible gateway line.

This candidate does not open API/SDK worker execution, hosted verification, signing, NuGet.org publication, public leaderboard, certification, or autonomous worker authority. It stabilizes the public source snapshot and CI proof posture after packaging, Matrix, SCORE.cmd, Guard, Shield, and public docs checks were aligned.

## Why This Candidate Is 0.6.2-beta

`0.6.1-beta` fixed the local release dist packaging boundary.

`0.6.2-beta` is a same-line patch because it fixes public source snapshot release readiness for that line:

- Public GitHub Actions `ci` passes on Ubuntu and Windows.
- Public `CARVES Matrix Proof` passes across Linux and Windows lanes.
- Guard Beta proof lane detects public source snapshots and avoids private alpha/beta document readiness checks.
- Windows `SCORE.cmd` smoke passes with package-local scorer behavior.
- Public docs index and release draft now point at the current Runtime snapshot.

This is why the candidate is:

```text
0.6.2-beta
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

Actual worker execution still has to pass Host policy, packet creation, evidence capture, result ingestion, and review approval. Startup readiness, gateway visibility, CI success, and local packaging do not authorize API/SDK worker execution.

## Distribution Posture

`0.6.2-beta` is a public source snapshot and local release-dist candidate:

- source build from the public repository;
- local `.nupkg` proof packages;
- local release dist archive;
- generated dist folder `.dist/CARVES.Runtime-0.6.2-beta`;
- audited release dist without `src/`, tests, build residue, or Runtime live control-plane truth;
- GitHub pre-release candidate tag `carves-runtime-v0.6.2-beta`.

It does not itself claim:

- NuGet.org publication;
- signed packages;
- hosted verification;
- public certification;
- public leaderboard or benchmark identity;
- model safety rating;
- operating-system sandboxing.

## Release Proof

Minimum local proof for this candidate:

```powershell
dotnet build CARVES.Runtime.sln --configuration Release
pwsh ./scripts/release/github-publish-readiness.ps1 -ArtifactRoot artifacts/release/github-publish-readiness
pwsh ./scripts/pack-runtime-dist.ps1 -Version 0.6.2-beta -Force
pwsh ./scripts/assert-runtime-release-dist.ps1 -DistRoot .dist/CARVES.Runtime-0.6.2-beta
```

Required hosted evidence before publication:

```text
ci success on the release commit
CARVES Matrix Proof success on the release commit
```

Version/BOM source of truth:

```text
docs/release/runtime-0.6.2-beta-version-bom.md
```

## Known Limitations

- This is still a bounded beta posture.
- The visible gateway is a CLI/Host status surface, not a finished dashboard.
- BYOK/provider SDK or API-backed worker execution remains closed.
- Host long-cycle stability is still a separate hardening and operator-time decision.
- Remote NuGet.org publication, signing, and hosted verification remain operator-gated.
