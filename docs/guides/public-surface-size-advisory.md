# Public Surface Size Advisory

This guide is a review signal, not a CI gate.

Public CARVES source, docs, and tests should stay readable enough for a first-run reviewer to understand why a file exists, what responsibility it owns, and where the next related behavior lives. Size can prompt that review question, but size alone does not decide quality.

Do not turn this advisory into a fixed line-count failure threshold. A file is not unhealthy just because it crosses a number, and a split is not automatically useful just because it lowers a count.

Prefer splitting when the file mixes responsibility boundaries, hides first-run intent, forces unrelated churn into the same edit surface, or makes focused tests harder to read. Prefer keeping a file whole when the current shape is cohesive, stable, and easier to verify than the split version.

When a public-facing file feels too large, use this order:

1. Identify the responsibility that is hard to explain.
2. Move cohesive behavior behind an existing boundary or a clearly named helper.
3. Keep call sites and public command behavior stable.
4. Add or adjust focused tests that describe the behavior being preserved.

Do not split solely to satisfy a number. Do not add a repository-wide scanner that fails historical files. Do not make release readiness depend on raw line count without a human review judgment and a governed card.
