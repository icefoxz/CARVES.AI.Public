# CARVES Shield Matrix Boundary v0

Status: boundary draft for CARD-756.

CARVES Shield is a multi-dimensional label for AI coding workflow governance. It reports summary evidence for how a project governs AI-generated code changes across three dimensions:

```text
Guard    output governance: did the AI patch pass a declared boundary check?
Handoff  input governance: did the next AI session receive reliable handoff context?
Audit    history governance: can prior AI decisions be read, explained, and reviewed?
```

CARVES Shield is a CARVES-level self-check standard. It is not a CARVES.Guard feature, not a code quality score, not a model safety benchmark, and not a production certification by default.

The matrix is designed around Guard, Handoff, and Audit because those are the initial CARVES project dimensions intended to become independently evaluable. A score in any dimension still requires evidence; the matrix shape itself is not a claim that every target repository has enabled every dimension.

## Product Boundary

CARVES Shield has two modes:

```text
Shield Lite      quick self-check for broad adoption and comparison
Shield Standard  evidence-backed three-dimensional matrix
```

Lite is allowed to produce a single score because its job is quick local self-check feedback. Standard must preserve the three independent dimensions because its job is accurate governance reporting.

Do not collapse Standard into one overall score. A project with strong Guard evidence and no Handoff or Audit evidence should be shown as strong in one dimension and absent in the others, not as a misleading average.

## Dimensions

### Guard

Guard measures output governance. It answers whether an AI-generated patch is checked before it enters review or merge.

Guard evidence may include:

- policy presence and schema validity
- effective protected paths
- change budget
- dependency policy
- source/test discipline
- decision records
- CI or PR check evidence

### Handoff

Handoff measures input governance. It answers whether an AI session starts with reliable context, known constraints, completed facts, and remaining work.

Handoff evidence may include:

- handoff packet presence
- packet schema validity
- current objective
- remaining work
- must-not-repeat items
- completed facts with evidence refs
- orientation freshness
- session continuity

### Audit

Audit measures history governance. It answers whether prior AI-related decisions can be read, explained, and reviewed.

Audit evidence may include:

- decision log presence
- readable records
- supported schema versions
- rule ids and evidence
- explain coverage
- summary/report outputs
- record integrity and malformed-record posture

## Numeric Scale

Each dimension uses the same visible scale:

```text
0      gray    not enabled / no evidence
C      red     critical failure
1-4    white   basic configured
5-7    yellow  disciplined
8-9    green   sustained or strong
```

`C` is not a number. It means the dimension is configured but failed a Critical Gate. `C` is worse than `0` because `0` makes no workflow governance claim, while `C` means a governance claim exists but is not trustworthy.

## Critical Gate

Critical Gate is independent from the numeric score.

If any critical rule fails in a dimension, that dimension is reported as `C` instead of a number:

```text
◈ CARVES GC·H5·A4 ✗CG-04
```

If multiple dimensions fail:

```text
◈ CARVES GC·HC·A4 ✗CG-04,CH-02
```

Critical rules should be few, mechanical, and tied to actual workflow governance or trust failure. They are not quality preferences.

## Standard Label

The Standard label format is:

```text
◈ CARVES G<score>·H<score>·A<score> /<window>d <critical>
```

Examples:

```text
◈ CARVES G8·H5·A3 /30d ✓
◈ CARVES G2·H0·A1 ✓
◈ CARVES G8·HC·A3 /30d ✗CH-02
```

Rules:

- `G` is Guard.
- `H` is Handoff.
- `A` is Audit.
- `0` means the dimension is not enabled or no evidence was provided.
- `C` means Critical failure.
- `/Nd` is the sample window in days.
- Sample windows matter mainly for sustained scores such as 8 and 9.
- `✓` means all Critical Gates passed.
- `✗ID` lists failing Critical Gate ids.

## Lite Label

Shield Lite is a quick self-check for individual developers, hobbyists, open-source maintainers, and teams trying CARVES for the first time.

Lite may produce a single 0-100 score:

```text
CARVES Shield Lite: 72/100 Yellow
```

Lite must be labeled as self-check, not certification.

Lite should emphasize:

- top risks
- next steps
- estimated improvement from simple fixes
- no source upload required by default

Example:

```text
Score: 72/100
Band: Yellow
Top risks:
- Guard is not running in CI.
- Source changes do not require test changes.
Next step:
- Add CARVES.Guard to pull_request CI.
```

## Planned API Boundary

CARVES may provide a future Shield testing API.

The default API posture should be evidence-first:

```text
local tool scans repo -> local tool emits evidence summary -> API evaluates evidence -> API returns result and badge metadata
```

The API should not require source upload by default.

Allowed evidence examples:

- policy fields
- effective protected path list
- decision counts
- malformed record counts
- CI detection result
- handoff packet metadata
- audit summary metadata

Disallowed default claims:

- "CARVES reviewed your source code"
- "CARVES certified this project"
- "CARVES guarantees semantic code safety"
- "CARVES benchmarks model safety"
- "CARVES provides OS sandbox containment"

## Self-Check Versus Verified

Shield v0 starts as self-check.

```text
Self-Check  evidence generated by the project or local CLI
Verified    future hosted or third-party verification, not claimed by v0
```

Badges and docs must clearly distinguish self-check from verified evaluation. Shield v0 does not claim central verification.

## Non-Goals

Shield v0 does not:

- implement `carves shield evaluate`
- implement a hosted API
- render dynamic badges
- create a public directory
- certify projects
- benchmark or rate AI model safety
- score code quality
- replace security review
- replace tests
- require source upload

## Follow-Up Cards

Recommended follow-up sequence:

```text
CARD-757  Define Shield evidence schema v0
CARD-758  Define Standard G/H/A 0-9 rubric
CARD-759  Define Shield Lite scoring model
CARD-760  Define Shield API contract and privacy posture
CARD-761  Implement local Shield evaluate prototype
CARD-762  Add Shield badge output
CARD-763  Add GitHub Actions Shield proof
CARD-764  Publish Shield docs and Wiki
```
