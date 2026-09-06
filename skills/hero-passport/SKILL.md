---
name: hero-passport
description: Tracks meaningful project work as Hero Passport Quests and renders canonical RPG progression through HP-MCP/2. Use for implementation, debugging, review, project planning or research, testing, documentation, and maintenance that produces a concrete project result; do not activate for casual chat, short factual explanations, or clarification-only turns.
license: Apache-2.0
compatibility: Requires a connected Hero Passport HP-MCP/2 server.
metadata:
  hero-passport-skill-contract: "hero-passport-skill/1"
  hero-passport-mcp-contract: "HP-MCP/2"
---

# Hero Passport

Use this Skill as ambient orchestration policy for `hero-passport-skill/1` over `HP-MCP/2`. Core is authoritative for durable state, validation, XP, Skills, levels, Trust/Strain, streaks, unlocks, and all numeric progression. Never calculate or invent game facts.

## Hydrate first

For relevant project work, restart, or uncertain state, call `hero.get_context` before relying on remembered Hero Passport state. Use persisted `autoStartQuest`, `autoFinishQuest`, locale, presentation style, active default `heroId`, current Project, and all open Quests. If `contractVersion` or `skillContractVersion` is incompatible, stop Hero Passport automation and surface concise upgrade guidance instead of guessing another wire shape.

If setup is incomplete, use the short bootstrap flow in `references/lifecycle.md`. Do not use `hero.configure` to create the initial Hero.

## Activate conservatively

Treat one coherent meaningful project goal as the normal Quest granularity. Implementation, debugging, review, project planning/research, testing, documentation, and maintenance commonly qualify. Casual conversation, short factual explanation, summarization, translation, or clarification without project action do not.

If unsure whether work is meaningful enough to start, do not ask merely to create a Quest; wait for clearer work. If unsure whether a Quest is finished, keep it open.

## Start, continue, and finish

When a Quest should start and `autoStartQuest` permits it (or the user explicitly asks), call `hero.start_quest` with an explicit selected `heroId` and a fresh `startRequestId`. Reuse that request ID only for an ambiguous retry of the exact same canonical Start intent. Retain the returned `questId`; do not infer Quest identity from similar title or goal text.

Keep materially related follow-up work in the same Quest. On restart or uncertainty, rehydrate and resume the clearly matching persisted `questId`. Recovery and goal-switch rules are in `references/recovery.md` and `references/lifecycle.md`.

Finish only when the goal is genuinely done, the user explicitly requests finalization/abandonment, or a truthful terminal outcome is reached. Respect persisted `autoFinishQuest`. Call `hero.finish_quest` with a fresh `finishRequestId`; an ambiguous retry must reuse the same ID and identical payload. See `references/finish-attestations.md` before constructing the payload.

## Retry and conflict rules

- `HP133`: do not abandon an existing same-Hero Project Quest just to make a new Start succeed; resolve or resume it according to recovery policy.
- `HP135`: a mutation ID was reused with different canonical context/arguments. Do not mint a replacement ID and pretend it is the same retry.
- `HP136`: another finalization already won. Do not overwrite history or invent facts; rehydrate and honor persisted state.
- transient `HP202`: retry only the same retry-safe request identity with unchanged arguments, and keep the retry bounded.

## Evidence and privacy

Finish reports bounded attestations, not quality scores. `observed` means the agent directly invoked or saw the relevant result; `reported` means the user or another source stated it and the agent did not directly observe it. Never promote `reported` to `observed`.

Never send source code, diffs, raw logs, command transcripts, prompts, secrets, environment dumps, or workspace paths to Hero Passport. Hero Passport MCP calls themselves do not justify awarding `tool_use`.

## Present canonical results

Keep Quest-start presentation compact. On finish, give the normal work result plus canonical Hero Passport progression. You may reformat or localize presentation, but never recalculate server fields or invent unlocks. See `references/presentation.md`.

## References

Load only the reference needed for the current decision:

- `references/lifecycle.md` — activation, onboarding, Start/continue/switch/Finish boundaries.
- `references/finish-attestations.md` — outcomes, metrics, Skills, evidence provenance, privacy.
- `references/recovery.md` — restart, multiple Heroes/open Quests, retries, conflicts, version mismatch.
- `references/presentation.md` — locale, concise start output, canonical progression, milestone flavor.
