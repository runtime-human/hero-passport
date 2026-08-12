# Recovery reference

When local orchestration memory is missing or uncertain, Core state wins.

## Recovery sequence

1. Call `hero.get_context`.
2. Verify `contractVersion=HP-MCP/2` and `skillContractVersion=hero-passport-skill/1`.
3. Respect persisted `autoStartQuest`, `autoFinishQuest`, locale, presentation style, and active default Hero.
4. Inspect all `openQuests` in the current Project, not only those owned by the active default Hero.
5. Match the current work conservatively.

## Matching policy

- no plausible open Quest: form a new Quest only when meaningful work clearly warrants it and the intended Hero+Project slot is free;
- exactly one clearly matching Quest: resume its persisted `questId` and `heroId`;
- several plausible matches: ask a concise user choice rather than guessing;
- another open Quest for the selected Hero but a different goal: do not bypass `HP133`; resolve explicit switch/finish/abandon semantics first.

Title/goal similarity is context, not identity. Never synthesize or infer a `questId` from text.

## Cross-host active-Hero changes

The active Hero is only a default preference for forming a new call. Once the Skill has selected a Hero for a Start intent, the explicit `heroId` remains part of that intent even if another host changes the global active preference before the call or retry.

A retry of a committed Start returns the original persisted Hero/Quest. Do not re-resolve ownership.

## Ambiguous transport results

For `hero.bootstrap`, `hero.create`, `hero.start_quest`, and `hero.finish_quest`, keep the caller-generated request ID with its exact intended arguments until the outcome is known. On an ambiguous transport failure, retry that same identity and payload.

Never generate a fresh request ID just because a reply was lost; that changes an ambiguous retry into a new mutation intent.
