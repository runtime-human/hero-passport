# Hero Passport — Configuration and Onboarding

**Status:** Accepted v3.2.1  
**Snapshot:** 2026-08-11

## 1. Principle

Configuration is small, typed and user-owned. Game progression is not configuration.

Never expose config fields for XP, levels, ranks, Skill XP, Trust/Strain, streak, Traits/Titles or historical Quest outcomes.

## 2. Typed singleton

Persistence is one typed `app_settings` row (`id=1`), not a generic KV store.

Stored values:

```text
setup_completed
active_hero_id
locale
presentation_style
auto_start_quest
auto_finish_quest
project_identity_salt_v1
config_version
```

Before setup: `setup_completed=false`, `active_hero_id=NULL`.

After setup: active Hero must be non-null.

## 3. First run

Short onboarding remains:

1. locale (`ru-RU` / `en-US`);
2. initial Hero name;
3. presentation style;
4. auto-start Quest preference;
5. auto-finish Quest preference + confirmation.

Defaults:

```text
presentationStyle = rpg_engineering
autoStartQuest    = true
autoFinishQuest   = true
```

## 4. CLI first run

Canonical command:

```text
hero-passport init
```

Interactive CLI may ask step-by-step terminal questions. Script/non-interactive paths use explicit flags/input and never hang for prompts.

The CLI ultimately executes the same bootstrap Application use case and crash-safe request semantics.

## 5. MCP first run

stdio remains protocol-pure; no onboarding prompts on stdout.

Before setup:

```text
hero.get_context -> allowed
hero.bootstrap   -> allowed
all other HP-MCP tools -> HP001 setup_required
```

The Agent Skill conducts conversational setup and calls `hero.bootstrap` with a fresh `bootstrapRequestId`.

Bootstrap is resource creation and is separately crash-idempotent. It is no longer overloaded into `hero.configure`.

Fresh bootstrap after setup -> `HP002 setup_already_completed`.

## 6. Mutable preferences

Post-setup `hero.configure` changes only:

```text
locale
presentationStyle
autoStartQuest
autoFinishQuest
```

No Hero name field exists in configure.

Repeating the same complete preference set is a no-op success.

## 7. Runtime hydration

A separately installed/restarted Skill must not rely on remembered defaults.

At relevant startup/recovery, `hero.get_context` returns effective persisted preferences and compatibility versions.

This prevents `autoStartQuest=false` or presentation/locale preferences from being ignored after a host restart.

## 8. Active Hero semantics

`active_hero_id` is the global **default preference** for forming a new Quest.

It is not hidden ownership context inside `StartQuest`.

The Skill reads it via `hero.get_context`, then passes explicit `heroId` to `hero.start_quest`.

A concurrent activation in another host therefore cannot retarget an already-formed Start request.

## 9. Locale semantics

Global locale affects general UI and new Quest presentation.

A new Quest snapshots the current effective locale inside the Start writer transaction after idempotency receipt lookup. Replays return the original persisted Quest locale even if global locale later changes.

0.1 resources must be complete for:

```text
ru-RU
en-US
```

Missing keys fail tests/CI.

## 10. Presentation style

MVP enum:

```text
rpg_engineering
classic_rpg
minimal
```

Presentation changes formatting/flavor only, never game calculations.

## 11. Skill/Core compatibility

`hero.get_context` exposes at least:

```text
productVersion
contractVersion
skillContractVersion
ruleVersions
```

Portable Skill package declares its expected `hero-passport-skill/1` compatibility metadata.

If Core/Skill contract is incompatible, Skill must surface upgrade guidance and avoid guessing changed wire semantics.

## 12. Project root override

CLI/integration may supply explicit `--project-root`; otherwise current working directory resolves through `project-identity/1`.

This is invocation configuration, not persisted profile data.

## 13. Environment/data locations

`HERO_PASSPORT_HOME` is the dev/test isolation override.

Do not create a broad environment-variable tuning surface for game rules.

## 14. Validation

All configuration is validated at one typed boundary. Unknown keys, malformed locales/styles/names fail deterministically with safe errors.

XP/rule thresholds remain versioned game content, not mutable user config.
