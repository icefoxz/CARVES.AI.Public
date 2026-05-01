# CARVES Agent Trial V1 Local Result Card

Status: CARD-912 human-readable local result card implemented for offline Agent Trial commands.

The local result card is a derived Markdown summary for humans. It is not the source of truth and it does not replace Matrix verification.

## Output

`carves-matrix trial collect` and `carves-matrix trial local` write:

```text
<bundle-root>/matrix-agent-trial-result-card.md
```

`carves-matrix trial collect --json`, `carves-matrix trial local --json`, and `carves-matrix trial verify --json` also include a `result_card` object in command output.

The card uses only bundle-relative evidence references:

- `trial/carves-agent-trial-result.json`;
- `matrix-artifact-manifest.json`;
- `matrix-proof-summary.json`.

The card must not include private workspace roots, local home directories, source snippets, raw diffs, prompt responses, model responses, secrets, credentials, or customer payloads.

## Required Labels

Every card must clearly label the result as:

- local-only;
- unsubmitted;
- non-certified.

The card also states:

```text
Source of truth: manifest-covered JSON evidence, not this card.
```

## Required Sections

The current card includes:

- title;
- labels;
- source-of-truth line;
- local score and scoring profile;
- collection status;
- Matrix verification status;
- dimension score lines;
- evidence links;
- non-claims.

## Non-Claims

The result card must not claim:

- server acceptance;
- public leaderboard eligibility;
- certification;
- general model intelligence;
- production safety;
- tamper-proof local execution.

## Example

```markdown
# CARVES Agent Trial Local Result

Labels: Local-only, unsubmitted, non-certified.
Source of truth: manifest-covered JSON evidence, not this card.

Final result: GREEN VERIFIED
Final score: GREEN 100/100 (scored)
Local dimension score: GREEN 100/100 (scored)
Collection: collectable
Verification: verified; trial_artifacts_verified=true

Dimension Scores:

Color bands: RED 0-4, YELLOW 5-8, GREEN 9-10.

| Dimension | Score | Status |
| --- | --- | --- |
| Review | GREEN 10/10 | OK |

Evidence:
- [trial/carves-agent-trial-result.json](trial/carves-agent-trial-result.json)
- [matrix-artifact-manifest.json](matrix-artifact-manifest.json)
- [matrix-proof-summary.json](matrix-proof-summary.json)

Non-claims:
- This is not a certification.
- This is not a server-accepted leaderboard result.
- This is not a general intelligence or production-safety score.
- This does not prove the local machine was tamper-proof.
```

## CARD-912 Acceptance Mapping

`CARD-912` is satisfied by the current implementation:

- local trial output includes `result_card`;
- collect/local write `matrix-agent-trial-result-card.md`;
- the card includes score, dimensions, evidence posture, and non-claims;
- evidence links point to manifest-covered JSON artifacts instead of replacing them;
- local-only, unsubmitted, and non-certified labels are explicit;
- tests pin the card path, labels, source-of-truth language, score readback, evidence link, and no workspace-root leakage.
