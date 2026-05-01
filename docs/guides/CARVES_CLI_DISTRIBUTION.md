# CARVES CLI Distribution

This guide records the Phase 1 product-closure CLI entry.

It gives operators and external agents a stable `carves` command path without requiring them to remember `dotnet run --project ...`.

## Supported Entries

Phase 1 supports three bounded entries:

1. Source-tree wrapper for local development and dogfood.
2. Frozen local dist wrapper for external-project alpha use.
3. Local .NET tool package for global or tool-path installation.

These entries invoke the existing `CARVES.Runtime.Cli` friendly CLI. They do not create a second control plane.

## Guard Publishable Entry

The public-facing Guard entry is:

```powershell
carves guard init
carves guard check --json
carves guard audit
carves guard report
carves guard explain <run-id>
```

`carves guard init` creates the starter policy at `.ai/guard-policy.json` and refuses to overwrite an existing policy unless `--force` is supplied.

The Guard local package smoke is:

```powershell
.\scripts\guard\guard-packaged-install-smoke.ps1
```

This smoke builds a local nupkg, installs it with `dotnet tool install --tool-path`, verifies `help guard`, runs `guard init`, and proves `guard check --json` from a temporary external git repository. It does not push to NuGet.org.

NuGet.org publication remains an operator gate. The package metadata is prepared for prerelease packaging, but remote registry publication is not part of this phase.

## Source-Tree Wrapper

From the `CARVES.Runtime` repo root:

```powershell
.\carves.ps1 help
.\carves.ps1 init .
.\carves.ps1 doctor
.\carves.ps1 agent start
.\carves.ps1 agent start --json
.\carves.ps1 agent handoff
.\carves.ps1 agent handoff --json
.\carves.ps1 agent bootstrap
.\carves.ps1 pilot readiness
.\carves.ps1 pilot invocation
.\carves.ps1 pilot start
.\carves.ps1 pilot problem-intake
.\carves.ps1 pilot triage
.\carves.ps1 pilot follow-up
.\carves.ps1 pilot follow-up-plan
.\carves.ps1 pilot follow-up-record
.\carves.ps1 pilot follow-up-intake
.\carves.ps1 pilot follow-up-gate
.\carves.ps1 pilot record-follow-up-decision <decision> --all --reason <text>
.\carves.ps1 pilot next
.\carves.ps1 pilot report-problem <json-path>
.\carves.ps1 pilot list-problems
.\carves.ps1 pilot inspect-problem <problem-id>
.\carves.ps1 pilot activation
.\carves.ps1 pilot dist-smoke
.\carves.ps1 pilot dist-binding
.\carves.ps1 pilot target-proof
.\carves.ps1 pilot guide
.\carves.ps1 pilot status
.\carves.ps1 pilot resources
.\carves.ps1 pilot commit-hygiene
.\carves.ps1 pilot commit-plan
.\carves.ps1 pilot closure
.\carves.ps1 pilot residue
.\carves.ps1 pilot ignore-plan
.\carves.ps1 pilot ignore-record
.\carves.ps1 pilot record-ignore-decision keep_local --all --reason "operator accepted local CARVES residue"
.\carves.ps1 pilot dist
.\carves.ps1 pilot proof
.\carves.ps1 status
```

For shells that prefer a `.cmd` entry:

```powershell
.\carves.cmd help
.\carves.cmd agent start
.\carves.cmd agent handoff
```

If this repo root is on `PATH`, Windows can resolve the wrapper as:

```powershell
carves help
carves init .
carves agent start
carves agent start --json
carves agent handoff
carves agent bootstrap
carves pilot readiness
carves pilot invocation
carves pilot start
carves pilot problem-intake
carves pilot triage
carves pilot follow-up
carves pilot follow-up-plan
carves pilot follow-up-record
carves pilot follow-up-intake
carves pilot follow-up-gate
carves pilot record-follow-up-decision <decision> --all --reason <text>
carves pilot next
carves pilot report-problem <json-path>
carves pilot list-problems
carves pilot inspect-problem <problem-id>
carves pilot activation
carves pilot dist-smoke
carves pilot dist-binding
carves pilot target-proof
carves pilot guide
carves pilot status
carves pilot resources
carves pilot commit-hygiene
carves pilot commit-plan
carves pilot closure
carves pilot residue
carves pilot ignore-plan
carves pilot ignore-record
carves pilot record-ignore-decision keep_local --all --reason "operator accepted local CARVES residue"
carves pilot dist
carves pilot proof
carves status
```

The source-tree wrapper is a development convenience. It stages the CLI source into an isolated temp generation before building, so normal repo `obj/bin` locks do not block basic `carves` entry. Users and agents no longer need the project path.

On WSL/Linux, the source-tree `./carves` wrapper uses the same rule with a lighter fallback: it runs `runtime-cli/carves.dll` when that published CLI exists, otherwise it falls back to:

```bash
dotnet run --project <RuntimeRoot>/src/CARVES.Runtime.Cli/carves.csproj -- <arguments>
```

Set `CARVES_RUNTIME_FORCE_SOURCE=1` only when intentionally testing the source fallback.

If Windows Application Control blocks the staged temp assembly, the wrapper retries once and then falls back to source project execution:

```powershell
dotnet run --project <RuntimeRoot>\src\CARVES.Runtime.Cli\carves.csproj -- <arguments>
```

This fallback preserves source-tree dogfood usability. It does not replace signed release packaging.

## Frozen Local Dist Wrapper

For stable external-project alpha work, prefer a frozen local dist root under the WSL filesystem when the target project is also being worked from WSL:

```bash
<runtime-dist>/carves pilot invocation --json
<runtime-dist>/carves host ensure --json
```

The local release dist contains `runtime-cli/carves.dll`. The Unix `carves` wrapper executes that published CLI directly, avoiding repeated `dotnet run --project` source-tree cold startup. If the published CLI is missing, the release wrapper fails clearly; it does not fall back to source execution.

Windows callers can keep using:

```powershell
& "<LocalDistRoot>\carves.ps1" pilot invocation --json
& "<LocalDistRoot>\carves.ps1" host ensure --json
```

The PowerShell release wrapper also requires `runtime-cli\carves.dll`. Source-build fallback is only for source-tree or explicit `-DistKind dev` packaging, not the formal release dist.

`carves host ensure --json` is the agent-safe readiness command for external projects. It starts the resident host when none is running, validates a live host when one already exists, and returns machine-readable readiness without changing target project files.

## Local .NET Tool Package

Build a local tool package:

```powershell
$packageRoot = Join-Path $env:TEMP "carves-runtime-cli-packages"
dotnet pack .\src\CARVES.Runtime.Cli\carves.csproj -c Release -o $packageRoot
```

Install it globally from the local package source:

```powershell
dotnet tool install --global CARVES.Runtime.Cli --add-source $packageRoot --version 0.6.2-beta
```

If it is already installed:

```powershell
dotnet tool update --global CARVES.Runtime.Cli --add-source $packageRoot --version 0.6.2-beta
```

After install:

```powershell
carves help
carves init .
carves doctor
carves agent start
carves agent start --json
carves agent handoff
carves agent handoff --json
carves agent bootstrap
carves pilot readiness
carves pilot invocation
carves pilot start
carves pilot problem-intake
carves pilot triage
carves pilot follow-up
carves pilot follow-up-plan
carves pilot follow-up-record
carves pilot follow-up-intake
carves pilot follow-up-gate
carves pilot record-follow-up-decision <decision> --all --reason <text>
carves pilot next
carves pilot report-problem <json-path>
carves pilot list-problems
carves pilot inspect-problem <problem-id>
carves pilot activation
carves pilot dist-smoke
carves pilot dist-binding
carves pilot target-proof
carves pilot guide
carves pilot status
carves pilot resources
carves pilot commit-hygiene
carves pilot commit-plan
carves pilot closure
carves pilot residue
carves pilot ignore-plan
carves pilot ignore-record
carves pilot record-ignore-decision keep_local --all --reason "operator accepted local CARVES residue"
carves pilot dist
carves pilot proof
```

For isolated validation without modifying global tools:

```powershell
$toolRoot = Join-Path $env:TEMP "carves-runtime-cli-tool"
dotnet tool install CARVES.Runtime.Cli --tool-path $toolRoot --add-source $packageRoot --version 0.6.2-beta
& (Join-Path $toolRoot "carves.exe") help
```

## Minimum Smoke

The Phase 1 smoke path is:

```powershell
carves help
carves init .
carves doctor
carves agent start
carves agent start --json
carves agent handoff
carves agent handoff --json
carves agent bootstrap
carves pilot readiness
carves pilot invocation
carves pilot start
carves pilot problem-intake
carves pilot triage
carves pilot follow-up
carves pilot follow-up-plan
carves pilot follow-up-record
carves pilot follow-up-intake
carves pilot follow-up-gate
carves pilot next
carves pilot report-problem <json-path>
carves pilot list-problems
carves pilot inspect-problem <problem-id>
carves pilot activation
carves pilot dist-smoke
carves pilot dist-binding
carves pilot target-proof
carves pilot guide
carves pilot status
carves pilot resources
carves pilot commit-hygiene
carves pilot commit-plan
carves pilot closure
carves pilot residue
carves pilot ignore-plan
carves pilot dist
carves pilot proof
```

Expected result:

- `help` lists canonical `carves` verb families.
- `init` is available as the bounded first-run attach wrapper for an existing git/workspace repo.
- `doctor` separates tool readiness, target repo readiness, and resident host readiness.
- `agent start` prints the one-command external-agent thread start readback and current `next_governed_command`.
- `agent handoff` prints the governed handoff proof.
- `agent handoff --json` returns machine-readable proof data with `is_valid=true`.
- `agent bootstrap` reports target bootstrap readiness; `agent bootstrap --write` creates missing bootstrap files without overwriting existing files.
- `pilot readiness` prints the read-only Runtime alpha external-use readiness rollup.
- `pilot invocation` prints the read-only CLI invocation contract and Runtime-root lane.
- `pilot start` prints the read-only external-agent pilot start bundle, guardrails, required readbacks, and current next governed command.
- `pilot problem-intake` prints the read-only problem reporting schema and stop/report contract; `pilot report-problem` records bounded target runtime problem/evidence records when an agent is blocked; `pilot triage` groups recorded reports into an operator-facing friction ledger; `pilot follow-up` promotes repeated or blocking patterns into operator-review candidates without creating cards or tasks; `pilot follow-up-plan` projects accept/reject/wait choices without recording decisions; `pilot follow-up-record` audits durable operator decision records; `pilot follow-up-intake` projects accepted records into formal planning inputs; `pilot follow-up-gate` checks accepted inputs against intent draft and the single active planning slot; `pilot record-follow-up-decision` writes operator decisions without creating cards or tasks.
- `pilot next` prints the read-only next governed command and stop/report triggers before each external-agent step.
- `pilot activation` prints the read-only CLI activation plan and operator-owned alias/PATH boundaries.
- `pilot dist-smoke` proves the frozen local Runtime dist exists, includes current product closure resources, and matches the clean Runtime source HEAD.
- `pilot dist-binding` prints the read-only target dist binding plan and operator-owned retarget commands.
- `pilot target-proof` proves the current external target is initialized, bootstrapped, and bound to the fresh frozen Runtime dist.
- `pilot guide` prints the read-only productized external-project pilot sequence.
- `pilot status` prints the read-only current pilot stage and next governed command.
- `pilot resources` prints the read-only external consumer resource pack and Runtime/target boundary.
- `pilot commit-hygiene` classifies target dirty paths before any final product proof commit.
- `pilot commit-plan` turns those classifications into stage/exclude/review path lists and command previews.
- `pilot closure` verifies the target repo is clean after the operator-reviewed product proof commit.
- `pilot residue` explains excluded local/tooling residue after closure and suggests reviewed `.gitignore` candidates without mutating the target.
- `pilot ignore-plan` turns residue suggestions into operator-reviewed keep-local, cleanup, or `.gitignore` decision candidates without mutating the target.
- `pilot ignore-record` shows whether current ignore-plan entries have durable operator decision records, whether those records pass malformed/invalid/stale/conflict audit, and whether their paths are tracked and clean in target git; `pilot record-ignore-decision` writes those records without mutating `.gitignore`.
- `pilot dist` verifies the target repo is consuming CARVES from a frozen local Runtime dist root.
- `pilot proof` aggregates local dist handoff and target commit closure into one final read-only product pilot proof.

## External Target Dogfood

Phase 4 external target dogfood is recorded in:

- `docs/runtime/carves-product-closure-phase-4-external-target-dogfood-proof.md`
- `docs/guides/CARVES_EXTERNAL_TARGET_DOGFOOD.md`

When validating from a target repo outside `CARVES.Runtime`, use the same `carves` command path and confirm that `agent handoff --json` projects `runtime_document_root_mode=attach_handshake_runtime_root` or `runtime_manifest_root`.

Phase 6 real target writeback is recorded in:

- `docs/runtime/carves-product-closure-phase-6-official-truth-writeback.md`
- `docs/guides/CARVES_REAL_PROJECT_WRITEBACK.md`

Phase 7 real target managed workspace execution is recorded in:

- `docs/runtime/carves-product-closure-phase-7-managed-workspace-execution.md`
- `docs/guides/CARVES_REAL_PROJECT_WORKSPACE.md`

Phase 8 real target managed workspace writeback is recorded in:

- `docs/runtime/carves-product-closure-phase-8-managed-workspace-writeback.md`
- `docs/guides/CARVES_REAL_PROJECT_WORKSPACE_WRITEBACK.md`

Phase 9 productized external-project pilot guide is recorded in:

- `docs/runtime/carves-product-closure-phase-9-productized-pilot-guide.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_GUIDE.md`

Phase 10 productized external-project pilot status is recorded in:

- `docs/runtime/carves-product-closure-phase-10-productized-pilot-status.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md`

Phase 11B target agent bootstrap pack is recorded in:

- `docs/runtime/carves-product-closure-phase-11b-target-agent-bootstrap-pack.md`
- `docs/guides/CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md`

Phase 12 existing-target bootstrap repair is recorded in:

- `docs/runtime/carves-product-closure-phase-12-existing-target-bootstrap-repair.md`
- `docs/guides/CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md`

Phase 13 target commit hygiene is recorded in:

- `docs/runtime/carves-product-closure-phase-13-target-commit-hygiene.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md`

Phase 14 target commit plan is recorded in:

- `docs/runtime/carves-product-closure-phase-14-target-commit-plan.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md`

Phase 15 target commit closure is recorded in:

- `docs/runtime/carves-product-closure-phase-15-target-commit-closure.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md`

Phase 16 local dist handoff is recorded in:

- `docs/runtime/carves-product-closure-phase-16-local-dist-handoff.md`
- `docs/guides/CARVES_RUNTIME_LOCAL_DIST.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md`

Phase 17 product pilot proof is recorded in:

- `docs/runtime/carves-product-closure-phase-17-product-pilot-proof.md`
- `docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md`

Phase 18 external consumer resource pack is recorded in:

- `docs/runtime/carves-product-closure-phase-18-external-consumer-resource-pack.md`
- `docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md`

Phase 19 CLI invocation contract is recorded in:

- `docs/runtime/carves-product-closure-phase-19-cli-invocation-contract.md`
- `docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md`

Phase 20 CLI activation plan is recorded in:

- `docs/runtime/carves-product-closure-phase-20-cli-activation-plan.md`
- `docs/guides/CARVES_CLI_ACTIVATION_PLAN.md`

Phase 21 target dist binding plan is recorded in:

- `docs/runtime/carves-product-closure-phase-21-target-dist-binding-plan.md`
- `docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md`

Phase 22 local dist freshness smoke is recorded in:

- `docs/runtime/carves-product-closure-phase-22-local-dist-freshness-smoke.md`
- `docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md`

Phase 23 frozen dist target readback proof is recorded in:

- `docs/runtime/carves-product-closure-phase-23-frozen-dist-target-readback-proof.md`
- `docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md`

Phase 24 wrapper Runtime root binding is recorded in:

- `docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md`

Phase 25 external target product proof closure is recorded in:

- `docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md`

Phase 26A projection cleanup keeps the active product closure read model aligned to Phase 25:

- `docs/runtime/carves-product-closure-phase-26a-product-closure-projection-cleanup.md`

Phase 26 real external repo pilot is recorded in:

- `docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md`

Phase 27 external target residue policy is recorded in:

- `docs/runtime/carves-product-closure-phase-27-external-target-residue-policy.md`

Phase 28 target ignore decision plan is recorded in:

- `docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md`

Phase 29 target ignore decision record is recorded in:

- `docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md`

Phase 30 target ignore decision record audit is recorded in:

- `docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md`

Phase 31 target ignore decision record commit readback is recorded in:

- `docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md`

Phase 32 alpha external-use readiness rollup is recorded in:

- `docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md`

Phase 33 external target pilot start bundle is recorded in:

- `docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md`
- `docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md`

Phase 34 agent problem intake is recorded in:

- `docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md`
- `docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md`

Phase 35 agent problem triage ledger is recorded in:

- `docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md`
- `docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md`

Phase 36 agent problem follow-up candidates is recorded in:

- `docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md`

Phase 37 agent problem follow-up decision plan is recorded in:

- `docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md`

Phase 38 agent problem follow-up decision record is recorded in:

- `docs/runtime/carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md`

Phase 39 agent problem follow-up planning intake is recorded in:

- `docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md`

Phase 40 agent problem follow-up planning gate is recorded in:

- `docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md`

## Local Runtime Distribution

External target repos should not point at an actively edited Runtime source checkout when the goal is stable product dogfood.

Create a frozen local distribution folder instead:

```powershell
.\scripts\pack-runtime-dist.ps1 -Version 0.6.2-beta
```

Default output:

```text
<runtime-dist>
```

Then attach or repair target repos so their `.ai/runtime.json` runtime root points at the dist folder.

Verify the target-to-dist handoff from the target repo:

```powershell
carves agent start --json
carves pilot invocation --json
carves pilot activation --json
carves pilot readiness --json
carves pilot start --json
carves pilot problem-intake --json
carves pilot triage --json
carves pilot follow-up --json
carves pilot follow-up-plan --json
carves pilot follow-up-record --json
carves pilot follow-up-intake --json
carves pilot follow-up-gate --json
carves pilot record-follow-up-decision <decision> --all --reason <text> --json
carves pilot next --json
carves pilot dist-smoke --json
carves pilot dist-binding --json
carves pilot dist --json
carves pilot target-proof --json
carves pilot resources --json
carves pilot residue --json
carves pilot ignore-plan --json
carves pilot ignore-record --json
carves pilot proof --json
```

The local dist workflow is recorded in:

- `docs/guides/CARVES_RUNTIME_LOCAL_DIST.md`
- `docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md`
- `docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md`
- `docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md`
- `docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md`
- `docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md`
- `docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md`
- `docs/guides/CARVES_CLI_ACTIVATION_PLAN.md`
- `docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md`
- `docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md`
- `docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md`

## Non-Claims

Phase 1 does not claim:

- public NuGet publication
- signed release packaging
- a project-creation or goal-capture `carves init` wizard
- real external target repo dogfood proof
- OS/process sandboxing
- full ACP or MCP transport implementation

Those remain later product-closure phases.
