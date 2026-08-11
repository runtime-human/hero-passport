# Hero Passport — Agent Skill Contract

**Status:** Accepted v1 orchestration contract for Hero Passport v3.2  
**Snapshot:** 2026-08-11

This document defines the behavior of the official Hero Passport Agent Skill. It is not a game-engine contract: all rewards and durable invariants remain server-authoritative.

## 1. Purpose

The Skill makes Hero Passport feel ambient during normal agent work:

```text
recognize meaningful work
-> start/resume Quest
-> work normally
-> recognize completion or explicit switch
-> report bounded facts
-> finish Quest
-> render canonical RPG result
```

The user should rarely need to mention Hero Passport.

## 2. Packaging

Ship a portable Agent Skill:

```text
skills/hero-passport/
  SKILL.md
  references/
    lifecycle.md
    finish-facts.md
    presentation.md
    recovery.md
```

`SKILL.md` must remain concise and follow the open Agent Skills format. Detailed edge cases belong in focused references loaded on demand.

Validate distributable Skill content with the current Agent Skills reference validator (`skills-ref validate`) when available in release tooling.

Host-specific setup belongs in `docs/integrations/*`, not in the portable lifecycle instructions.

## 3. Activation intent

The Skill is relevant when the user works with a project and Hero Passport MCP tools are available, including implementation, debugging, review, planning, research, documentation, maintenance or testing work.

It should not force a Quest for casual conversation, short factual questions or low-effort clarification with no concrete project work.

## 4. Lifecycle state machine

Conceptual local reasoning states:

```text
NO_QUEST
ACTIVE_QUEST(questId, title, goal)
```

These are model/orchestration concepts, not trusted server state. Before relying on an uncertain remembered state, use `hero.list_active_quests`.

### NO_QUEST -> ACTIVE_QUEST

Start when meaningful project work is clearly beginning.

The Skill generates:

```text
startRequestId = fresh UUIDv7 for this start intent
questType
short title
precise goal
```

The same `startRequestId` must be reused only when retrying that same start call after an ambiguous transport/tool result. A separate intended Quest always gets a new request ID.

On success retain `questId` in working context.

### ACTIVE_QUEST -> ACTIVE_QUEST

Continue when follow-up work is materially part of the same goal. Examples:

- add a test for the same fix;
- adjust naming discovered during implementation;
- update docs directly required by the feature;
- continue after user supplies requested information.

Do not fragment one coherent goal into micro-Quests.

### ACTIVE_QUEST -> FINISHED

Finish only when the goal is genuinely complete and the agent is ready to present the final work result.

Do not finish merely because:

- one sub-step ended;
- the agent needs a user decision;
- the agent is about to run verification;
- the agent says “I can also…” while required work remains.

### Explicit goal switch

If the user clearly changes to an independent goal before the old one is complete:

```text
useful completed result exists -> finish old as partial
nothing useful completed       -> finish old as abandoned
then start new Quest
```

If the switch is ambiguous, keep the old Quest and do not guess.

## 5. Conservative automation

Default policy is asymmetric in favor of avoiding user friction and false state:

```text
unsure whether to start  -> do not ask, wait for clearer work
unsure whether to finish -> keep Quest open
```

Manual user commands override this policy:

```text
“начни квест” / equivalent -> start if server invariant allows
“заверши квест”            -> finish with truthful current outcome/facts
“не заканчивай”            -> keep open
“брось/отмени квест”       -> abandoned
```

## 6. Recovery and handoff

When the Skill starts/reloads in a project and does not have a reliable `questId`, call `hero.list_active_quests` when meaningful work is about to begin.

Because the Core permits at most one open Quest for the active Hero+Project:

- if none exists, normal start may proceed;
- if one exists and the new work clearly continues it, resume that `questId`;
- if one exists but the new goal differs, surface a concise continue/finish/abandon choice unless the user explicitly requested a switch that already determines `partial`/`abandoned` semantics.

Never invent a replacement `questId` or infer retry identity from similar text.

## 7. Multiple agents

The Skill must treat a Quest as shared durable work state for the Hero+Project, not as owned by the current agent.

If another agent already opened the Quest, this agent may resume it when the goal matches. Agent brand/name is not persisted as ownership and must never affect XP.

## 8. Finish facts

The Skill reports only truthful bounded facts it can derive from the interaction.

### Outcome

```text
success    goal accomplished
partial    useful subset accomplished but requested goal not fully done
blocked    meaningful work cannot continue because of an external blocking condition
failed     attempted result did not reach a usable state
abandoned  intentionally stopped without a scored result
```

Do not classify a normal user-requested reprioritization as failure.

### Skills

Choose 1–3 canonical skills actually important to the Quest, ordered most-to-least important. Do not add `tool_use` merely because Hero Passport MCP itself was called.

### Build/test evidence

Evidence semantics:

```text
observed  this agent directly invoked/observed the relevant check and result
reported  user or another source stated the result; this agent did not directly verify it
none      no supporting observation/report applies
```

Never promote `reported` to `observed`.

Do not send raw logs, source, diffs or command transcripts.

### Scope violations

Count concrete departures from the requested goal that required unnecessary work or introduced out-of-scope changes. Do not count normal discovery or necessary adjacent fixes.

### User corrections

Count substantive corrections where the user had to redirect an incorrect assumption/output. Do not count ordinary preference choices or requested refinements.

## 9. Presentation

### Start

Default start output is one short line and uses the Quest title:

```text
⚔ Добавить first-run onboarding
```

Avoid boilerplate such as “Quest started:” unless a host’s UX requires it.

### Finish

First present concise work completion information, then canonical Hero Passport reward logs/table. The Skill may reformat canonical numeric fields but must not recalculate them.

Example shape:

```text
+60 XP  Базовая награда
+10 XP  Тестирование
+10 XP  Бонус за контроль
+10 XP  Итоговый отчёт
 +5 XP  Без исправлений

↑ Coding             +48 XP
↑ Testing Awareness  +29 XP
↑ Scope Control      +18 XP
★ Level 7 → 8

XP       +95
Level    7 → 8
Trust   52 → 54
Strain  18 → 16
Streak       6 🔥
```

Use semantic result fields as authority even if fallback `displayText` exists.

## 10. Flavor text

Core returns curated milestone flavor keys/text. The Skill may lightly contextualize a line to the current Quest, for example referencing migrations or legacy code, but it may not:

- invent extra XP;
- invent an unlock;
- change Rank/Title/Trait meaning;
- turn every Quest into comedy.

Flavor is normally shown only for significant level/rank/title/trait/streak milestones.

## 11. Onboarding

If a gameplay call returns `HP001 setup_required`, the Skill conducts the five-step setup conversationally:

1. language;
2. Hero name;
3. presentation style;
4. auto-start/auto-finish preferences;
5. confirmation.

Then call `hero.configure` once with validated values. Do not instruct the user to edit SQLite/config files manually as the normal flow.

## 12. Language

Use the Quest’s effective locale for Hero Passport presentation. The Skill may converse in the user’s current language independently, but must not translate canonical game values differently from the server’s localization mapping.

Supported MVP presentation locales:

```text
ru-RU
en-US
```

## 13. Failure handling

Tool errors are handled by stable code:

```text
HP001 -> onboarding
HP133 -> recover/resolve existing active Quest
HP135 -> do not invent a new request ID for the same retry; inspect caller logic
HP202 -> bounded retry of the same safe request identity after transient DB busy condition
other expected HP errors -> explain safe corrective action
```

Never automatically abandon an existing Quest merely to make a new start succeed.

## 14. Evaluation requirements

AgentEvals must cover at least:

```text
short factual question -> no Quest
meaningful implementation request -> auto-start
multi-step same goal -> one Quest
clarification mid-work -> remains open
complete goal -> auto-finish
explicit different goal after complete -> finish then new start
explicit mid-work switch -> partial/abandoned then new start
ambiguous switch -> no silent close
restart with matching open Quest -> resume
restart with different request -> surface recovery choice
same start transport retry -> reuse startRequestId
build/test reported vs observed provenance
Hero Passport MCP use -> no self-awarded tool_use
milestone result -> flavor without changed facts
```

These evals are release gates alongside MCP contract tests; Skill behavior is part of the product UX but never a substitute for Core invariants.
