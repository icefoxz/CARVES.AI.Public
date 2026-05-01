# CARVES Agent Trial V1 Local Diagnostics

Status: CARD-913 friendly local diagnostics implemented for offline Agent Trial commands.

Local diagnostics translate existing collector and verifier reason codes into stable user-facing guidance. They do not replace Matrix verification, artifact hashes, schemas, or the manifest-covered JSON evidence.

## Output

`carves-matrix trial prepare|collect|local|verify --json` can include:

```json
{
  "diagnostics": [
    {
      "code": "trial_required_command_failed",
      "category": "agent_behavior",
      "severity": "error",
      "message": "A required command failed. The local score may be capped or unavailable, and this is treated as agent behavior evidence.",
      "evidence_ref": "artifacts/test-evidence.json",
      "command_ref": "required_commands",
      "next_step": "Run the required command yourself, inspect artifacts/test-evidence.json, and fix the task output rather than removing the required command.",
      "reason_codes": ["required_command_failed"]
    }
  ]
}
```

Text output prints the same diagnostics after collection and verification readback.

## Categories

Diagnostics use three categories:

- `user_setup`: missing pack, missing report, unavailable local command/tooling, or workspace setup problem;
- `agent_behavior`: the agent output or required command behavior failed the local task;
- `evidence_integrity`: manifest, hash, schema, pin, consistency, or trial artifact integrity failed.

Integrity failures remain errors. They must not be downgraded to warnings.

## Stable Codes

Current stable local diagnostic codes include:

- `trial_setup_pack_missing`;
- `trial_setup_workspace_not_empty`;
- `trial_agent_report_missing`;
- `trial_agent_report_schema_invalid`;
- `trial_instruction_pack_missing`;
- `trial_task_contract_pin_mismatch`;
- `trial_instruction_pack_pin_mismatch`;
- `trial_required_command_failed`;
- `trial_required_command_timed_out`;
- `trial_required_command_unavailable`;
- `trial_diff_scope_unavailable`;
- `trial_verify_failed`;
- `trial_verify_hash_mismatch`;
- `trial_verify_missing_artifact`;
- `trial_verify_schema_invalid`;
- `trial_verify_consistency_mismatch`.

Unknown collector reasons are surfaced as `trial_collection_issue` and still preserve their underlying `reason_codes`.

## Privacy Boundary

Diagnostic fields must use portable evidence references:

- `--pack-root`;
- `--workspace`;
- `.carves/trial/task-contract.json`;
- `.carves/trial/instruction-pack.json`;
- `artifacts/agent-report.json`;
- `artifacts/test-evidence.json`;
- `artifacts/diff-scope-summary.json`;
- `artifacts/carves-agent-trial-result.json`;
- `matrix-artifact-manifest.json`;
- `trial/`.

Diagnostic messages and next steps must not include private workspace roots, home directories, temp roots, secrets, source snippets, raw diffs, prompt responses, or model responses.

## Common Failures

| Failure | Category | Stable code | Evidence |
| --- | --- | --- | --- |
| Missing starter pack | `user_setup` | `trial_setup_pack_missing` | `--pack-root` |
| Self-edited task contract | `evidence_integrity` | `trial_task_contract_pin_mismatch` | `.carves/trial/task-contract.json` |
| Required command failed | `agent_behavior` | `trial_required_command_failed` | `artifacts/test-evidence.json` |
| Manifest-covered artifact hash mismatch | `evidence_integrity` | `trial_verify_hash_mismatch` | `matrix-artifact-manifest.json` |

## Non-Claims

Diagnostics do not claim:

- server acceptance;
- leaderboard eligibility;
- certification;
- hosted rerun validation;
- tamper-proof local execution;
- that a user can repair a failed run by weakening the task contract.

## CARD-913 Acceptance Mapping

`CARD-913` is satisfied by the current implementation:

- common failures produce stable diagnostic codes and readable messages;
- diagnostics distinguish `user_setup`, `agent_behavior`, and `evidence_integrity`;
- diagnostics reference portable artifacts or command groups without private paths;
- tests cover missing pack, mutated contract, failed required command, and verify hash mismatch.
