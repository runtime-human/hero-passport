---
name: hero-passport
description: Ambient RPG companion for meaningful software-project work. Use Hero Passport when implementation, debugging, review, project research, planning, documentation, testing, or maintenance becomes a coherent project goal; hydrate Hero Passport context, conservatively start/resume one durable Quest, finish it only at a genuine terminal boundary, and render only Core-authoritative progression. Do not activate for casual conversation or short factual questions with no meaningful project action.
compatibility: Requires Hero Passport HP-MCP/2 tools and hero-passport-skill/1 compatible Core.
metadata:
  version: "0.1.0"
  hero-passport-skill-contract: "hero-passport-skill/1"
  hero-passport-mcp-contract: "HP-MCP/2"
---

# Hero Passport lifecycle

Hero Passport is an ambient companion. Do not make the user manage it during ordinary project work.

## Always hydrate before relying on remembered state

When this skill activates for project work, after restart, or whenever Quest state is uncertain, call `hero.get_context` first.

Use Core as authority for setup, effective preferences, active default Hero, current Project, open Quests, and compatibility versions. If `contractVersion != HP-MCP/2` or `skillContractVersion != hero-passport-skill/1`, stop Hero Passport automation and surface concise upgrade guidance rather than guessing another wire shape.

If setup is incomplete, follow [references/lifecycle.md](references/lifecycle.md#onboarding).

## Start conservatively

Recommended granularity is one coherent meaningful user goal per Quest. This is a Skill heuristic, not something Core can infer.

Start automatically only when meaningful project work clearly begins and `autoStartQuest` is true. If unsure whether work is meaningful enough, do not ask just for Hero Passport and do not start yet; wait for a clearer boundary. Explicit user intent to start overrides auto-start preference.

For a new Quest:

1. choose the intended Hero, normally `get_context.activeHero.heroId`;
2. generate one fresh UUIDv7 `startRequestId` for this exact start intent;
3. choose one canonical `questType`;
4. create a short stable title and precise goal;
5. call `hero.start_quest` with the explicit `heroId`;
6. retain returned `questId` and persisted `heroId` as orchestration state.

Reuse a `startRequestId` only for an ambiguous transport retry of the identical intended call. Never mint a new ID merely to bypass `HP135` or `HP133`.

See [references/lifecycle.md](references/lifecycle.md) for continuation, switches, and error handling.

## Resume instead of duplicating

`hero.get_context.openQuests` spans all Heroes for the current Project. Resume a Quest only when the current work clearly matches that durable Quest. Do not infer identity from title similarity alone.

If several open Quests plausibly match, surface a concise user choice rather than guessing. See [references/recovery.md](references/recovery.md).

## Finish conservatively

Do not finish merely because a turn ended. Finish only when the coherent goal is genuinely complete and the work result is ready to present, or a truthful terminal `partial`, `blocked`, `failed`, or explicit `abandoned` boundary has been reached.

If unsure whether to finish, keep the Quest open.

For finalization:

1. generate one fresh UUIDv7 `finishRequestId` for the finalization intent;
2. derive truthful bounded attestations using [references/finish-attestations.md](references/finish-attestations.md);
3. call `hero.finish_quest` with the persisted `questId`;
4. on ambiguous transport failure retry the same ID and identical payload;
5. never recalculate XP, Skills, levels, Rank, Trust/Strain, streaks, Traits, Titles, or milestones.

If Core returns `HP136`, another distinct finalization already won. Do not overwrite or invent replacement facts; rehydrate and honor persisted state.

## Presentation

At Start, keep Hero Passport compact, for example `⚔ <Quest title>`.

At Finish, present the normal work result and the canonical progression returned by Core. Reformatting is allowed; changing numeric or unlock facts is not. Use the persisted Quest locale for Hero Passport labels.

See [references/presentation.md](references/presentation.md).

## Privacy boundary

Never send Hero Passport source code, diffs, raw terminal/test/build logs, prompts, environment dumps, secrets, remote URLs, or full workspace paths. Report only the bounded fields accepted by HP-MCP/2.

Hero Passport calls themselves never justify the `tool_use` Skill.
