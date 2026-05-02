# CARVES.Guard Policy Template

Language: [Chinese](guard-policy-starter.zh-CN.md)

Copy this file into your project:

```text
.ai/guard-policy.json
```

This starter policy fits many small application repositories:

- source code in `src/`
- tests in `tests/`
- `README.md` changes allowed
- dependency manifests are `package.json` or `*.csproj`
- lockfiles are `package-lock.json` or `packages.lock.json`
- one patch can change at most 5 files by default

If a field is unclear, check the [Glossary](glossary.en.md).

## Template

```json
{
  "schema_version": 1,
  "policy_id": "starter-guard-policy",
  "description": "Starter CARVES.Guard policy for a small application repo.",
  "path_policy": {
    "path_case": "case_sensitive",
    "allowed_path_prefixes": [
      "src/",
      "tests/",
      "README.md",
      "package.json",
      "package-lock.json",
      "*.csproj",
      "packages.lock.json"
    ],
    "protected_path_prefixes": [
      ".git/",
      ".github/workflows/",
      ".env",
      ".env.local",
      "secrets/",
      "infra/production/"
    ],
    "outside_allowed_action": "review",
    "protected_path_action": "block"
  },
  "change_budget": {
    "max_changed_files": 5,
    "max_total_additions": 200,
    "max_total_deletions": 200,
    "max_file_additions": 100,
    "max_file_deletions": 100,
    "max_renames": 1
  },
  "dependency_policy": {
    "manifest_paths": [
      "package.json",
      "*.csproj"
    ],
    "lockfile_paths": [
      "package-lock.json",
      "packages.lock.json"
    ],
    "manifest_without_lockfile_action": "review",
    "lockfile_without_manifest_action": "review",
    "new_dependency_action": "review"
  },
  "change_shape": {
    "allow_rename_with_content_change": false,
    "allow_delete_without_replacement": false,
    "generated_path_prefixes": [
      "dist/",
      "build/",
      "coverage/"
    ],
    "generated_path_action": "review",
    "mixed_feature_and_refactor_action": "review",
    "require_tests_for_source_changes": true,
    "source_path_prefixes": [
      "src/"
    ],
    "test_path_prefixes": [
      "tests/"
    ],
    "missing_tests_action": "review"
  },
  "decision": {
    "fail_closed": true,
    "default_outcome": "allow",
    "review_is_passing": false,
    "emit_evidence": true
  }
}
```

## How To Adapt It

### Your source is not in `src/`

If your source is in `app/`, replace:

```json
"src/"
```

with:

```json
"app/"
```

Also update:

```json
"source_path_prefixes": [
  "app/"
]
```

### Your tests are not in `tests/`

If your tests are in `test/`:

```json
"test_path_prefixes": [
  "test/"
]
```

Also replace `tests/` with `test/` in `allowed_path_prefixes`.

### You want stricter source/test discipline

Change:

```json
"missing_tests_action": "review"
```

to:

```json
"missing_tests_action": "block"
```

Now source changes without test changes block the patch.

### You want AI to change at most 3 files

Change:

```json
"max_changed_files": 3
```

### You want to delete source files

The starter policy keeps:

```json
"allow_delete_without_replacement": false
```

That means a patch deleting a file must include credible replacement evidence. CARVES.Guard does not treat any random added file as a replacement.

Accepted replacement candidates are intentionally narrow:

- a git rename from the deleted path;
- an added file at the same path;
- an added file with the same file name and extension;
- an added file in the same directory with the same extension and a related stem, such as `old.ts` -> `old-v2.ts`.

If the replacement is uncertain, Guard blocks with `shape.delete_without_replacement`. This rule does not prove semantic equivalence; it only prevents silent delete-plus-unrelated-add patches.

### You want to allow docs

Add:

```json
"docs/"
```

to `allowed_path_prefixes`.

### You have sensitive project paths

Add them to `protected_path_prefixes`, for example:

```json
"billing/production/",
"deploy/prod/"
```

## Beginner Defaults To Avoid

Do not start by:

- setting `outside_allowed_action` to `allow`
- setting `protected_path_action` to `review` or `allow`
- setting `review_is_passing` to `true`
- setting `max_changed_files` very high

Start conservative. Keep AI patches small. Relax the rules later if your team needs it.

## Check That The Policy Works

After creating the policy:

```powershell
carves guard report
```

Then let AI make a small change and run:

```powershell
carves guard check --json
```
