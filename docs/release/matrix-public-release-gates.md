# Matrix Public Release Gates

Status: release gate map.

This document lists the gates that must remain visible during the Guard, Handoff, Audit, and Shield matrix release path.

## Automatic Implementation Gates

These can be completed by ordinary implementation cards:

| Gate | Proof |
| --- | --- |
| Package metadata ready | Project files include public package metadata and package readme references. |
| Version dimensions declared | The release candidate records which version dimensions changed and follows `docs/release/runtime-versioning-policy.md`. |
| Local nupkg install smoke | Dotnet tool installs from a local package feed and command help works. |
| External-repo smoke | A temporary git repository runs the relevant command chain. |
| Documentation ready | Public docs explain command usage, artifacts, privacy posture, and limits. |
| CI template ready | Workflow templates are copyable and have shape/smoke coverage. |
| Matrix evidence ready | Audit or collector output produces valid `shield-evidence.v0`. |
| Shield self-check ready | `carves shield evaluate` consumes only `shield-evidence.v0` and does not rate AI models. |
| Badge ready | `carves shield badge` renders local self-check metadata and SVG. |

## Human Or Operator Gates

These must not be marked complete by implementation work alone:

| Gate | Required human/operator action |
| --- | --- |
| License choice | Confirm the repository license before public release. |
| NuGet.org ownership | Reserve or confirm package ids and owner account. |
| NuGet.org push | Push packages using an approved token and release decision. |
| GitHub public release | Create tag/release and publish release notes. |
| Hosted Shield API | Approve service design, retention, privacy, auth, and cost model. |
| Public leaderboard or directory | Approve product, abuse, privacy, and ranking policy. |
| Verified or certified status | Define verification authority and evidence rules before using these words. |
| Trademark or badge trust policy | Approve how external projects may display CARVES marks. |

## Fail-Closed Language

If an operator gate has not happened, release documents must say so directly.

Use:

```text
NuGet.org publication is an operator gate and has not been claimed by this checkpoint.
```

Avoid:

```text
Published to NuGet.org.
```

unless the push has actually happened and is verifiable.

## Shield Evaluation Gate

The public self-check evaluation gate is:

```text
shield-evidence.v0 -> carves shield evaluate -> Standard G/H/A + Lite
```

Audit may generate or summarize `shield-evidence.v0`. Audit must not become the scoring authority.

Shield may produce G/H/A and Lite self-check output from `shield-evidence.v0`. Shield must not silently change its input contract to raw source, raw diff, Guard decisions, Handoff packets, or Audit logs, and it must not present the result as model safety benchmarking.

## Privacy Gate

Every public release checkpoint must confirm that default artifacts do not include:

- source code
- raw git diff text
- prompts
- model responses
- secrets
- credentials
- environment variable values
- private file payloads

If a future opt-in richer evidence mode exists, it must be documented separately and cannot be treated as the default matrix path.

## Final GitHub Publish Gate

The matrix can be called GitHub-publishable when the final checkpoint confirms:

1. Guard checkpoint complete.
2. Handoff checkpoint complete.
3. Audit checkpoint complete.
4. Matrix end-to-end smoke complete.
5. Cross-platform packaged install smoke complete.
6. Matrix GitHub Actions proof complete.
7. Public README and docs complete.
8. Repo hygiene complete.
9. Known limitations complete.
10. Remaining human/operator gates listed.

The final operator-owned gate is `docs/release/matrix-operator-release-gate.md`. It lists the required evidence and the manual publication actions, and it keeps deferral choices separate from Matrix proof or verification results.
