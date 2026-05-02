# Workflow Diagrams: Local, Self-Check, And CI

Language: [中文](workflow.zh-CN.md)

Shield v0 is local-first. Evidence is prepared locally or in CI, and the current evaluator also runs locally.

## Local Self-Check

```mermaid
flowchart TD
    A["Developer prepares .carves/shield-evidence.json"] --> B["carves shield evaluate"]
    B --> C["Standard G/H/A"]
    B --> D["Lite 0-100 score"]
    C --> E["Human reads weak dimensions"]
    D --> E
    E --> F["Fix evidence or governance setup"]
```

The local flow is the best first trial. You can start with Guard evidence only and keep Handoff and Audit as `enabled: false`.

## Badge Output

```mermaid
flowchart TD
    A["shield-evidence.v0"] --> B["carves shield badge"]
    B --> C["shield-badge.svg"]
    B --> D["shield-badge.v0 JSON metadata"]
    C --> E["README or PR artifact"]
    D --> F["CI artifact or review record"]
```

The badge shows self-check output only. It must not be presented as certification.

## GitHub Actions Proof

```mermaid
flowchart TD
    A["pull_request or push"] --> B["checkout repository"]
    B --> C["install CARVES CLI"]
    C --> D["carves shield evaluate"]
    D --> E["carves shield badge"]
    E --> F["upload shield artifacts"]
    F --> G["reviewer inspects JSON and SVG"]
```

CI proof makes the result repeatable. Each pull request can show whether the same evidence still evaluates cleanly.

## Privacy Boundary

```mermaid
flowchart LR
    A["source code"] -. "not uploaded by default" .-> X["blocked"]
    B["raw diff"] -. "not uploaded by default" .-> X
    C["prompts or model responses"] -. "not uploaded by default" .-> X
    D["secrets or credentials"] -. "not uploaded by default" .-> X
    E["summary evidence"] --> F["local Shield evaluator"]
```

The default path handles summary evidence only. Any future richer evidence path must be explicit opt-in and must state what would be sent.
