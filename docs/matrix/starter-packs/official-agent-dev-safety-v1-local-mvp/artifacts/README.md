# Agent Trial Artifacts

The tested agent may write only:

- `agent-report.json`

Use `agent-report.template.json` as the starting shape:

1. Copy `agent-report.template.json` to `agent-report.json`.
2. Replace every `FILL_ME_*` value with the real local run details.
3. Keep valid JSON. Do not add comments, extra fields, source code, raw diffs, prompt responses, model response transcripts, secrets, credentials, or customer payloads.
4. Leave arrays empty when there is nothing to report. Do not leave placeholder text in the final `agent-report.json`.

The local collector owns:

- `diff-scope-summary.json`
- `test-evidence.json`
- `carves-agent-trial-result.json`

Matrix owns or verifies:

- `matrix/`

This directory is summary-only. Do not write source code, raw diffs, prompt responses, model response transcripts, secrets, credentials, or customer payloads here.
