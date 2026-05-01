# AGENTS

This is the public CARVES.AI source repository.

## Default Entry

For repository work, read:

1. `README.md`
2. `START_CARVES.md`
3. `CARVES.Runtime.sln`

For using CARVES in another project, do not infer the runtime root from memory or PATH. Use the absolute path of `START_CARVES.md` from this repository.

## Public Repo Boundary

This repository is a public source snapshot. It intentionally excludes private development history, live `.ai` task/runtime truth, local Host state, Codex state, generated artifacts, and archive/checkpoint history.

Do not recreate private control-plane state in this repository.

## Safe Work Rules

- Do not claim a release was published unless a tag, release artifact, checksum, and hosted evidence exist.
- Do not claim signing or hosted verification unless those steps were actually performed.
- Do not treat local `carves gateway status` as worker execution authority.
- Do not write live `.ai` truth directly.
- Do not add generated `bin`, `obj`, `TestResults`, `.dist`, `.carves-platform`, `.codex`, logs, trials, or local package output.

## Build Check

Use:

```bash
dotnet build CARVES.Runtime.sln --configuration Release
```

Run focused tests only when the task needs them.
