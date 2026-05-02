# GitHub Actions Integration

Language: [Chinese](github-actions.zh-CN.md)

Shield can run as pull request CI proof. It reads `shield-evidence.v0` summary evidence and uploads locally generated JSON/SVG artifacts.

## Recommended Files

```text
.carves/shield-evidence.json
.github/workflows/carves-shield.yml
```

## Copyable Workflow

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

      - name: Install CARVES CLI
        run: dotnet tool install --global CARVES.Runtime.Cli --version 0.2.0-beta.1

      - name: Prepare Shield artifact directory
        run: mkdir -p artifacts/shield

      - name: Shield evaluate
        run: carves shield evaluate .carves/shield-evidence.json --json --output combined | tee artifacts/shield/shield-evaluate.json

      - name: Shield badge
        run: carves shield badge .carves/shield-evidence.json --json --output artifacts/shield/shield-badge.svg | tee artifacts/shield/shield-badge.json

      - name: Upload Shield artifacts
        uses: actions/upload-artifact@v4
        with:
          name: shield-proof
          path: artifacts/shield
          if-no-files-found: error
```

## Produced Artifacts

```text
artifacts/shield/shield-evaluate.json
artifacts/shield/shield-badge.json
artifacts/shield/shield-badge.svg
```

These artifacts are meant for reviewers. They should not contain source code, raw diffs, prompts, model responses, secrets, or credentials.

## When CI Fails

`carves shield evaluate` returns non-zero when:

- the evidence file is missing
- JSON is invalid
- schema version is unsupported
- privacy posture is invalid
- evidence declares default-forbidden sensitive material

`carves shield badge` also returns non-zero for invalid evidence and does not write a positive badge.

## What Is Not Required

Shield v0 CI proof does not require:

- provider secrets
- hosted API key
- network call
- source upload
- raw diff upload
- prompt upload
- model response upload
- secret upload
- credential upload
- public directory listing
- certification claim

## Private Repository Guidance

Private repositories can use `repo_id_hash` in evidence instead of placing private owner/name data in public artifacts.

## Publishing A Badge

If you want to place the badge in README, first download `shield-badge.svg` from CI artifacts and confirm that the result and evidence match your expectation.

Badge wording must remain self-check wording. Do not present it as verified or certified.
