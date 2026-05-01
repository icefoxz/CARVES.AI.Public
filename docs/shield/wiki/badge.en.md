# Badge Guide

The Shield badge is a locally generated SVG. It is useful in README files, PR artifacts, or release notes so readers can quickly see the current self-check posture.

## Generate A Badge

```powershell
carves shield badge .carves/shield-evidence.json --output docs/shield-badge.svg
```

Inspect JSON metadata:

```powershell
carves shield badge .carves/shield-evidence.json --json
```

## What The Badge Shows

Visible text comes from Lite:

```text
CARVES Shield | 90/100 Strong
```

Metadata preserves Standard:

```text
G8.H8.A8
```

This gives readers both:

- a simple Lite score
- the more precise G/H/A dimension structure

## Colors

```text
red     Critical
gray    No Evidence
white   Basic
yellow  Disciplined
green   Strong
```

Green usually means strong or sustained evidence. Yellow means meaningful discipline. White means basic configuration. Gray means no usable evidence. Red means a Critical Gate failed.

## Recommended README Text

```markdown
![CARVES Shield](docs/shield-badge.svg)

This badge is a CARVES Shield local self-check. It is not certification.
```

## Avoid This Wording

Do not write:

```text
Certified by CARVES.
Verified safe by CARVES.
CARVES proves this code is secure.
```

## When To Refresh The Badge

Refresh it when:

- Shield evidence changes
- Guard, Handoff, or Audit configuration changes
- GitHub Actions proof is added
- block or review decisions are resolved
- a release is prepared

## What The Badge Does Not Prove

The badge does not prove:

- source code is bug-free
- AI made no mistakes
- the project passed a security audit
- operating-system sandboxing exists
- CARVES certified the project
