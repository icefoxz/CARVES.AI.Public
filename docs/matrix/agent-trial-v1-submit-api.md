# CARVES Agent Trial V1 Submit API

Status: Phase 6 submit API contract freeze for the proposed Agent Trial V1 product line.

This document defines the V1 summary-only submission API. It does not implement authentication, persistence, hosted reruns, receipts, leaderboards, model identity verification, certification, semantic correctness proof, or operating-system sandboxing.

## Goal

The submit API accepts a summary-only Agent Trial result for server validation.

It answers:

```text
Can the server validate that this submitted result belongs to the registered user, issued challenge, exact pack/prompt versions, Matrix-covered trial artifacts, and allowed visibility?
```

It does not require source upload, raw diff upload, prompt response upload, model response upload, full log upload, or hosted rerun in V1.

Submission is the first V1 path that can move a result out of `local_only`. Authority modes and non-claims are frozen in `docs/matrix/agent-trial-v1-authority-boundary.md`.

## Endpoint Shape

Proposed V1 endpoint:

```text
POST /v1/agent-trial/results
```

Authentication is required.

The user must own the `challenge_id` in the submitted result.

## Request Shape

Minimum request body:

| Field | Required | Meaning |
| --- | --- | --- |
| `challenge_id` | yes | Server-issued challenge id |
| `visibility` | yes | `private`, `unlisted`, or `public` |
| `trial_result` | yes | `carves-agent-trial-result.v0` summary envelope |
| `summary_artifacts` | yes | Uploaded summary artifacts needed for validation |
| `client_submission_id` | no | Client idempotency key |

Required `summary_artifacts` roles:

- `matrix_artifact_manifest`;
- `matrix_proof_summary`;
- `matrix_verify`;
- `task_contract`;
- `agent_report`;
- `diff_scope_summary`;
- `test_evidence`;
- `trial_result_summary`;
- `shield_evidence`;
- `shield_evaluation`.

V1 default submission must not include raw source, raw diff, full logs, prompt response transcript, or model response transcript.

## Server Validation Steps

The server must validate:

1. Authenticated user owns the challenge.
2. User accepted current terms/privacy.
3. Challenge exists, is not expired, and is not already accepted as public.
4. Challenge AgentProfile snapshot hash matches the result snapshot or server-approved equivalent.
5. Submitted `suite_id`, `pack_id`, `pack_version`, `task_id`, `task_version`, `prompt_id`, and `prompt_version` match the issued challenge.
5a. Submitted task contract bytes match the expected task contract hash bound to the issued challenge or official pack version.
6. Result schema is `carves-agent-trial-result.v0`.
7. Result privacy flags are summary-only.
8. Required summary artifacts are present.
9. Uploaded artifact hashes match `trial_result.artifact_hashes`.
10. Matrix manifest and proof summary hashes match the submitted artifacts.
11. Strict Matrix trial verification or equivalent server-side validation passes over the uploaded summary artifact bundle.
12. Shield evaluation is bound to the submitted Shield evidence hash.
13. Visibility is one of `private`, `unlisted`, or `public`.
14. Public submissions satisfy leaderboard eligibility gates.

The server must treat submitted `.carves/trial/*` files and uploaded summary artifacts as candidate evidence until they are matched against server-issued challenge authority. It must not trust client-provided `authority_mode=leaderboard_eligible`, `verification_status`, `official_leaderboard_eligible=true`, or `leaderboard_eligibility` without recomputing eligibility.

If validation fails, the server should return structured rejection reasons. It must not silently repair mismatched hashes, versions, or missing artifacts.

## Matrix Verification Requirement

V1 can validate Matrix posture in either of two equivalent ways:

1. Run `carves-matrix verify <artifact-root> --trial` or the same strict trial verifier logic over the submitted summary artifact bundle.
2. Use a server-side verifier implementation that enforces the same manifest, hash, privacy, schema, consistency, and complete trial artifact coverage rules.

The server must not trust a client-provided `matrix_verify.status=verified` without rechecking the artifact bundle or equivalent verified bytes.

Submit validation is stricter than ordinary local Matrix verification. Ordinary `verify <artifact-root>` remains compatible with non-trial bundles and can report loose trial files as readback, but server submission must be equivalent to strict trial verification plus server-issued challenge, pack, prompt, identity, receipt, and visibility checks.

## Response Shape

Accepted response:

```json
{
  "schema_version": "agent-trial-submit-response.v0",
  "status": "accepted",
  "result_id": "tr_01",
  "challenge_id": "mch_01",
  "visibility": "public",
  "receipt": {
    "schema_version": "agent-trial-receipt.v0",
    "receipt_id": "rcpt_01",
    "receipt_sha256": "sha256:..."
  }
}
```

Rejected response:

```json
{
  "schema_version": "agent-trial-submit-response.v0",
  "status": "rejected",
  "challenge_id": "mch_01",
  "reason_codes": ["matrix_verify_failed", "trial_artifact_missing"],
  "issues": [
    {
      "code": "trial_test_evidence_missing",
      "message": "Required test evidence artifact was not submitted."
    }
  ]
}
```

## Visibility Rules

Visibility is respected at submission time:

- `private`: visible to the submitting user only;
- `unlisted`: visible by direct link when supported, not public leaderboard eligible;
- `public`: eligible for public leaderboards only after validation and anti-gaming gates.

Changing visibility later must not rewrite receipt inputs. The server may issue a visibility event, but the original receipt remains bound to the accepted hashes.

## Idempotency And Duplicate Handling

`client_submission_id` can make retry safe.

The server should detect:

- duplicate `client_submission_id` for the same user;
- duplicate `trial_result_sha256`;
- duplicate Matrix manifest hash under the same challenge;
- replayed result from another user or another challenge;
- second public accepted submission for the same challenge.

Duplicates should be flagged rather than silently accepted into public leaderboard aggregation.

## Privacy Boundary

Allowed default upload:

- summary result envelope;
- summary Matrix manifest and proof summary;
- task contract metadata;
- agent report summary;
- diff scope summary;
- test evidence summary;
- Shield evidence/evaluation summary artifacts;
- artifact hashes and bounded issue codes.

Forbidden default upload:

- private source files;
- raw diff text;
- full stdout/stderr logs;
- prompt response transcript;
- model response transcript;
- secrets;
- credentials;
- customer payloads;
- absolute local paths.

## Phase 6 Acceptance Mapping

TRIAL-V1-061 is satisfied by this document:

- the server validates user, challenge id, expiry, prompt version, task pack version, and result schema;
- the server must run Matrix verify or equivalent server-side validation over uploaded summary artifacts;
- private, unlisted, and public visibility are preserved and have separate leaderboard effects.
