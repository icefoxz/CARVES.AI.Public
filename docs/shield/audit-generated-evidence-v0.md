# Audit-Generated Shield Evidence v0

`carves-audit evidence` can generate a local `shield-evidence.v0` document from default Guard and Handoff outputs.

```powershell
carves-audit evidence --json --output .carves/shield-evidence.json
carves shield evaluate .carves/shield-evidence.json --json --output combined
```

Audit is the collector only. Shield remains the evaluator.

The generated evidence is summary-first and keeps the default privacy posture:

```json
{
  "source_included": false,
  "raw_diff_included": false,
  "prompt_included": false,
  "secrets_included": false,
  "upload_intent": "local_only"
}
```

Audit-generated evidence does not include source code, raw diffs, prompts, model responses, secrets, credentials, or private file payloads.

When Audit has not observed stronger scoring evidence, it writes conservative values rather than inferred positives:

- no append-only proof becomes `append_only_claimed=false`;
- missing explain coverage becomes covered counts of `0`;
- missing report artifacts become report booleans of `false`.

Shield treats these values as insufficient evidence for higher Audit levels. This is expected and prevents a readable local log from being mistaken for full historical governance.
