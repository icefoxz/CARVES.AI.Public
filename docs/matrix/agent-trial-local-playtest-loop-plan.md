# Matrix Agent Trial Local Playtest Loop Plan

## Status
Materialized as governed cards `CARD-907` through `CARD-916`.

`CARD-907` through `CARD-916` are complete. The current readiness audit is recorded in `docs/matrix/agent-trial-local-playtest-readiness-audit.md`, and the pinned local user smoke is recorded in `docs/matrix/agent-trial-v1-local-user-smoke.md`.

## Objective
Make the offline Agent Trial path usable for a first-time user who wants to download the project, run a local score, understand the result, and compare repeated runs without registering or submitting data.

This plan does not implement server registration, leaderboard acceptance, or certification. It only closes the local downloadable playtest loop.

## Execution Order
1. `CARD-907` - Re-audit the current local journey before adding more surface. Completed.
2. `CARD-908` - Package the official starter trial pack. Completed.
3. `CARD-909` - Add the standard agent instruction and prompt pack. Completed.
4. `CARD-910` - Expose the local command surface. Completed.
5. `CARD-911` - Make score mapping explicit and versioned. Completed.
6. `CARD-912` - Generate a human-readable local result card. Completed.
7. `CARD-913` - Improve local failure diagnostics. Completed.
8. `CARD-914` - Add local history and compare. Completed.
9. `CARD-915` - Write the first-run quickstart. Completed.
10. `CARD-916` - Run and pin an end-to-end local user smoke. Completed.

## Product Boundary
- Local results are offline and unsubmitted.
- Local Matrix verification proves bundle integrity and contract checks, not agent certification.
- Score comparisons are direct only when the scoring profile, prompt pack, task pack, and collector/verifier versions match.
- Server registration, challenge pull, receipt issuance, and leaderboard eligibility remain future platform work.

## First Execution Step
No `CARD-907` through `CARD-916` local playtest loop card remains pending. Follow-on work should come from a new governed card rather than continuing this closed local loop implicitly.
