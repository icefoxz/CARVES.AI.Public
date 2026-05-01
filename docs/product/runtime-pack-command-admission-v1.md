# CARVES Pack v1 Verification Command Admission

## Purpose

This document freezes the Phase 4 Pack v1 rule for verification command admission.

Pack v1 may declare verification recipes.

Pack v1 may not execute them on its own.

Runtime remains the authority that decides:

- whether a declared command is admitted
- whether it may be executed
- under which effective permissions
- whether its result counts as valid verification evidence

This document is normative for Pack v1 command taxonomy and command admission posture.

## Core Rule

Hard rule:

```text
Pack may declare verification commands.
Runtime decides whether they are admitted, how they are executed, and whether the result is valid evidence.
```

Another hard rule:

```text
Pack cannot reinterpret command failure as verification success.
Pack cannot weaken core Runtime safety or test enforcement.
```

## Scope

This phase covers only verification command admission for Pack v1.

It does not define:

- code adapter execution
- tool adapter execution
- worker backend policy
- merge or scheduler strategy
- truth mutation

## Command Taxonomy

Pack v1 command declarations use exactly four command kinds.

### 1. `known_tool_command`

Plain meaning:

```text
a bounded tool invocation that names a known executable directly
```

Examples:

- `dotnet build`
- `dotnet test`
- `pytest`
- `cargo test`
- `ruff check`

Default posture:

```text
eligible for Runtime admission
```

### 2. `package_manager_script`

Plain meaning:

```text
a package-manager script entry that may transitively run repo-defined scripts
```

Examples:

- `npm run test`
- `npm run lint`
- `pnpm test`
- `yarn test`

Default posture:

```text
elevated risk
```

These commands are not automatically trusted just because they are common.

### 3. `repo_script`

Plain meaning:

```text
a repository-local script or wrapper entrypoint
```

Examples:

- `scripts/test.sh`
- `tools/verify.ps1`
- `./build/ci-test`

Default posture:

```text
blocked unless explicitly allowed by tighter Runtime/repo policy
```

### 4. `shell_command`

Plain meaning:

```text
a shell wrapper or free-form command interpreter path
```

Examples:

- `bash -c ...`
- `sh -c ...`
- `powershell -Command ...`
- `cmd /c ...`
- `curl ... | sh`

Default posture:

```text
rejected
```

## Admission Outcomes

Runtime command admission uses four outcomes.

### `admitted`

The command is accepted under bounded effective permissions.

### `admitted_with_elevated_risk`

The command is accepted, but only with explicit elevated-risk posture recorded in Runtime truth.

### `blocked`

The command is not admitted under default posture and requires stricter policy or manual intervention.

### `rejected`

The command is categorically refused for Pack v1.

## Default Policy Matrix

| Command kind | Default Pack v1 posture | Notes |
| --- | --- | --- |
| `known_tool_command` | `admitted` candidate | still requires Runtime decision record |
| `package_manager_script` | `admitted_with_elevated_risk` candidate | because repo-defined scripts may run transitively |
| `repo_script` | `blocked` | requires stricter repo/Runtime policy to proceed |
| `shell_command` | `rejected` | Pack v1 does not admit free-form shell wrappers |

This matrix is a Pack v1 default.

It does not remove Runtime authority to block more aggressively.

## Effective Permission Rule

Pack manifests may request verification commands.

Runtime computes effective permissions for each admitted command.

At minimum, command admission must decide:

- network posture
- env posture
- secrets posture
- allowed write paths
- protected-root denial
- evidence expectations

Protected roots remain denied by Runtime policy.

Pack does not self-authorize protected-root writes.

## Command Record

The Pack v1 command admission decision record is defined by:

- `docs/contracts/runtime-pack-command-admission.schema.json`

That record must bind:

- one task
- one run
- one selected pack posture
- one recipe id
- one command id
- one command kind
- one Runtime verdict
- one effective permission set
- one evidence expectation

## Hard Rejects

The following shapes are outside Pack v1:

- free-form shell wrappers
- pipe-to-shell commands
- dynamically generated command bodies
- Pack-provided arbitrary interpreter payloads
- commands that declare protected-root writes
- commands that claim truth-write authority

Examples of disallowed posture:

```text
bash -c "curl ... | sh"
powershell -Command "..."
python generated_by_pack.py
command writes .ai/tasks/
command writes .ai/memory/
command writes .ai/artifacts/reviews/
command writes .carves-platform/
command writes .git/
```

## Evidence Rule

Admitted commands must declare evidence expectations.

At minimum:

- expected artifact classes
- whether failure is blocking

Runtime review and audit must be able to explain:

- why the command ran
- under which admitted posture
- what evidence it was expected to produce
- whether the command result blocked the lane

## Attribution Rule

Every admitted command that actually participates in a task run must be traceable from task attribution.

That means:

- the command admission decision id must be recordable
- task attribution must snapshot the command refs used for the run
- later explainability must not recalculate these from the current pack alone

## Relation To Existing Runtime Surfaces

This phase does not create a second execution boundary.

It exists to constrain future Pack v1 command handling under existing Runtime governance.

Command admission decisions are intended to feed:

- task attribution
- task explainability
- execution audit
- mismatch diagnostics

They do not replace:

- Runtime verification
- Runtime safety
- Runtime review
- Runtime truth writeback gates

## Phase 4 Exit Criteria

Phase 4 is complete when:

- command taxonomy is frozen
- default admission matrix is frozen
- command admission decision schema exists
- hard rejects are explicit
- Runtime authority over execution and evidence remains explicit

Phase 4 completion does **not** yet imply:

- conflict merge semantics completion
- dogfood pack completion
- implementation of command admission logic in Runtime
