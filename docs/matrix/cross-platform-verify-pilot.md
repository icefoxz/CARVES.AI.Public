# CARVES Matrix Cross-Platform Verify Pilot

Status: local Matrix pilot verification lane.

This lane validates the external pilot catalog on Windows and Linux without requiring source upload, raw diff upload, hosted verification, public ranking, or certification.

Run it from the repository root:

```powershell
pwsh ./scripts/matrix/matrix-cross-platform-verify-pilot.ps1 -ArtifactRoot artifacts/matrix/external-pilot-verify
```

The script performs four checks:

1. Validates `docs/matrix/examples/matrix-external-repo-pilot-set.v0.example.json`.
2. Creates summary-only Matrix verification bundles for the small Node, small .NET, Python package, monorepo-like nested project, and dirty worktree pilot shapes.
3. Runs `carves-matrix verify <artifact-root> --json` for every pilot bundle.
4. Mutates one summary artifact after manifest creation and verifies the failure reports an explicit `hash_mismatch` reason code.

## Checkpoint

The lane writes:

```text
matrix-cross-platform-verify-pilot-checkpoint.json
```

The checkpoint schema is `matrix-cross-platform-verify-pilot-checkpoint.v0`. It records the platform, pilot catalog schema, verified pilot count, per-pilot verify status, trust-chain gate posture, and the failure-probe reason codes. Public output redacts runtime roots, uses `artifact_root = "."` for the checkpoint and pilot bundles, and keeps per-pilot artifact roots bundle-relative.

Expected successful checkpoint posture:

```text
verified_pilot_count: 5
artifact_root: .
failure_probe.expected_reason_code: hash_mismatch
privacy.summary_only: true
public_claims.certification: false
```

## GitHub Actions

`.github/workflows/matrix-proof.yml` runs the Matrix proof lane and this pilot verify lane on:

- `ubuntu-latest`
- `windows-latest`

The workflow uploads both the standard Matrix proof artifacts and the pilot verification checkpoint artifacts.

## Boundary

The pilot verification lane uses generated summary fixtures. It does not prove package manager installs, semantic source correctness, large production monorepo coverage, private dependency access, hosted verification, public certification, public ranking, model safety benchmarking, operating-system sandboxing, or automatic rollback.
