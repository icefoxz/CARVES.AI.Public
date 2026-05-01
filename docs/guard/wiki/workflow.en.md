# CARVES.Guard Workflow Diagrams

This page shows where CARVES.Guard belongs and what to do after `allow`, `review`, or `block`.

## Local Development Flow

```mermaid
flowchart TD
    A["Human asks for a small change"] --> B["AI coding tool edits code"]
    B --> C["Patch appears in git working tree"]
    C --> D["Run carves guard check"]
    D --> E{"Guard decision"}
    E -->|allow| F["Continue to normal human review or merge"]
    E -->|review| G["Human checks warnings and evidence"]
    E -->|block| H["Do not merge; ask AI to shrink or fix the patch"]
    G --> I{"Risk accepted"}
    I -->|yes| F
    I -->|no| H
    H --> B
```

## Decision Flow

```mermaid
flowchart LR
    A["git status / diff"] --> B["changed files"]
    C[".ai/guard-policy.json"] --> D["policy"]
    B --> E["path checks"]
    B --> F["budget checks"]
    B --> G["dependency checks"]
    B --> H["shape checks"]
    D --> E
    D --> F
    D --> G
    D --> H
    E --> I{"findings"}
    F --> I
    G --> I
    H --> I
    I -->|violation exists| J["block"]
    I -->|warnings only| K["review"]
    I -->|no findings| L["allow"]
```

## GitHub Actions Flow

```mermaid
flowchart TD
    A["Pull request opened or updated"] --> B["GitHub Actions starts"]
    B --> C["Checkout PR head and fetch base branch"]
    C --> D["Materialize PR diff into working tree"]
    D --> E["Install or locate carves command"]
    E --> F["Run carves guard check --json"]
    F --> G{"Exit code"}
    G -->|0 allow| H["CI check passes"]
    G -->|1 review/block| I["CI check fails"]
    I --> J["Developer reads JSON or explain output"]
    J --> K["Shrink patch, add tests, or fix policy violation"]
```

## Recommended Team Rule

When adopting Guard:

- `allow`: continue to normal human review.
- `review`: start by failing CI so a human sees the warning; later decide if review should be non-blocking.
- `block`: must be fixed before merge.

If this is too strict for the first week, make CI upload the JSON report without blocking. After the team understands the `rule_id` values, turn Guard into a required check.

## Correct Placement

```text
AI writes the patch.
Guard checks the patch boundary.
Humans review semantic correctness.
```

Guard does not prove business logic correctness and does not replace tests. It blocks obviously oversized, out-of-scope, missing-test, or sensitive-path patches first.
