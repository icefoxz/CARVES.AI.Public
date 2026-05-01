# CARVES Local Dist Freshness Smoke

This guide records the Phase 22 read-only smoke for a frozen local Runtime dist.

Use it before asking an external target repo to bind to or claim stable consumption from `D:\Projects\CARVES.AI\.dist\CARVES.Runtime-0.6.1-beta`.

## Commands

```powershell
carves pilot dist-smoke
carves pilot dist-smoke --json
carves pilot dist-freshness
carves pilot dist-freshness --json
```

Equivalent inspect/API surfaces:

```powershell
carves inspect runtime-local-dist-freshness-smoke
carves api runtime-local-dist-freshness-smoke
```

## What It Proves

The smoke is ready only when:

- the Runtime source repo has a detected git HEAD
- the Runtime source worktree is clean
- the candidate local dist exists
- the candidate local dist has `MANIFEST.json`, `VERSION`, and a wrapper
- the candidate local dist contains the Phase 22 smoke document, the current product closure document, the frozen-dist target readback proof document, and required guides
- `MANIFEST.json` records the same source commit as the Runtime source HEAD
- `MANIFEST.json` records the same output path as the candidate dist root
- the candidate local dist does not contain `.git` or `CARVES.Runtime.sln`

## Normal Operator Flow

From the Runtime source repo:

```powershell
.\scripts\pack-runtime-dist.ps1 -Version 0.6.1-beta -Force
carves pilot dist-smoke --json
```

If the smoke reports `local_dist_freshness_smoke_ready=true`, continue with:

```powershell
carves pilot dist-binding --json
carves pilot dist --json
carves pilot target-proof --json
carves pilot proof --json
```

From a dist wrapper, the equivalent readback is:

```powershell
& "<LocalDistRoot>\carves.ps1" pilot dist-smoke --json
```

## Agent Rules

- Do not edit files inside the dist folder manually.
- Do not claim the dist is current when `manifest_source_commit_matches_source_head=false`.
- Do not claim the dist is current while the source worktree is dirty.
- Do not skip `dist-smoke` and go directly to `dist-binding` when proving product closure.
- Follow `recommended_next_action` when the smoke reports a gap.

## Non-Claims

This guide does not create, refresh, publish, sign, retarget, stage, commit, push, or release anything. It is only a read-only freshness proof before target binding and local dist handoff.
