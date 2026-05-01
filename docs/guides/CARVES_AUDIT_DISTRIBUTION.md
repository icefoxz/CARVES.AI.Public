# CARVES Audit Distribution

CARVES Audit is the read-only evidence discovery layer for Guard decisions and Handoff packets.

It answers three questions:

- What Guard decisions can be read?
- What Handoff packet can be read?
- Can those inputs be summarized into safe `shield-evidence.v0` for local Shield evaluation?

Audit does not score Shield levels, render badges, upload data, create a compliance archive, or mutate Guard/Handoff files.

## Status

CARVES Audit is currently an Alpha local-package pilot.

It is not published to NuGet.org in this phase. It is not signed. NuGet.org publication and signing remain operator gates.

## Packages

| Package | Version | Role |
| --- | --- | --- |
| `CARVES.Audit.Core` | `0.1.0-alpha.1` | Portable read-only discovery and evidence core. |
| `CARVES.Audit.Cli` | `0.1.0-alpha.1` | `carves-audit` dotnet tool. |

Prepared package metadata:

- `PackageLicenseExpression`: `Apache-2.0`
- `PackageProjectUrl`: `https://github.com/CARVES-AI/CARVES.Runtime`
- `RepositoryUrl`: `https://github.com/CARVES-AI/CARVES.Runtime`
- `PackageReadmeFile`: `CARVES_AUDIT_DISTRIBUTION.md`

## Default Discovery

From an external repository root, Audit looks for:

```text
.ai/runtime/guard/decisions.jsonl
.ai/handoff/handoff.json
```

Missing default files are not fatal. Malformed default files produce degraded readback. Explicit paths supplied through CLI options still fail closed when missing or unreadable.

## Local Tool Install

Build local packages:

```powershell
$packageRoot = Join-Path $env:TEMP "carves-audit-packages"
dotnet pack .\src\CARVES.Audit.Core\Carves.Audit.Core.csproj -c Release -o $packageRoot
dotnet pack .\src\CARVES.Audit.Cli\Carves.Audit.Cli.csproj -c Release -o $packageRoot
```

Install into an explicit tool path:

```powershell
$toolRoot = Join-Path $env:TEMP "carves-audit-tool"
dotnet tool install CARVES.Audit.Cli --tool-path $toolRoot --add-source $packageRoot --version 0.1.0-alpha.1 --ignore-failed-sources
```

Run the installed tool from a target repository:

```powershell
carves-audit summary --json
carves-audit timeline --json
carves-audit explain <run-id> --json
carves-audit evidence --json --output .carves/shield-evidence.json
```

## Local Smoke

The local packaged install smoke proves pack, install, default discovery, and evidence generation without NuGet.org:

```powershell
.\scripts\audit\audit-packaged-install-smoke.ps1
```

The smoke validates:

- `summary`
- `timeline`
- `explain`
- `evidence`
- output file `.carves/shield-evidence.json`

## Boundary

Audit reads discovered or supplied input paths only.

It does not:

- compute Shield G/H/A levels
- compute Lite score
- render badges
- upload evidence
- write Guard decisions
- write Handoff packets
- create an Audit database
- claim complete historical retention

The `evidence` command writes only when `--output <path>` is explicitly supplied. The generated evidence is summary-first and keeps privacy flags set to:

```json
{
  "source_included": false,
  "raw_diff_included": false,
  "prompt_included": false,
  "secrets_included": false
}
```

## Completeness

Audit Alpha is complete for local evidence discovery over the default Guard and Handoff locations.

It does not prove that every AI action in a repository has been captured. It only reports what can be read from the discovered or explicitly supplied inputs.
