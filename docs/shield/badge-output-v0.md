# CARVES Shield Badge Output v0

`carves shield badge` renders a local static SVG badge from `shield-evidence.v0`.

It uses the same local evaluator as `carves shield evaluate`, so the badge inherits the same privacy boundary:

- no source upload
- no raw diff upload
- no prompt or model response upload
- no secret or credential upload
- no private file payload upload
- no hosted badge service
- no public directory
- no certification claim

## Command

```powershell
carves shield badge <evidence-path> [--json] [--output <svg-path>]
```

Behavior:

- without `--output`, the command writes SVG to stdout
- with `--output`, the command writes a local SVG file and prints a short text summary
- with `--json`, the command emits `shield-badge.v0` metadata
- invalid evidence returns non-zero and does not produce a positive badge

## Badge Semantics

The visible badge message is derived from Shield Lite:

```text
CARVES Shield | 90/100 Strong
```

The badge metadata keeps the Standard dimensions:

```text
G8.H8.A8
```

The badge always remains a self-check in v0:

```json
{
  "self_check": true,
  "certification": false
}
```

## Color Mapping

```text
critical     red
strong       green
disciplined  yellow
basic        white
no_evidence  gray
```

## Example

```powershell
carves shield badge docs/shield/examples/shield-evidence-standard.example.json --output docs/shield-badge.svg
```

To inspect metadata instead:

```powershell
carves shield badge docs/shield/examples/shield-evidence-standard.example.json --json
```

For CI usage, see [CARVES Shield GitHub Actions Proof v0](github-actions-proof-v0.md).

For reader-facing badge guidance, see [Shield Badge Guide](wiki/badge.en.md) or [Badge 解读与发布](wiki/badge.zh-CN.md).
