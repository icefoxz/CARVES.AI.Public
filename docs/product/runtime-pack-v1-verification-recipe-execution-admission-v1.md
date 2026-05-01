# Pack v1 Verification Recipe Execution Admission v1

## Scope

This phase closes bounded Runtime integration for `verification_recipe` within Pack v1.

The work in this phase is limited to:

- consuming selected declarative Pack v1 `verification_recipe` entries through existing Runtime-owned validation command flow
- classifying recipe commands by the frozen Pack v1 command taxonomy
- persisting one Runtime-owned command admission decision record per recipe command under the existing execution run truth root
- projecting bounded command admission evidence through existing run and execution-trace inspect surfaces

This phase does **not**:

- open Adapter Host
- open Marketplace or registry distribution
- grant Pack-owned truth mutation
- grant Review verdict or Merge authority
- close the full Review gate integration line

## Runtime integration result

Selected declarative Pack v1 manifests can now contribute validation commands through existing Runtime execution wiring.

The integration path is:

1. a declarative manifest is admitted through the Pack v1 manifest bridge
2. a selected declarative pack is resolved from existing Runtime pack selection truth
3. `verification_recipe` entries are evaluated by `RuntimePackVerificationRecipeAdmissionService`
4. effective validation commands are overlaid onto the worker request without mutating task graph truth
5. command admission decision records are written under `.ai/runtime/runs/<task-id>/`
6. `inspect run` and `inspect execution-trace` project bounded `pack_command_admission` evidence

## Command taxonomy posture

The default Pack v1 verification command posture is now enforced in Runtime-owned execution admission:

- `known_tool_command` -> admitted
- `package_manager_script` -> admitted with elevated risk
- `repo_script` -> blocked by default
- `shell_command` -> rejected by default

Hard reject remains in place for:

- shell wrappers
- free-form shell token expansion
- protected-root writes
- Pack-owned truth-write claims

## Truth and boundary notes

- command admission decisions are stored under the existing execution run truth root
- Pack v1 does not create a second control plane
- Pack v1 does not create a second truth root
- task graph truth is not rewritten to persist effective validation commands
- review gate behavior is unchanged

## Acceptance summary

This phase is accepted when all of the following are true:

- selected declarative `verification_recipe` commands appear in Runtime-owned validation flow
- command admission decision records are persisted for admitted, elevated-risk, blocked, and rejected commands
- execution-trace inspection can explain which verification commands were admitted from the selected declarative pack
- historical decision records remain attached to the execution run rather than being recomputed from current selection state

## Closure wording

The accurate closure wording for this phase is:

```text
Pack v1 verification_recipe execution admission: completed
```

The inaccurate closure wording is:

```text
Pack v1 full Runtime integration: completed
```
