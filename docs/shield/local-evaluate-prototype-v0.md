# CARVES Shield Local Evaluate Prototype v0

`carves-shield evaluate` is the local-only prototype for checking `shield-evidence.v0` summary evidence.

The combined `carves` tool keeps `carves shield evaluate` as a compatibility wrapper.

It does not upload source code, raw diffs, prompts, model responses, secrets, credentials, or private file payloads. It also does not implement hosted API behavior, badge rendering, public directory listing, authentication, billing, persistence, or certification.

## Command

```powershell
carves-shield evaluate <evidence-path> [--json] [--output <lite|standard|combined>]
```

Compatibility wrapper:

```powershell
carves shield evaluate <evidence-path> [--json] [--output <lite|standard|combined>]
```

Output modes:

- `combined` emits Standard G/H/A and Lite results. This is the default.
- `standard` emits only the Standard G/H/A projection.
- `lite` emits only the Lite score projection.

Exit codes:

- `0`: evidence is supported, privacy posture is valid, and evaluation completed.
- `1`: evidence is unsupported, invalid, missing, or has an invalid privacy posture.
- `2`: command usage is invalid.

## Result Contract

The JSON result uses `shield-evaluate.v0`.

Top-level statuses:

- `ok`: local self-check completed.
- `unsupported_schema`: evidence is not `shield-evidence.v0`.
- `invalid_privacy_posture`: evidence indicates source, raw diff, prompt, secret, credential, model response, private payload, or unsupported upload intent.
- `invalid_input`: evidence file is missing, unreadable, empty, or malformed JSON.

The prototype always returns `certification=false`. A local self-check is useful evidence, not a verified public certification.

## Example

```powershell
carves-shield evaluate docs/shield/examples/shield-evidence-standard.example.json --json
```

The standard example projects separate Guard, Handoff, and Audit levels, then derives a Lite score from those levels using the v0 Lite model.

## Local Badge

CARD-762 adds local static badge output on top of this evaluator:

```powershell
carves-shield badge docs/shield/examples/shield-evidence-standard.example.json --output shield-badge.svg
```

The badge remains a self-check artifact. It is not hosted verification or certification.
