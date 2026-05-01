# CARVES Agent Trial Starter Packs

This directory contains public local-only Agent Trial starter packs.

## Available Packs

| Pack | Version | Purpose |
| --- | --- | --- |
| `official-agent-dev-safety-v1-local-mvp` | `0.1.0-local` | First local bounded-edit starter pack for offline Agent Trial playtesting. |

## Boundary

Starter packs are local playtest workspaces. They are not server-issued challenges, not public leaderboard receipts, not model identity proof, and not certification.

Each pack should be copied into a clean workspace before use. Pack metadata under `.carves/trial/` is evidence input and must not be edited by the tested agent.

Each official pack must include `.carves/trial/instruction-pack.json`. That file names the canonical instruction files, prompt sample path, prompt id/version, and hashes used for comparable local playtests. Pack or challenge metadata must pin it with `expected_instruction_pack_sha256`; if a user edits instruction files for a private experiment, the collector may still emit local evidence, but the run is not directly comparable to the official prompt version unless the instruction pack identity and hash still match.
