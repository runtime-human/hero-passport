# Finish attestations

Use this reference when constructing `hero.finish_quest`. Report bounded facts about the work; do not score quality or fabricate verification.

## Outcome

Choose exactly one truthful result:

- `success` — requested goal accomplished.
- `partial` — useful subset completed but the goal is not fully done.
- `blocked` — an external condition prevents meaningful continuation.
- `failed` — the attempt ended without a usable requested result.
- `abandoned` — work was intentionally stopped without a scored result.

## Build and test evidence

Evidence provenance is deliberately narrow:

- `observed` — the agent directly invoked or saw the relevant result.
- `reported` — the user or another source stated it; the agent did not directly observe it.
- `none` — no supporting observation/report.

Consistency rules:

- `not_run` -> evidence must be `none`.
- `passed` or `failed` -> evidence must be `observed` or `reported`.
- `unknown` -> evidence may be `observed`, `reported`, or `none`.
- if `testsStatus != not_run`, `testsMentioned` must be true.

Never upgrade `reported` to `observed`. Do not claim a build/test was observed because another tool, user, CI summary, or prior message merely reported it unless the current agent actually invoked/saw that result.

## Skills

Choose 1–3 canonical Skill keys that were actually important to the Quest, ordered primary to tertiary. Do not use Hero Passport MCP calls themselves as evidence for `tool_use`.

Canonical keys:

`coding`, `testing_awareness`, `scope_control`, `documentation`, `tool_use`, `planning`, `research`, `debugging`, `review`, `maintenance`.

For Russian presentation, `scope_control` is displayed as «Контроль»; the canonical wire key remains `scope_control`.

## Corrections and scope

`scopeViolations` and `userCorrections` are bounded best-effort self-attestations. Count actual departures/corrections, not normal discovery, preference choices, or ordinary iterative refinement.

## Privacy boundary

Send only bounded Hero Passport fields. Never send source code, diffs, raw logs, command transcripts, prompts, secrets, environment dumps, full workspace paths, Git remotes, or other unnecessary repository contents.

The natural-language `summary` should describe the outcome compactly without copying sensitive implementation material.
