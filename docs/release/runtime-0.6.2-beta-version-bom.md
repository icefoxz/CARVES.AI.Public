# CARVES Runtime 0.6.2-beta Version BOM

Status: `0.6.2-beta` public source snapshot candidate BOM.

This file defines the current Runtime public source snapshot, local package proof, and local dist identity. It does not create a tag, publish a GitHub release, push NuGet.org packages, sign packages, or bypass operator gates.

Current release identity entry:

```text
docs/release/runtime-0.6.2-beta-release-identity.md
```

## Conclusion

The current Runtime candidate is:

```text
CARVES.Runtime 0.6.2-beta
```

Plain meaning:

```text
This is a patch on the 0.6 startup and visible gateway line.
The prerelease channel is still beta.
The patch fixes public source snapshot CI, Matrix proof, SCORE.cmd smoke, Guard proof lane scope, and release draft alignment.
This is still not a stable release, NuGet.org publication, signed package, hosted verification, certification claim, or worker execution authority.
```

## Why 0.6.2-beta

This is not a `0.7.0-beta` capability release.

The externally meaningful capability line remains the `0.6` line:

- `carves up <target-project>`;
- project-local `.carves/carves`;
- agent-readable startup files;
- existing-project handling;
- visible gateway/status feedback;
- local Guard / Handoff / Audit / Shield / Matrix self-check behavior.

The change is public snapshot release readiness:

- Public `ci` is green on Ubuntu and Windows.
- Public `CARVES Matrix Proof` is green on Ubuntu and Windows.
- Public Guard Beta proof lane no longer depends on private alpha/beta docs.
- Windows playable `SCORE.cmd` smoke passes against package-local scorer behavior.
- Release draft and docs index now point at the Runtime `0.6.2-beta` snapshot instead of the older Matrix RC draft.

Under [runtime-versioning-policy.md](runtime-versioning-policy.md), `patch` is for fixes to validation, packaging, and docs within the same capability line. That makes the correct jump:

```text
0.6.1-beta -> 0.6.2-beta
```

## Version Dimensions

| Dimension | Candidate value | Notes |
| --- | --- | --- |
| Runtime public snapshot version | `0.6.2-beta` | Source snapshot version shown in README. |
| Runtime dist version | `0.6.2-beta` | Local release dist archive candidate. |
| Runtime CLI package version | `0.6.2-beta` | `carves` carries the current startup, visible gateway, public CI, and release-readiness patch. |
| Runtime dist folder | `.dist/CARVES.Runtime-0.6.2-beta` | Generated local release dist; not committed to source. |
| Git tag candidate | `carves-runtime-v0.6.2-beta` | Operator candidate only until tag creation is explicitly performed. |
| Release checkpoint / BOM | `docs/release/runtime-0.6.2-beta-version-bom.md` | This file. |
| Runtime release identity | `docs/release/runtime-0.6.2-beta-release-identity.md` | Current candidate identity. |
| GitHub Actions evidence | `ci` and `CARVES Matrix Proof` on the release commit | Must be confirmed from GitHub Actions. |

## Package Version Plan

This candidate upgrades only the Runtime shell package version.

| Package / Component | Previous Runtime line | Candidate version | Decision |
| --- | --- | --- | --- |
| `CARVES.Runtime.Cli` | `0.6.1-beta` | `0.6.2-beta` | Upgrade. Runtime public snapshot and release-readiness posture changed. |
| `CARVES.Guard.Core` | `0.2.0-beta.1` | `0.2.0-beta.1` | Keep. Guard is not the upgraded Runtime shell line. |
| `CARVES.Guard.Cli` | `0.2.0-beta.1` | `0.2.0-beta.1` | Keep. |
| `CARVES.Handoff.Core` | `0.1.0-alpha.1` | `0.1.0-alpha.1` | Keep. |
| `CARVES.Handoff.Cli` | `0.1.0-alpha.1` | `0.1.0-alpha.1` | Keep. |
| `CARVES.Audit.Core` | `0.1.0-alpha.1` | `0.1.0-alpha.1` | Keep. |
| `CARVES.Audit.Cli` | `0.1.0-alpha.1` | `0.1.0-alpha.1` | Keep. |
| `CARVES.Shield.Core` | `0.1.0-alpha.1` | `0.1.0-alpha.1` | Keep. |
| `CARVES.Shield.Cli` | `0.1.0-alpha.1` | `0.1.0-alpha.1` | Keep. |
| `CARVES.Matrix.Core` | `0.2.0-alpha.1` | `0.2.0-alpha.1` | Keep. |
| `CARVES.Matrix.Cli` | `0.2.0-alpha.1` | `0.2.0-alpha.1` | Keep. |

## Minimum Local Proof

```powershell
dotnet build CARVES.Runtime.sln --configuration Release
git diff --check
pwsh ./scripts/release/github-publish-readiness.ps1 -ArtifactRoot artifacts/release/github-publish-readiness
pwsh ./scripts/pack-runtime-dist.ps1 -Version 0.6.2-beta -Force
pwsh ./scripts/assert-runtime-release-dist.ps1 -DistRoot .dist/CARVES.Runtime-0.6.2-beta
```

Full long-cycle Host stability, hosted verification, NuGet.org publication, and package signing remain separate operator gates.

## Non-Claims

`0.6.2-beta` does not claim:

- stable release status;
- remote package publication;
- signed packages;
- hosted verification;
- certification, leaderboard, or benchmark status;
- finished dashboard readiness;
- OS sandboxing;
- provider SDK/API-backed worker execution;
- arbitrary BYOK worker activation;
- semantic source-code correctness proof.
