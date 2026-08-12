# Finish attestations reference

Report only bounded attestations accepted by HP-MCP/2. These are not independent verification or quality scores.

## Build/test provenance

`observed` means the agent asserts it directly invoked or saw the relevant result during this work.

`reported` means the user or another source stated the result and the agent did not directly observe it.

`none` means there is no supporting observation/report.

Never promote `reported` to `observed`.

Consistency rules:

```text
status = not_run -> evidence = none
status = passed | failed -> evidence = observed | reported
status = unknown -> evidence = observed | reported | none
testsStatus != not_run -> testsMentioned = true
```

Do not attach raw command output, terminal logs, test logs, build logs, source, diffs, prompts, secrets, environment dumps, remote URLs, or full paths.

## Scope violations

`scopeViolations` is a best-effort bounded self-attestation for meaningful work outside the requested goal. Do not count necessary discovery, ordinary adjacent work required to complete the goal, or user-approved refinement as scope violations.

## User corrections

`userCorrections` counts substantive corrections from the user caused by the agent taking the work in a wrong direction. Do not count preference choices, normal clarification, or optional refinement.

## Skills used

Choose 1–3 canonical Skills that materially contributed to the work, ordered primary, secondary, tertiary:

```text
coding
testing_awareness
scope_control
documentation
tool_use
planning
research
debugging
review
maintenance
```

Do not award `tool_use` merely because Hero Passport MCP tools were called.

## Summary

Write a compact factual completion summary of the work result. Do not pad it to game the summary reward and do not include secrets or raw logs.
