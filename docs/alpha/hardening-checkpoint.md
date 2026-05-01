# Alpha Guard Hardening Checkpoint

Checkpoint id: `alpha-guard-hardening-2026-04-14`

Release checkpoint: `alpha-guard-0.1.0-alpha.2`

Status: Alpha hardening complete for the stable external diff-only entry.

## Scope

This checkpoint covers the Alpha Guard external path:

```powershell
carves guard check --json
carves guard audit
carves guard report
carves guard explain <run-id>
```

The stable external path is diff-only. It reads the current git working-tree diff and `.ai/guard-policy.json`. It does not require Runtime task, card, taskgraph, memory, or resident host truth in the target repository.

Task-aware `guard run <task-id>` remains experimental and is not promoted by this checkpoint.

## Closed Gaps

`CARD-735` closed rule-branch coverage gaps for the Alpha policy evaluator and CLI decision output. Covered families include malformed policy, future schema, empty diff, protected and outside paths, changed-file and line budgets, dependency manifest and lockfile discipline, generated paths, rename-with-content-change, delete-without-replacement, missing tests, and mixed feature/refactor shape.

`CARD-736` hardened path normalization semantics. Guard now normalizes backslashes, repeated separators, current-directory segments, and safe parent traversals, while treating root escapes, absolute paths, drive-qualified paths, URI-like paths, rename old/new paths, and delete paths conservatively.

`CARD-737` made the decision read model resilient. Guard readbacks surface diagnostics for malformed records, future-version records, empty lines, skipped records, and truncated sidecar history without breaking canonical patch admission.

`CARD-738` resolved task-aware posture by marking `guard run` experimental in CLI help, text output, JSON output, and docs. Stable Alpha language now keeps `guard check` as the external entry.

`CARD-739` proved local packaged install smoke. A locally built `CARVES.Runtime.Cli` nupkg can be installed into a temporary `dotnet tool --tool-path`, then run against an external temporary git repository without Runtime task/card/taskgraph truth.

## Verification

Focused application tests:

```powershell
dotnet test tests\Carves.Runtime.Application.Tests\Carves.Runtime.Application.Tests.csproj --no-build --filter "GuardPolicyEvaluatorTests|GuardDecisionReadServiceTests|GuardRunDecisionServiceTests|AlphaGuardTrustBasisCoverageAuditTests|AlphaGuardReleaseCheckpointTests"
```

Observed result:

```text
Passed: 42/42
```

Focused integration tests:

```powershell
dotnet test tests\Carves.Runtime.IntegrationTests\Carves.Runtime.IntegrationTests.csproj --no-build --filter "GuardCheckCliTests|CliDistributionClosureTests"
```

Observed result:

```text
Passed: 15/15
```

Packaged install smoke:

```powershell
.\scripts\alpha\guard-packaged-install-smoke.ps1
```

Observed result:

```text
smoke: alpha_guard_packaged_install
package_install: install
remote_registry_published: false
global_tool_install_used: false
guard_check_allow=allow
guard_check_block=block
guard_audit decisions=2
guard_report: attention; allow=1; block=1
guard_explain found=True; rules=path.protected_prefix
truth_required: task=False; card=False; graph=False
```

Whitespace check:

```powershell
git diff --check -- . ':!.ai/tasks/nodes/*'
```

Observed result:

```text
passed with CRLF warnings limited to .ai truth files
```

## Remaining Limitations

Alpha Guard is not OS sandbox containment.

Diff-only mode evaluates a patch after files already exist in the working tree. It does not intercept system calls, virtualize the filesystem, run the agent in a container, or automatically roll back arbitrary writes.

Task-aware mode is experimental until a later live-worker proof exercises Runtime boundary, safety, packet enforcement, task state, and Guard decision mapping through one deterministic worker run.

The packaged install smoke proves local nupkg installation only. It does not prove remote registry publication, signed packages, unattended upgrade channels, or cross-platform installer behavior.

Alpha Guard v1 does not claim semantic correctness, architecture quality, AI planning, token optimization, or generated-code quality review.

## Beta Readiness Posture

Beta planning may begin for the stable diff-only Guard external entry.

Beta implementation must not treat that as production readiness. The first Beta cards should keep these blockers explicit:

- promote or keep excluding task-aware `guard run`
- decide whether remote registry publication is required for Beta
- add cross-platform packaged install smoke
- add version upgrade and rollback-path smoke
- add real external project pilot over more than one repository
- define the production safety language for non-OS-sandbox containment

## Default External Read Path

External users do not need Runtime wave history, governance program re-audit docs, taskgraph internals, or sibling repo role history for Alpha Guard onboarding.

Use this short path:

```text
README.md
docs/alpha/README.md
docs/alpha/policy-schema.md
docs/alpha/safety-model.md
docs/alpha/hardening-checkpoint.md
docs/release/alpha-guard-0.1.0-alpha.2.md
```
