# Hero Passport — Configuration and Onboarding

**Status:** Accepted v3.2  
**Snapshot:** 2026-08-11

## 1. Principle

Configuration is small, typed and user-owned. Game state is not configuration.

Do not allow config/API fields for XP, levels, ranks, Skills, Trust, Strain, streak, Traits, Titles or historical Quest outcomes.

## 2. First-run state

Persist `setup_completed=false` until the first setup transaction succeeds.

Short onboarding:

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

Locale is inferred by the host/agent when reasonable but explicitly confirmed as part of onboarding. User override always wins.

## 3. CLI

Canonical first-run command:

```text
hero-passport init
```

Interactive CLI may ask step-by-step questions on the terminal.

Script/non-interactive paths must have explicit flags/JSON input rather than hanging for prompts.

## 4. MCP first run

stdio transport must remain protocol-pure.

Before setup:

```text
hero.configure -> allowed
other HP-MCP game/hero tools -> HP001 setup_required
```

The Agent Skill handles conversational setup and sends the completed setting set through `hero.configure`.

## 5. Mutable settings

Post-setup `hero.configure` can change only:

```text
locale
presentationStyle
autoStartQuest
autoFinishQuest
```

Initial Hero name is onboarding-only through this tool; Hero lifecycle is managed through Hero operations, not generic config.

## 6. Locale semantics

Global locale affects general UI and new Quest presentation.

A Quest snapshots effective locale at start. Historical game facts remain semantic keys/numbers and are not rewritten when locale changes.

0.1 resources must be complete for:

```text
ru-RU
en-US
```

Missing keys fail tests/CI.

## 7. Presentation style

MVP enum:

```text
rpg_engineering  default; concise RPG + developer vocabulary
classic_rpg      less engineering humor
minimal          numbers/status with minimal flavor
```

Style changes formatting/flavor only, never game calculations.

## 8. Environment/config locations

App data uses OS-standard locations documented in `DISTRIBUTION.md`.

Development/test isolation override:

```text
HERO_PASSPORT_HOME
```

Do not create a broad environment-variable configuration surface for game rules.

## 9. Project root override

CLI/integration may supply explicit `--project-root`; otherwise current working directory is resolved by `project-identity/1`.

This is process/invocation configuration, not stored user profile data.

## 10. Validation

All configuration is validated at a single boundary and exposed internally as typed options/value objects.

Unknown keys, malformed locales, unknown presentation styles and invalid Hero names fail deterministically with safe errors.

## 11. No hidden tuning

XP/rule thresholds are versioned game content in Domain, not user configuration in 0.1. This avoids different agents silently changing the economy.
