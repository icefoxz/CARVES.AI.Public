# 证据模板：如何准备 shield-evidence.v0

语言：[En](evidence-starter.en.md)

Shield 读取的是 `shield-evidence.v0` 摘要证据。

推荐路径：

```text
.carves/shield-evidence.json
```

## 最小模板

下面这个模板只启用 Guard 维度，Handoff 和 Audit 先关闭。适合第一次接入。

```json
{
  "schema_version": "shield-evidence.v0",
  "evidence_id": "my-project-first-shield-check",
  "generated_at_utc": "2026-04-14T00:00:00Z",
  "mode_hint": "both",
  "sample_window_days": 30,
  "repository": {
    "host": "github",
    "visibility": "private"
  },
  "privacy": {
    "source_included": false,
    "raw_diff_included": false,
    "prompt_included": false,
    "secrets_included": false,
    "redaction_applied": true,
    "upload_intent": "local_only"
  },
  "dimensions": {
    "guard": {
      "enabled": true,
      "policy": {
        "present": true,
        "schema_valid": true,
        "effective_protected_path_prefixes": [".git/", ".env", "secrets/"],
        "protected_path_action": "block",
        "outside_allowed_action": "review",
        "fail_closed": true,
        "change_budget_present": true,
        "dependency_policy_present": false,
        "source_test_rule_present": false,
        "mixed_feature_refactor_rule_present": false
      },
      "ci": {
        "detected": false,
        "workflow_paths": [],
        "guard_check_command_detected": false,
        "fails_on_review_or_block": false
      },
      "decisions": {
        "present": false,
        "window_days": 30,
        "allow_count": 0,
        "review_count": 0,
        "block_count": 0,
        "unresolved_review_count": 0,
        "unresolved_block_count": 0
      },
      "proofs": []
    },
    "handoff": {
      "enabled": false
    },
    "audit": {
      "enabled": false
    }
  },
  "provenance": {
    "producer": "manual",
    "producer_version": "0.0.0",
    "generated_by": "local",
    "source": "manual-first-check",
    "warnings": []
  }
}
```

## 修改顺序

1. 先确保 `privacy.*_included` 都是 `false`。
2. 填写 `repository.host` 和 `repository.visibility`。
3. 根据实际情况填写 Guard policy。
4. 如果 CI 已接入，再把 `ci.detected` 和 workflow 路径改成真实值。
5. 如果还没有决策记录，保持 `decisions.present=false`。
6. 暂时没有 Handoff 或 Audit 时，保持 `enabled=false`。

## 本地检查

```powershell
carves shield evaluate .carves/shield-evidence.json --json --output combined
```

如果返回 `invalid_privacy_posture`，先检查证据文件是否错误地声明了 source、raw diff、prompt、secret 或 credential 被包含。

## 什么时候能拿到更高分

- Guard 接入 CI 后通常会明显提升。
- 有持续 30 天的决策证据，才有机会进入绿色区间。
- Handoff 和 Audit 维度启用后，Lite 分数会更完整。
- Critical Gate 失败时，先修 Critical Gate，不要追分。
