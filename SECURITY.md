# Security Policy

CARVES Guard, Handoff, Audit, and Shield are local-first tools. The public matrix proof does not require source upload, raw diff upload, prompt upload, model response upload, secrets, credentials, hosted verification, or a public leaderboard.

## Supported Versions

| Product | Current public posture |
| --- | --- |
| Guard | `0.2.0-beta.1`, local-package beta proof |
| Handoff | `0.1.0-alpha.1`, local-package alpha proof |
| Audit | `0.1.0-alpha.1`, local-package alpha proof |
| Shield | local command in `CARVES.Runtime.Cli` |

## Reporting A Vulnerability

Use GitHub private vulnerability reporting when it is available for this repository. If private reporting is unavailable, open a minimal public issue that says a security report is available and wait for maintainer contact. Do not include exploit details, private repository content, raw diffs, prompts, model responses, secrets, credentials, or customer data in a public issue.

## What To Include

- affected command and version;
- operating system;
- a minimal reproduction using synthetic files;
- expected and actual decision output;
- whether the issue can change an allow/review/block decision, evidence discovery, Shield scoring, or artifact privacy.

## Non-Claims

CARVES does not claim operating-system sandboxing, syscall interception, automatic rollback, hosted verification, public certification, or public leaderboard ranking in the current release line.
