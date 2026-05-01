# Evidence Starter: Prepare shield-evidence.v0

Shield reads `shield-evidence.v0` summary evidence.

Recommended path:

```text
.carves/shield-evidence.json
```

## Minimal Template

This template enables Guard only and keeps Handoff and Audit disabled. It is suitable for a first integration.

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

## Update Order

1. First confirm every `privacy.*_included` value is `false`.
2. Fill `repository.host` and `repository.visibility`.
3. Fill Guard policy fields from your actual setup.
4. If CI is wired, update `ci.detected` and workflow paths.
5. If you do not have decision records yet, keep `decisions.present=false`.
6. If you do not have Handoff or Audit evidence yet, keep those dimensions as `enabled=false`.

## Local Check

```powershell
carves shield evaluate .carves/shield-evidence.json --json --output combined
```

If the command returns `invalid_privacy_posture`, check whether the evidence file says source, raw diff, prompt, secret, or credential material was included.

## How Scores Improve

- Adding Guard to CI usually improves posture quickly.
- Sustained 30-day decision evidence is normally required for green levels.
- Enabling Handoff and Audit makes the Lite score more complete.
- If a Critical Gate fails, fix the Critical Gate before chasing points.
