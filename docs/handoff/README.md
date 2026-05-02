# CARVES.Handoff Docs

Language: [中文](README.zh-CN.md)

CARVES.Handoff is a CARVES Runtime capability for AI session continuity packets. It helps an outgoing session leave the current objective, completed facts, remaining work, must-not-repeat notes, and next guidance in an inspectable JSON packet.

## Recommended Reading Order

1. [Five-minute quickstart](quickstart.en.md)
2. [Distribution guide](../guides/CARVES_HANDOFF_DISTRIBUTION.md)
3. [Publish checkpoint](../release/handoff-publish-checkpoint.md)

## Default Path

```text
.ai/handoff/handoff.json
```

Shortest path:

```powershell
carves-handoff draft --json
carves-handoff inspect --json
carves-handoff next --json
```

Default repo root:

- Without `--repo-root`, standalone `carves-handoff` walks upward from the current directory to the nearest git repository.
- If no git repository is found, it uses the current directory.
- Pass `--repo-root <path>` when the target repository must be explicit.

## Boundary

Handoff is session continuity, not a planner and not a memory database. It does not own Guard or Audit truth, mutate Guard decisions, create long-term memory, or decide whether the next action should execute.
