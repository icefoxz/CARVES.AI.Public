# Public Export Boundary

This repository was initialized as a clean public source snapshot from the private CARVES.Runtime operating repository.

## Included

- Source code under `src/`.
- Tests under `tests/`, excluding generated build output.
- Build and release helper scripts under `scripts/`.
- Public product documentation under `docs/audit`, `docs/contracts`, `docs/guard`, `docs/handoff`, `docs/matrix`, `docs/product`, `docs/shield`, selected `docs/guides`, selected `docs/runtime`, selected `docs/session-gateway`, and selected `docs/release`.
- Root startup files and project metadata.
- `.ai/PROJECT_BOUNDARY.md` only.

## Excluded

- Private git history.
- `.ai/tasks`, `.ai/runtime`, `.ai/memory`, `.ai/artifacts`, `.ai/execution`, `.ai/failures`, and other live control-plane truth.
- `.carves-platform` runtime state.
- `.codex` and `.codex-temp`.
- `docs/archive`.
- Runtime checkpoint and phase-history bulk docs.
- Generated artifacts, logs, trials, local packages, build outputs, and release archives.

## Validation Goal

The first public snapshot must at minimum build from source:

```bash
dotnet build CARVES.Runtime.sln --configuration Release
```

Full public release readiness still requires tag, release asset, checksum, signing decision, hosted verification decision, and public release notes.
