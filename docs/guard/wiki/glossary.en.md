# CARVES.Guard Glossary

Language: [中文](glossary.zh-CN.md)

This glossary explains the keywords used by CARVES.Guard docs and command output.

## Core Concepts

| Keyword | Meaning |
| --- | --- |
| CARVES.Guard | A patch admission gate for AI-generated code changes. It does not write code. It checks whether a patch may enter review or merge. |
| AI coding tool | A tool that writes code, such as Claude Code, Cursor, Copilot, Codex, or another assistant. Guard does not replace it. |
| patch | A set of code changes that has not entered your accepted review or merge path yet. |
| diff | The concrete git difference: added, deleted, modified, or renamed lines and files. |
| working tree | The current state of your project directory. `guard check` checks changes materialized there. |
| admission gate | A gate before the next step. Guard's gate sits before review or merge. |
| policy | The rule set written by your project. It tells Guard what paths can change, what budgets apply, and when to review or block. |
| `.ai/guard-policy.json` | The default policy path. External projects need this Guard policy file. |

## Decision Terms

| Keyword | Meaning |
| --- | --- |
| decision | The final Guard result for one patch: `allow`, `review`, or `block`. |
| `allow` | The patch satisfies policy and may continue to normal human review or merge. It does not prove semantic correctness. |
| `review` | The patch has risk that needs human attention, but it is not a hard block. |
| `block` | The patch violates a hard rule and must not enter your accepted review or merge path. |
| violation | A `block` finding. Any violation makes the final decision `block`. |
| warning | A `review` finding. If there are warnings and no violations, the final decision is `review`. |
| `rule_id` | A stable rule identifier, for example `budget.max_changed_files`. Start with this when debugging. |
| evidence | The reason for the finding, for example `12 files changed; budget is 5`. |
| evidence ref | A reference for one evidence item, useful in `explain` output and logs. |
| `run_id` | The unique id for one Guard check. Use it with `carves guard explain <run-id>`. |

## Command Terms

| Keyword | Meaning |
| --- | --- |
| `carves guard check` | Checks the current git patch and returns allow/review/block. |
| `--json` | Emits machine-readable JSON for scripts and CI. |
| `--repo-root` | Points Guard at a repository when you are not running from the project root. |
| `--policy` | Sets the policy file path. Default: `.ai/guard-policy.json`. |
| `--base` | Sets the git diff base ref. In this beta, the patch still needs to be materialized in the working tree for CI use. |
| `--head` | Sets the git diff head ref. CI usually materializes the PR diff first, then runs `check`. |
| `carves guard audit` | Shows recent Guard decision records. |
| `carves guard report` | Shows current policy status and recent allow/review/block summary. |
| `carves guard explain` | Expands one decision by `run_id`: rules, paths, evidence, and outcome. |

## Policy Terms

| Keyword | Meaning |
| --- | --- |
| `schema_version` | Policy format version. v1 uses `1`. |
| `policy_id` | Your policy name, for example `webapp-guard-policy`. |
| `path_policy` | Path rules: allowed paths and protected paths. |
| `allowed_path_prefixes` | Path prefixes AI patches may touch, such as `src/` and `tests/`. |
| `protected_path_prefixes` | Sensitive paths. Touching them usually blocks the patch. `.git/` is always protected by Guard. |
| `outside_allowed_action` | What to do when a patch touches a path outside allowed prefixes. |
| `protected_path_action` | What to do when a patch touches a protected path. Beginners should use `block`. |
| `change_budget` | Patch budget: changed file count, added lines, deleted lines, and rename limits. |
| `max_changed_files` | Maximum number of changed files in one patch. |
| `max_total_additions` | Maximum total added lines in one patch. |
| `max_total_deletions` | Maximum total deleted lines in one patch. |
| `max_file_additions` | Maximum added lines in one file. |
| `max_file_deletions` | Maximum deleted lines in one file. |
| `max_renames` | Maximum renamed files in one patch. |
| `dependency_policy` | Dependency rules for manifests and lockfiles. |
| manifest | A dependency manifest, such as `package.json` or `*.csproj`. |
| lockfile | A file that pins dependency versions, such as `package-lock.json` or `packages.lock.json`. |
| `change_shape` | Patch-shape rules: rename, delete, generated output, and source/test coupling. |
| generated path | Generated output directories, such as `dist/`, `build/`, and `coverage/`. |
| `source_path_prefixes` | Source paths, such as `src/`. |
| `test_path_prefixes` | Test paths, such as `tests/`. |
| `missing_tests_action` | What to do when source changes without test changes. Start with `review`, then move to `block` when ready. |
| `decision.fail_closed` | Whether Guard should fail conservatively when it cannot read policy or git diff. Keep this `true`. |
| `review_is_passing` | Whether `review` counts as passing. v1 requires `false`. |
| `emit_evidence` | Whether Guard emits evidence. v1 requires `true`. |

## GitHub Actions Terms

| Keyword | Meaning |
| --- | --- |
| CI | Automated checks. GitHub Actions is one CI option. |
| pull request | A proposed change set before merging. |
| materialize PR diff | Apply the PR diff to the working tree in CI so `guard check` can inspect it. |
| fail the job | Return a non-zero exit code so GitHub Actions marks the check failed. `review` and `block` both fail the command. |
| self-hosted runner | A runner you control. It can preinstall `carves`. |
| internal package source | Your team's private tool source. This beta doc does not claim public registry publication. |
