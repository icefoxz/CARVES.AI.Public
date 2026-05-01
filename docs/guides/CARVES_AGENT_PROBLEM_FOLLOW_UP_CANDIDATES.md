# CARVES Agent Problem Follow-Up Candidates

This guide records the Phase 36 read-only bridge from agent problem triage to operator-reviewed governed follow-up.

## When To Use

Use this after one or more agents have reported problems and the triage ledger has been read:

```powershell
carves pilot problem-intake --json
carves pilot triage --json
carves pilot follow-up --json
carves pilot follow-up-plan --json
carves pilot follow-up-record --json
carves pilot follow-up-intake --json
```

Equivalent read-only aliases:

```powershell
carves pilot problem-follow-up --json
carves pilot triage-follow-up --json
carves inspect runtime-agent-problem-follow-up-candidates
carves api runtime-agent-problem-follow-up-candidates
carves inspect runtime-agent-problem-follow-up-decision-plan
carves api runtime-agent-problem-follow-up-decision-plan
carves inspect runtime-agent-problem-follow-up-decision-record
carves api runtime-agent-problem-follow-up-decision-record
```

## What The Surface Shows

The surface shows:

- recorded problem count
- follow-up candidate count
- governed candidate count
- watchlist candidate count
- repeated pattern count
- blocking candidate count
- candidate id and status
- recommended triage lane
- related problem ids
- related evidence ids
- affected stages and repos
- suggested title and intent
- operator review questions

## Candidate Status

```text
governed_follow_up_candidate
watchlist_only
```

`governed_follow_up_candidate` means the operator should review the pattern. It does not mean CARVES has created a card or task.

`watchlist_only` means the report remains visible but does not yet have repeated or blocking evidence.

## Promotion Rule

```text
problem_count >= 2
or blocking/high/critical severity exists
```

If either condition is true, the pattern becomes a governed follow-up candidate.

## Required Operator Discipline

Use candidates to decide whether a friction pattern deserves governed work.

Before opening work, decide:

- is this a Runtime command contract problem?
- is this a target bootstrap guidance problem?
- is this a target-project-only misunderstanding?
- does it repeat across repos, stages, or agents?
- can docs/readback guidance fix it?
- what acceptance evidence would prove it is resolved?

## Non-Authority

This surface does not:

- create cards
- create tasks
- approve reviews
- resolve problem records
- authorize blocked changes
- edit protected truth roots
- edit `.gitignore`
- mutate runtime dist binding
- stage, commit, tag, pack, or release

Run `carves pilot follow-up-plan --json` after this surface to see the explicit accept/reject/wait decision choices. Run `carves pilot follow-up-record --json` to see whether those choices have durable operator records. Run `carves pilot follow-up-intake --json` to see which accepted, clean records may enter formal planning. The only durable implementation route is still the governed planning lane.
