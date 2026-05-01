# CARVES Matrix Continuous Challenge Notes

This note records a future design direction. It is not implemented by the current local Matrix verifier, and it does not create hosted verification, public certification, public ranking, model safety benchmarking, semantic correctness proof, or operating-system sandboxing.

## Plain-Language Goal

The continuous challenge idea is not to prove that an AI model is internally stable.

It asks a narrower question:

```text
Can the same AI coding workflow repeatedly produce verifiable governance evidence under fresh challenges?
```

In practice, the system would compare the first submitted score with later verified scores. A pattern like this is easy to understand:

```text
Day 1: G9.H9.A9 = 100
Day 2: G4.H2.A1 = 30
Day 3: G4.H2.A1 = 30
```

That does not mathematically prove cheating, but it is a strong reproducibility signal. A one-time high score that cannot be reproduced under daily challenge should be displayed as an initial spike, not as the current trustworthy posture.

The comparison is only direct when the prompt, pack, task, scoring profile, collector, and verifier version posture match. If any of those dimensions change, the same Day 1 / Day 2 display becomes trend-only until a future server normalization process explicitly relates the versions.

## What Is Being Measured

The measured object is the workflow, not the raw model.

The workflow includes:

- the target repository state;
- the AI or human process used to make changes;
- Guard policy and decision output;
- Handoff packet quality;
- Audit evidence discovery;
- Shield G/H/A and Lite score;
- Matrix artifact integrity;
- the continuity receipt chain across days.

This means the score answers:

```text
Did this repo and workflow keep producing the same kind of governance evidence?
```

It does not answer:

```text
Is the AI model generally safe?
Did the generated code have correct semantics?
Was the local machine impossible to tamper with?
```

## How A Challenge Gives A Task

A future hosted or coordinator-driven challenge should issue a small challenge descriptor. The descriptor should be bound to the project, day, previous receipt, and commit or baseline ref.

Example shape:

```json
{
  "schema_version": "matrix-continuity-challenge.v0",
  "challenge_id": "mcc_20260416_project_01",
  "project_id": "project_01",
  "day": 2,
  "previous_receipt_sha256": "sha256:...",
  "target": {
    "repo_id": "repo_hash_or_provider_id",
    "baseline_ref": "main",
    "commit_sha": "abc123"
  },
  "task": {
    "kind": "governed_edit",
    "prompt_id": "bounded_change_001",
    "instruction": "Make a small governed change and preserve the CARVES evidence chain.",
    "allowed_paths": ["src/", "tests/", "docs/"],
    "forbidden_paths": [".git/", ".env", "secrets/"],
    "required_commands": [
      "carves-matrix proof --lane native-minimal --json",
      "carves-matrix verify <artifact-root> --json"
    ]
  },
  "requested_readback": {
    "matrix_manifest_sha256": true,
    "matrix_proof_summary_sha256": true,
    "shield_evidence_sha256": true,
    "shield_evaluate_sha256": true,
    "standard_label": true,
    "lite_score": true
  }
}
```

There are three useful task levels:

1. Governance rerun: no product edit is required. The project reruns Matrix proof and verify against the current repository state.
2. Bounded edit: the challenge gives a small issue-like change, allowed paths, forbidden paths, and required tests. CARVES then records whether the AI-assisted workflow stayed governed.
3. Hidden probe: the coordinator requests randomized readback or extra probes so a static fake bundle is harder to reuse.

The current Matrix local proof mostly supports the first shape. The second and third shapes need a separate challenge/receipt implementation.

## How A Result Is Asserted

Hard assertions should be mechanical:

- the submitted challenge id matches the issued challenge;
- the previous receipt matches the stored previous receipt;
- the submitted manifest hash matches the received bundle;
- Matrix verify passes;
- Shield score is recomputed from `shield-evidence.v0` or read from a verified `shield-evaluate.json`;
- `shield-evaluate.json.consumed_evidence_sha256` matches the included `shield-evidence.json`;
- the result is bound to project id, day, commit, tool version, and challenge id;
- the server or coordinator signs a receipt for the exact submitted hashes.

Soft assertions are trend labels:

- `stable_verified`: current score is close to the rolling median and challenges pass.
- `improving`: recent verified scores trend upward.
- `regressed`: current verified score dropped materially.
- `initial_spike`: first score is much higher than later verified median.
- `non_reproducible_high_score`: historical best cannot be reproduced under fresh challenges.
- `challenge_failed`: challenge was not answered or verify failed.

These labels should not accuse the user of cheating. They should describe reproducibility.

## Suggested Continuity Fields

A daily record should preserve the current score and the evidence chain:

```json
{
  "schema_version": "matrix-continuity-receipt.v0",
  "project_id": "project_01",
  "day": 2,
  "challenge_id": "mcc_20260416_project_01",
  "commit_sha": "abc123",
  "standard_label": "CARVES G4.H2.A1 /1d PASS",
  "lite_score": 30,
  "matrix_manifest_sha256": "sha256:...",
  "matrix_proof_summary_sha256": "sha256:...",
  "shield_evidence_sha256": "sha256:...",
  "shield_evaluate_sha256": "sha256:...",
  "previous_receipt_sha256": "sha256:...",
  "receipt_sha256": "sha256:..."
}
```

The display score should prefer current or rolling verified posture rather than historical best:

```text
Initial: 100
Current: 30
7-day median: 30
Trend: initial_spike
Version scope: same prompt/pack/task/scoring profile
Verified days: 6/7
```

This makes a one-time high score visible but not authoritative.

## Trust Boundary

This design can catch or expose low-cost cheating:

- edited JSON;
- replayed old bundles;
- mismatched Shield evidence and evaluation;
- one-time high-score screenshots;
- first-day fake evidence that cannot be reproduced later.

It still cannot defeat a determined local attacker who can continuously generate fake but self-consistent evidence. Stronger claims require server-side reruns, official toolchain pinning, hidden probes, runner provenance, signatures, and eventually transparency logging.

The right positioning is:

```text
continuous reproducibility signal
anti-replay evidence chain
tamper-evident submission history
```

The wrong positioning is:

```text
certification
tamper-proof proof
AI model safety benchmark
semantic correctness proof
```
