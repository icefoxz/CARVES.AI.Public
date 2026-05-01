# Runtime Protected Truth-Root Policy

This document records the official truth roots that cannot be treated as ordinary workspace or worker-returned files.

The policy is vendor-agnostic. It is enforced through Runtime surfaces, Mode E review preflight, managed workspace mutation audit, and review/writeback checks.

## Protected Roots

| Root | Classification | Allowed mutation channel | Unauthorized outcome |
| --- | --- | --- | --- |
| `.ai/tasks/` | `task_truth` | planning, task, review, markdown-sync Runtime commands | block before writeback |
| `.ai/memory/` | `memory_truth` | governed memory update or explicit operator-approved memory edit | block before writeback |
| `.ai/artifacts/reviews/` | `review_truth` | planner review artifact creation and review lifecycle commands | block before writeback |
| `.carves-platform/` | `platform_truth` | Runtime platform registry/governance commands | block before writeback |

## Denied Roots

| Root | Classification | Unauthorized outcome |
| --- | --- | --- |
| `.git/` | `vcs_internal` | deny without review or writeback |
| `.vs/` | `machine_local_state` | deny without review or writeback |
| `.idea/` | `machine_local_state` | deny without review or writeback |
| secret-like paths | `secret_material` | deny without review or writeback |

Secret-like paths include `.env`, `.pfx`, `.snk`, `secrets.json`, and paths under a `secrets/` segment.

## Review/Writeback Rule

Worker or adapter returned material that touches a protected root must be blocked before official writeback.

The operator surface must show:

- violation path
- protected classification
- required remediation action

Runtime-owned commands may still update protected roots through their governed write channels. The policy blocks unauthorized direct mutation, not Runtime itself.

## Non-Claims

This policy does not classify all repository files as protected truth.

This policy does not depend on a specific IDE, Codex setting, Claude setting, ACP implementation, or MCP implementation.
