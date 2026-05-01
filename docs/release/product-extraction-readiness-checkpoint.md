# Product Extraction Readiness Checkpoint

Status: public snapshot extraction checkpoint.

Guard, Handoff, Audit, Shield, and Matrix are extraction-ready product units for bounded copy/split readiness. They are not yet separate repositories.

The public repository keeps product source, public docs, release notes, and local verification scripts. It excludes private operating history, live control-plane truth, local host state, Codex local state, generated packages, logs, and release archives.

This checkpoint preserves operator review context without requiring public users to understand private CARD traceability before using Guard, Handoff, Audit, Shield, or Matrix.

## Product Roots

| Product | Core package | CLI package | Code root | CLI root | Docs root | Script root | Version | Command |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Guard | CARVES.Guard.Core | CARVES.Guard.Cli | src/CARVES.Guard.Core/ | src/CARVES.Guard.Cli/ | docs/guard/ | scripts/guard/ | 0.2.0-beta.1 | carves-guard |
| Handoff | CARVES.Handoff.Core | CARVES.Handoff.Cli | src/CARVES.Handoff.Core/ | src/CARVES.Handoff.Cli/ | docs/handoff/ | scripts/handoff/ | 0.1.0-alpha.1 | carves-handoff |
| Audit | CARVES.Audit.Core | CARVES.Audit.Cli | src/CARVES.Audit.Core/ | src/CARVES.Audit.Cli/ | docs/audit/ | scripts/audit/ | 0.1.0-alpha.1 | carves-audit |
| Shield | CARVES.Shield.Core | CARVES.Shield.Cli | src/CARVES.Shield.Core/ | src/CARVES.Shield.Cli/ | docs/shield/ | scripts/shield/ | 0.1.0-alpha.1 | carves-shield |
| Matrix | CARVES.Matrix.Core | CARVES.Matrix.Cli | src/CARVES.Matrix.Core/ | src/CARVES.Matrix.Cli/ | docs/matrix/ | scripts/matrix/ | 0.2.0-alpha.1 | carves-matrix |

Remaining extraction risks are bounded dependencies, package ownership decisions, and operator publication steps. The packages are not published to NuGet.org.

## Runtime Boundary

Runtime remains a compatibility and behavior-reference host. External users do not need Runtime task/card governance to run `carves guard`, `carves handoff`, `carves audit summary/timeline/explain/evidence`, `carves shield`, or `carves matrix`.

This checkpoint does not claim already separate repositories, GitHub release has been created, Git tag has been created, NuGet.org packages have been published, packages have been signed, hosted verification exists, public leaderboard exists, certification exists, operating-system sandboxing exists, automatic rollback of arbitrary writes exists, or external users must adopt Runtime governance.

## Validation Record

```text
dotnet build CARVES.Runtime.sln --no-restore
dotnet test tests\Carves.Runtime.IntegrationTests\Carves.Runtime.IntegrationTests.csproj --filter "RuntimeWrapperSlimmingTests|GuardExtractionShellTests|ShieldExtractionShellTests|MatrixExtractionShellTests|MatrixReleaseReadinessTests|GitHubPublishReadinessTests"
dotnet test tests\Carves.Audit.Tests\Carves.Audit.Tests.csproj --filter AuditCliRunnerTests
pwsh ./scripts/matrix/matrix-proof-lane.ps1 -ArtifactRoot <temp-artifact-root> -Configuration Debug
pwsh ./scripts/release/github-publish-readiness.ps1 -ArtifactRoot <temp-artifact-root> -AllowDirty
```

Expected public gate evidence: passed, 21 tests; passed, 19 tests; manifest includes 11 local package candidates.
