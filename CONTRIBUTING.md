# Contributing

Language: [Chinese](CONTRIBUTING.zh-CN.md)

CARVES is currently published as a small matrix of local-first command line tools:

- Guard checks patch boundaries.
- Handoff records continuation packets.
- Audit discovers local evidence.
- Shield evaluates summary evidence and renders a badge.

The project is not accepting broad unsolicited feature PRs yet. The current collaboration posture is issue-first: open an issue before a PR so the scope can stay inside the public matrix boundary.

## Before Opening An Issue

1. Check whether the request belongs to Guard, Handoff, Audit, or Shield.
2. Include the command you ran, the expected result, and the actual result.
3. Do not attach private source code, raw diffs, prompts, model responses, secrets, credentials, or customer data.
4. For security concerns, follow `SECURITY.md` instead of opening a detailed public issue.

## Pull Requests

Small documentation corrections and focused bug fixes are welcome after an issue exists. A PR should:

- stay inside the public product boundary;
- include tests or a clear reason tests are not applicable;
- avoid hosted API, certification, public leaderboard, or operating-system sandbox claims unless those have been explicitly accepted in a release card;
- avoid adding telemetry, uploads, or network calls to the local matrix proof path.

Maintainers may close work that expands product scope before the matrix boundary is ready.
