# Handoff Publish Checkpoint

Status: Handoff is ready for local continuity packet publishing in this public source snapshot.

The public Handoff surface uses `.ai/handoff/handoff.json` as the default local packet path. It supports draft, inspect, and next-action projection for bounded continuity between coding sessions.

This checkpoint is public release evidence only. It does not expose Runtime planning internals, hosted verification, public leaderboard claims, certification, or operating-system sandbox claims.

Representative commands:

```bash
carves-handoff draft --json
carves-handoff inspect --json
carves-handoff next --json
```

NuGet.org publication and package signing remain operator-owned release steps.
