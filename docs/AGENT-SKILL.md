# Hero Passport — Agent Skill Contract

**Status:** Accepted `hero-passport-skill/1` orchestration contract for v3.2.1  
**Snapshot:** 2026-08-11

This document defines the official Hero Passport Agent Skill. All game rewards/durable invariants remain Core-authoritative.

## 1. Purpose

Ambient flow:

```text
hydrate context
recognize meaningful work
-> start/resume Quest
-> work normally
-> recognize completion/switch
-> report bounded attestations
-> finish Quest
-> render canonical RPG result
```

Users should rarely need to mention Hero Passport.

## 2. Packaging

```text
skills/hero-passport/
  SKILL.md
  references/
    lifecycle.md
    finish-attestations.md
    presentation.md
    recovery.md
```

`SKILL.md` stays concise and follows the open Agent Skills format. Host-specific setup belongs in `docs/integrations/*`.

Skill metadata declares compatibility with:

```text
hero-passport-skill/1
HP-MCP/2
```

Release tooling validates distributable Skill content with the current Agent Skills validator when available.

## 3. Activation heuristic

Start/finish recognition is model-driven heuristic policy, not Core truth.

Recommended granularity: one coherent meaningful user goal per Quest.

Likely Quest work includes implementation, debugging, review, architecture/planning that produces a project result, research required for a project decision, documentation, maintenance and testing.

Do not auto-start for casual conversation, short factual explanations or clarification with no meaningful project action.

## 4. Hydrate before relying on remembered state

At relevant Skill activation/restart/recovery, call:

```text
hero.get_context
```

Use it to learn:

```text
Core/Skill/contract versions
setup state
persisted auto-start/auto-finish preferences
locale/presentation style
default active Hero
current Project
open Quests across all Heroes in this Project
```

Do not substitute packaged defaults for persisted settings after restart.

If `skillContractVersion`/`contractVersion` is incompatible, surface upgrade guidance and do not guess a newer/older wire shape.

## 5. Onboarding

If `setupCompleted=false`, the Skill may conduct the short setup conversationally:

1. locale;
2. initial Hero name;
3. presentation style;
4. auto-start preference;
5. auto-finish preference + confirmation.

Then generate one fresh `bootstrapRequestId` and call `hero.bootstrap`.

If the transport result is ambiguous, retry the **same** bootstrap request ID/arguments.

Do not call `hero.configure` to create the initial Hero. Configure is post-setup preferences only.

## 6. Local reasoning state

Conceptual Skill state:

```text
NO_QUEST
ACTIVE_QUEST(questId, heroId, title, goal)
```

This is orchestration memory only. Core state from `hero.get_context` is authoritative when memory is uncertain.

## 7. Start

When meaningful work clearly begins and auto-start is enabled (or the user explicitly asks), select the intended Hero.

Normal default selection:

```text
heroId = get_context.activeHero.heroId
```

The active pointer is a preference, not hidden server ownership. `hero.start_quest` always receives the explicit selected HeroId.

Generate:

```text
startRequestId = fresh UUIDv7 for this start intent
heroId
questType
short title
precise goal
```

Reuse the same `startRequestId` only for an ambiguous retry of that exact intended Start. A different intended Quest gets a new request ID.

On success retain returned `questId` + persisted `heroId`.

## 8. Conservative automation

```text
unsure whether to start  -> do not ask; wait for clearer meaningful work
unsure whether to finish -> keep Quest open
```

Manual user intent overrides automation:

```text
start Quest          -> start if invariant permits
finish Quest         -> finish with truthful current outcome/attestations
keep it open         -> no finish
abandon Quest        -> finish as abandoned
```

## 9. Continue same goal

Keep one Quest for materially related follow-up work, such as tests for the same fix, necessary adjacent docs, or changes discovered while implementing the same outcome.

Do not fragment one coherent goal into micro-Quests.

## 10. Recovery across Heroes

`hero.get_context.openQuests` contains all current-Project open Quests across Heroes.

Recovery policy:

- no plausible match -> form a new Quest for the selected/default Hero if its Hero+Project slot is free;
- exactly one clearly matching Quest -> resume that `questId`, even if another host later changed global active-Hero preference;
- several plausible matches -> do not guess; surface a concise choice;
- a different open Quest for the same selected Hero blocks new Start with HP133 until explicit finish/abandon/switch semantics resolve it.

Never infer Quest identity from similar title/goal text alone.

## 11. Explicit goal switch

If user clearly switches to an independent goal before old work is complete:

```text
useful completed result -> finish old as partial
no useful result        -> finish old as abandoned
then start new Quest
```

If switch is ambiguous, keep current Quest open.

## 12. Finish boundary

Finish only when the current goal is genuinely done and the agent is ready to provide its final work result, or when a truthful terminal outcome (`blocked`, `failed`, explicit `abandoned`) is reached.

Generate one fresh:

```text
finishRequestId
```

for the finalization intent.

Retry an ambiguous Finish using the same finishRequestId and identical canonical payload.

If Core returns `HP136 quest_already_finalized_conflict`, do not retry with invented facts or overwrite history. Explain that another finalization won and use the persisted game state as authority.

## 13. Bounded attestations

The Skill reports validated bounded attestations, not quality scores.

Outcome:

```text
success    requested goal accomplished
partial    useful subset but goal not fully done
blocked    external condition prevents meaningful continuation
failed     attempt ended without a usable requested result
abandoned  intentionally stopped without scored result
```

Skills: choose 1–3 canonical Skills actually important to the work, primary to tertiary. Hero Passport calls themselves do not qualify for `tool_use`.

Build/test provenance:

```text
observed  agent asserts it directly invoked/saw the relevant result
reported  user/other source stated it; agent did not directly observe it
none      no supporting observation/report
```

Never upgrade reported -> observed and never send raw logs/source/diffs/command transcripts.

Scope violations/user corrections are best-effort bounded self-attestations. Do not count normal discovery, preference choices or ordinary refinement as errors.

## 14. Presentation

Start remains compact:

```text
⚔ Добавить first-run onboarding
```

Finish presents normal work summary plus canonical progression. Skill may reformat but never recalculate.

Semantic result fields are authoritative over fallback displayText.

## 15. Milestone flavor

Core/engine milestone truth is semantic keys/events, not a deterministic flavor hash.

Presentation maps milestone semantics to current curated localized flavor. Skill may lightly contextualize significant level/rank/title/trait/streak lines, but may not invent unlocks or alter numeric facts.

Do not turn every Quest into comedy.

## 16. Language

Use persisted effective Quest locale for Hero Passport presentation. Supported MVP locales: `ru-RU`, `en-US`.

Skill conversation language may follow the user independently, but canonical values/keys remain server-authoritative.

## 17. Error handling

```text
HP001 -> bootstrap/setup path
HP002 -> setup already exists; rehydrate get_context
HP133 -> resolve existing same-Hero Project Quest
HP135 -> caller reused a mutation ID with changed canonical context/args; do not mint a replacement as a fake retry
HP136 -> another different finalization already committed; do not overwrite
HP202 -> bounded retry of the same retry-safe request identity/args after transient contention
```

Never automatically abandon a Quest just to make a new Start succeed unless the user explicitly switched away and established abandoned/partial semantics.

## 18. Evals

Minimum Agent Skill scenarios:

```text
short factual question -> no start
meaningful project work -> start
persisted autoStart=false after restart -> no auto-start
same-goal followups -> no fragmentation
awaiting user decision -> no finish
complete goal -> finish
explicit mid-work switch -> partial/abandoned then new start
ambiguous switch -> no silent close
restart same goal -> get_context -> resume matching questId
inactive-Hero open Quest still discoverable
multiple plausible open Quests -> no guess
active Hero changes in another host after context -> Start retains explicitly selected heroId
ambiguous Start retry -> same startRequestId
ambiguous Finish retry -> same finishRequestId/payload
conflicting finalized Quest -> honor HP136
reported vs observed -> never promote
Hero Passport MCP calls -> no self-awarded tool_use
milestone flavor -> presentation only
Skill/Core version mismatch -> fail safe with upgrade guidance
```

Skill evals are product release gates but never substitutes for Core/storage invariants.
