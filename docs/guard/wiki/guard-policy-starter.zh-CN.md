# CARVES.Guard Policy 模板

把下面内容复制到你的项目：

```text
.ai/guard-policy.json
```

这个模板适合常见小型应用项目：

- 源码在 `src/`
- 测试在 `tests/`
- 允许改 `README.md`
- 依赖文件是 `package.json` 或 `*.csproj`
- lockfile 是 `package-lock.json` 或 `packages.lock.json`
- 一次 patch 默认最多 5 个文件

不理解字段时先看 [术语表](glossary.zh-CN.md)。

## 模板

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

## 小白怎么改

### 你的源码不在 `src/`

例如你的源码在 `app/`，把：

```json
"src/"
```

改成：

```json
"app/"
```

同时修改：

```json
"source_path_prefixes": [
  "app/"
]
```

### 你的测试不在 `tests/`

例如测试在 `test/`：

```json
"test_path_prefixes": [
  "test/"
]
```

并把 `allowed_path_prefixes` 里的 `tests/` 改成 `test/`。

### 你想更严格

把：

```json
"missing_tests_action": "review"
```

改成：

```json
"missing_tests_action": "block"
```

这样改源码但没改测试会直接 block。

### 你想限制 AI 一次只改 3 个文件

改：

```json
"max_changed_files": 3
```

### 你想删除源码文件

模板默认保留：

```json
"allow_delete_without_replacement": false
```

这表示一个 patch 删除文件时，必须同时提供可信的 replacement evidence。CARVES.Guard 不会把任意新增文件都当成 replacement。

目前接受的 replacement candidate 很窄：

- git rename 明确从被删路径移动过来；
- 新增文件与被删文件是同一路径；
- 新增文件与被删文件有相同文件名和扩展名；
- 新增文件与被删文件在同一目录、扩展名相同，并且文件名 stem 相关，例如 `old.ts` -> `old-v2.ts`。

如果 replacement 不确定，Guard 会用 `shape.delete_without_replacement` 阻止。这个规则不证明语义等价，只是避免“删了一个文件，又随便加了一个无关文件”被误判为安全替代。

### 你想允许文档

加：

```json
"docs/"
```

到 `allowed_path_prefixes`。

### 你有自己的敏感路径

把它放到 `protected_path_prefixes`。例如：

```json
"billing/production/",
"deploy/prod/"
```

## 不建议新手一开始做的事

不要一开始就：

- 把 `outside_allowed_action` 设成 `allow`。
- 把 `protected_path_action` 设成 `review` 或 `allow`。
- 把 `review_is_passing` 设成 `true`。
- 把 `max_changed_files` 设得很大。

建议先保守，让 AI patch 小一点。团队熟悉后再放宽。

## 检查模板有没有生效

创建 policy 后，跑：

```powershell
carves guard report
```

然后让 AI 做一个小改动，再跑：

```powershell
carves guard check --json
```
