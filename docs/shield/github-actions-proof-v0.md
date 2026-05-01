# CARVES Shield GitHub Actions Proof v0

`scripts/shield/shield-github-actions-proof.ps1` is the local CI proof lane for CARVES Shield.

It runs:

```powershell
carves-shield evaluate <evidence-path> --json --output combined
carves-shield badge <evidence-path> --json --output <svg-path>
```

Compatibility wrapper:

```powershell
carves shield evaluate <evidence-path> --json --output combined
carves shield badge <evidence-path> --json --output <svg-path>
```

The proof lane writes local artifacts under `artifacts/shield/`:

- `shield-evaluate.json`
- `shield-badge.json`
- `shield-badge.svg`
- `shield-github-actions-proof.json`

The proof lane is self-check only. It does not claim verified certification.

## GitHub Actions Wiring

This repository runs the proof lane in `.github/workflows/ci.yml` after the Release build:

```yaml
- name: Shield GitHub Actions proof lane
  shell: pwsh
  run: ./scripts/shield/shield-github-actions-proof.ps1 -Configuration Release -SkipBuild

- name: Upload Shield proof artifacts
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: shield-proof-${{ matrix.os }}
    path: artifacts/shield
    if-no-files-found: warn
```

## CI Safety Posture

The proof lane declares these values in `shield-github-actions-proof.json`:

```json
{
  "provider_secrets_required": false,
  "hosted_api_required": false,
  "network_calls_required": false,
  "source_upload_required": false,
  "raw_diff_upload_required": false,
  "prompt_upload_required": false,
  "secret_upload_required": false,
  "credential_upload_required": false,
  "public_directory_required": false,
  "certification_claimed": false
}
```

## External Project Usage

For an external project, keep the same shape:

```yaml
name: carves-shield

on:
  pull_request:
  push:
    branches: [ main ]

jobs:
  shield:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - name: Install CARVES Shield CLI
        run: dotnet tool install --global CARVES.Shield.Cli --version 0.1.0-alpha.1
      - name: Shield evaluate
        run: carves-shield evaluate .carves/shield-evidence.json --json --output combined > shield-evaluate.json
      - name: Shield badge
        run: carves-shield badge .carves/shield-evidence.json --json --output shield-badge.svg > shield-badge.json
      - name: Upload Shield artifacts
        uses: actions/upload-artifact@v4
        with:
          name: shield-proof
          path: |
            shield-evaluate.json
            shield-badge.json
            shield-badge.svg
```

Keep the evidence file as summary evidence. Do not upload source code, raw diffs, prompts, model responses, secrets, credentials, or private file payloads.

## Local Run

From this repository:

```powershell
dotnet restore CARVES.Runtime.sln
./scripts/shield/shield-github-actions-proof.ps1 -Configuration Debug
```

The command returns `shield-github-actions-proof.v0` JSON and writes the local artifacts.

For a beginner-friendly workflow guide, see [Shield GitHub Actions integration](wiki/github-actions.en.md) or [GitHub Actions 接入](wiki/github-actions.zh-CN.md).
