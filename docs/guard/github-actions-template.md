# CARVES.Guard GitHub Actions Template

This page explains the copyable workflow in [`github-actions-template.yml`](github-actions-template.yml).

## What It Does

The workflow runs `carves guard check --json` on every pull request. Guard exits with:

- `0` for `allow`
- `1` for `review` or `block`

That means a pull request can only stay green when the patch is allowed by the repository policy. `review` is intentionally not treated as passing in the frozen Guard policy posture.

## How To Add It

1. Copy [`github-actions-template.yml`](github-actions-template.yml) into your repository as `.github/workflows/carves-guard.yml`.
2. Commit a Guard policy at `.ai/guard-policy.json`, or let the workflow create the starter policy on the first run.
3. Open a pull request that changes code.
4. Read the uploaded `carves-guard-decision` artifact when the check fails.

## Why It Checks Out CARVES.Guard

This prerelease template does not require NuGet.org publication and does not require hosted secrets. It checks out the public CARVES.Guard source at a version tag and runs the CLI from source.

After an operator publishes a registry package, the install step can be replaced with a normal `dotnet tool install` command. Until then, this source checkout keeps the workflow copyable without private tokens.

## Artifact

The workflow uploads `guard-check.json`. The file contains:

- `decision`: `allow`, `review`, or `block`
- `policy_id`: the policy used for the decision
- `changed_files`: the patch files Guard saw
- `violations`: blocking findings
- `warnings`: review findings
- `evidence_refs`: stable references you can paste into review comments

## Limits

Guard checks a git patch. It is not an operating-system sandbox, does not intercept writes in real time, and does not automatically roll back changes. In CI, this is exactly the useful posture: the patch may exist in the pull request, but it cannot pass the Guard job when it violates policy.
