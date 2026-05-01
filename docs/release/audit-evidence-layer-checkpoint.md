# Audit Evidence Layer Checkpoint

Status: ready as the matrix evidence discovery layer for the public source snapshot.

Audit reads local Guard decision history and Handoff packets, then writes summary evidence for Shield and Matrix. The public path keeps evidence local and does not publish source, raw diffs, prompts, secrets, credentials, hosted verification, public leaderboard entries, certification, or operating-system sandbox claims.

Primary public command:

```bash
carves-audit evidence --json --output .carves/shield-evidence.json
```

NuGet.org publication, release tags, hosted artifacts, and package signing remain operator-owned release steps.
