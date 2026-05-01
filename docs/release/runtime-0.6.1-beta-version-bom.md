# CARVES Runtime 0.6.1-beta Version BOM

Status: `0.6.1-beta` version candidate BOM.

This file defines the current Runtime local candidate package and dist identity. It does not create a tag, publish a GitHub release, push NuGet.org packages, or bypass operator gates.

Current release note / identity entry:

```text
docs/release/runtime-0.6.1-beta-release-identity.md
```

## Conclusion

The next Runtime candidate is:

```text
CARVES.Runtime 0.6.1-beta
```

Plain meaning:

```text
This is a patch on the 0.6 startup and visible gateway line.
The prerelease channel is still beta.
The patch fixes release packaging shape, required-input validation, and output audit.
This is still not a public release, NuGet.org publication, signed package, hosted verification, or certification claim.
```

## Why 0.6.1-beta

This is not a `0.7.0-beta` capability release.

The externally meaningful capability line remains the `0.6` line:

- `carves up <target-project>`;
- project-local `.carves/carves`;
- agent-readable startup files;
- existing-project handling;
- visible gateway/status feedback.

The change is the formal beta package boundary:

- release dist no longer defaults to a source-heavy archive;
- release wrappers no longer silently build from source;
- required release inputs fail closed;
- generated release output is audited for forbidden paths.

Under [runtime-versioning-policy.md](runtime-versioning-policy.md), `patch` is for fixes to validation, packaging, and docs within the same capability line. That makes the correct jump:

```text
0.6.0-beta -> 0.6.1-beta
```

## Version Dimensions

| Dimension | Candidate value | Notes |
| --- | --- | --- |
| Runtime dist version | `0.6.1-beta` | External projects should bind the audited release dist. |
| Runtime CLI package version | `0.6.1-beta` | `carves` carries the current startup, visible gateway, and release packaging patch. |
| Runtime dist folder | `.dist/CARVES.Runtime-0.6.1-beta` | Do not treat `0.6.0-beta` as the current formal beta package after this package exists. |
| Git tag candidate | `carves-runtime-v0.6.1-beta` | Operator candidate only; not created by this BOM. |
| Release checkpoint / BOM | `docs/release/runtime-0.6.1-beta-version-bom.md` | This file. |
| Runtime release note / identity | `docs/release/runtime-0.6.1-beta-release-identity.md` | Current candidate identity. |
| Dist manifest source commit | Actual `MANIFEST.json.source_commit` | Must come from the generated dist. |

## Package Version Plan

This candidate upgrades only the Runtime shell package version.

| Package / Component | Previous Runtime line | Candidate version | Decision |
| --- | --- | --- | --- |
| `CARVES.Runtime.Cli` | `0.6.0-beta` | `0.6.1-beta` | Upgrade. Runtime release dist packaging changed and the CLI package should match the candidate dist identity. |
| `CARVES.Guard.Core` | `0.2.0-beta.1` | `0.2.0-beta.1` | Keep. Guard is not the upgraded line. |
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
dotnet pack ./src/CARVES.Runtime.Cli/carves.csproj -c Release -o <package-root>
pwsh ./scripts/pack-runtime-dist.ps1 -Version 0.6.1-beta -Force
pwsh ./scripts/assert-runtime-release-dist.ps1 -DistRoot <dist-root>
```

For startup and visible gateway confidence, also run:

```bash
<dist-root>/carves help
<dist-root>/carves gateway status
<dist-root>/carves status --watch --iterations 1 --interval-ms 0
<dist-root>/carves pilot dist-smoke --json
```

Full long-cycle Host stability, hosted verification, and public publication remain separate operator gates.

## Non-Claims

`0.6.1-beta` does not claim:

- public release readiness for the whole Runtime product line;
- remote package publication;
- signed packages;
- GitHub release or tag creation;
- hosted verification;
- certification, leaderboard, or benchmark status;
- finished dashboard readiness;
- OS sandboxing;
- provider SDK/API-backed worker execution;
- arbitrary BYOK worker activation.
