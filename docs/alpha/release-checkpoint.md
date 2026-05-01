# Alpha Guard Release Checkpoint

Current Alpha Guard Beta identity:

```text
alpha-guard-0.2.0-beta.1
```

Release note:

```text
docs/release/alpha-guard-0.2.0-beta.1.md
```

Beta release checkpoint:

```text
docs/beta/guard-beta-release-checkpoint.md
```

Runtime package version:

```text
0.2.0-beta.1
```

Policy schema:

```text
guard-policy.v1
```

Stable Beta external entry:

```powershell
carves guard check --json
carves guard audit
carves guard report
carves guard explain <run-id>
```

Task-aware Runtime entry, experimental:

```powershell
carves guard run <task-id> --json
```

`guard run` is not part of the stable external Beta entry for `0.2.0-beta.1`. Use diff-only `guard check` as the default external path.

Task-aware posture:

```text
docs/beta/guard-run-beta-posture.md
```

Beta scope contract:

```text
docs/alpha/beta-scope-contract.md
```

Local package proof:

```powershell
.\scripts\alpha\guard-packaged-install-smoke.ps1
pwsh ./scripts/beta/guard-packaged-install-smoke.ps1
```

This builds a local `CARVES.Runtime.Cli` nupkg, installs or updates `carves` into a temporary tool path, and runs the installed CLI against a temporary external git repository. The proof covers allow, block, audit, report, and explain without requiring `.ai/tasks/`, cards, taskgraph, or resident Runtime host truth in the target repo.

This is not remote registry distribution.

Phase 2 evidence covers cross-platform packaged install proof, remote registry posture, policy compatibility, and decision-store hardening. Phase 3 evidence covers the external pilot matrix. Phase 4 evidence covers the CI proof lane. Phase 5 evidence covers the Beta threat model. Phase 6 evidence decides task-aware `guard run` posture: experimental for this Beta, not a stable external claim. Phase 7 evidence records the final Beta release checkpoint.

Beta distribution posture:

```text
docs/beta/guard-install-and-distribution.md
```

Beta external pilot matrix:

```powershell
pwsh ./scripts/beta/guard-external-pilot-matrix.ps1
```

```text
docs/beta/guard-external-pilot-matrix.md
```

Phase 3 evidence covers three external repo shapes, six allow/block scenarios, and three audit/report/explain readback sets. No block-level product issue was discovered by the matrix, so CARD-753 is a no-op resolution gate.

Beta CI proof lane:

```powershell
pwsh ./scripts/beta/guard-beta-proof-lane.ps1
```

```text
docs/beta/guard-beta-ci-proof-lane.md
```

Phase 4 evidence covers focused Guard Application tests, focused Guard Integration tests, Beta packaged install smoke, and Beta external pilot matrix from one CI-safe command. The lane requires no provider secrets, no remote package publication, and no live worker tests.

Beta threat model:

```text
docs/beta/guard-beta-threat-model.md
```

Phase 5 evidence freezes production safety language as a narrow process-level patch admission claim: no OS-level containment, no syscall interception, no filesystem virtualization, no network isolation, and no automatic rollback.

Hardening checkpoint:

```text
docs/alpha/hardening-checkpoint.md
```

The hardening checkpoint records the closed Alpha gaps, current focused test results, packaged smoke proof, remaining limitations, and the Beta readiness posture. Beta planning may begin for the stable diff-only external entry, but that does not promote Alpha Guard to production readiness.

Historical Alpha release note:

```text
docs/release/alpha-guard-0.1.0-alpha.2.md
```

This checkpoint remains pre-production. It does not claim production readiness, OS-level sandboxing, automatic rollback, semantic correctness, AI planning, token routing, Runtime governance onboarding, or architecture advice.

Beta readiness verdict:

```text
0.2.0-beta.1 is ready for external pilots through diff-only guard check.
```
