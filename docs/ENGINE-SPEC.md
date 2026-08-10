# Hero Passport — RPG Engine Specification

**Status:** Accepted for reward rule `1.0.0`  
**Baseline:** 2026-08-10  
**Principle:** deterministic, integer-only, explainable, replayable

## 1. Rule ownership

The RPG engine is pure domain logic. It consumes normalized quest facts and returns a complete immutable calculation result. It does not read a database, inspect code, call an LLM, run tests or infer facts from raw logs.

Persist the rule versions used for every completed quest.

Initial versions:

```text
rewardRuleVersion     1.0.0
levelRuleVersion      1.0.0
skillRuleVersion      1.0.0
trustRiskRuleVersion  1.0.0
traitRuleVersion      1.0.0
```

## 2. Canonical enums/keys

### Quest types

```text
planning
research
coding
review
debugging
documentation
maintenance
```

### Results

```text
success
partial
failed
blocked
abandoned
```

### Initial skills

```text
planning
research
coding
review
debugging
documentation
maintenance
testing_awareness
scope_control
tool_use
```

Persist canonical keys only.

## 3. Quest quality flags

Before reward calculation, construct:

```csharp
public sealed record QuestQualityFlags(
    bool HasTestsMentioned,
    bool HasCleanScope,
    bool HasClearSummary,
    bool HasNoUserCorrections,
    bool HasBuildPassed,
    bool HasTestsPassed);
```

Mapping for `1.0.0`:

```text
HasTestsMentioned      = metrics.testsMentioned
HasCleanScope          = metrics.scopeViolations == 0
HasClearSummary        = trimmed summary length >= 40
HasNoUserCorrections   = metrics.userCorrections == 0
HasBuildPassed         = metrics.buildStatus == passed
HasTestsPassed         = metrics.testsStatus == passed
```

`HasBuildPassed` and `HasTestsPassed` are recorded but do not independently change XP in reward rule `1.0.0`. This avoids silently changing the source report's baseline while preserving stronger evidence for a future version.

## 4. Base XP

```text
planning        30
research        40
coding          60
review          50
debugging       70
documentation   40
maintenance     40
```

Unknown quest type is invalid, never treated as zero XP.

## 5. Result multipliers

Use integer permille, not floating point:

```text
success      1000
partial       600
failed        200
blocked       300
abandoned       0
```

Result XP:

```text
resultXp = floor(baseXp * multiplierPermille / 1000)
```

This arithmetic is deterministic on every platform.

## 6. Bonuses

Applied after the result multiplier:

```text
HasTestsMentioned       +10  tests_mentioned
HasCleanScope           +10  clean_scope_bonus
HasClearSummary         +10  clear_summary
HasNoUserCorrections     +5  no_user_corrections
```

## 7. Penalties

Applied after bonuses:

```text
scopeViolations         -25 each  scope_violation
summary length < 40     -10       unclear_summary
userCorrections         -10 each  user_correction
```

`scopeViolations` and `userCorrections` must be non-negative integers. Contract validation rejects negative values and caps each counter at `100` to prevent overflow/absurd payloads.

## 8. XP calculation order

Exact formula:

```text
baseXp
-> resultXp = floor(baseXp * multiplier / 1000)
-> add qualifying bonuses
-> subtract penalties
-> finalXp = max(0, subtotal)
```

No hidden cap is applied in `1.0.0`; with bounded inputs, the maximum is naturally bounded.

### Standard golden fixture

```text
questType            coding        -> base 60
result               success       -> 60
HasTestsMentioned     true          -> +10
scopeViolations      0             -> +10
summary >= 40                      -> +10
userCorrections      0             -> +5
-----------------------------------------
finalXp                            = 95
```

This fixture is a compatibility lock. Changing it requires a new reward rule version.

## 9. Reward breakdown

The engine returns an immutable breakdown:

```csharp
public sealed record RewardBreakdown(
    string RuleVersion,
    int BaseXp,
    int ResultMultiplierPermille,
    int ResultXp,
    IReadOnlyList<RewardAdjustment> Bonuses,
    IReadOnlyList<RewardAdjustment> Penalties,
    int FinalXp);

public sealed record RewardAdjustment(string Key, int Xp);
```

Adjustment order is deterministic and part of golden output:

Bonuses:

```text
tests_mentioned
clean_scope_bonus
clear_summary
no_user_corrections
```

Penalties:

```text
scope_violation (one aggregated adjustment with total negative XP)
unclear_summary
user_correction (one aggregated adjustment with total negative XP)
```

## 10. Hero level progression

### 10.1 Level XP curve

Hero begins at level `1` with `0` total XP.

XP required to advance from level `L` to `L+1`:

```text
xpToNext(L) = 100 + 50 * (L - 1)
```

Therefore:

| Level | Total XP at level start | XP to next |
|---:|---:|---:|
| 1 | 0 | 100 |
| 2 | 100 | 150 |
| 3 | 250 | 200 |
| 4 | 450 | 250 |
| 5 | 700 | 300 |
| 6 | 1000 | 350 |

Closed-form threshold for level `L`:

```text
threshold(L) = (L - 1) * (25 * L + 50)
```

Equivalent to summing the per-level requirements. Use checked `long` arithmetic for total XP/thresholds.

### 10.2 Level read model

For total XP, derive:

```text
level
levelStartTotalXp
nextLevelTotalXp
levelXp = totalXp - levelStartTotalXp
nextLevelXp = nextLevelTotalXp - levelStartTotalXp
xpRemaining = nextLevelTotalXp - totalXp
```

At 95 total XP:

```text
level = 1
levelXp = 95
nextLevelXp = 100
xpRemaining = 5
```

Do not persist `level` as an independent source of truth unless used as an update projection; total XP is sufficient to derive it and tests must verify any cached value.

## 11. Skill normalization

Initial aliases:

```text
code, implementation        -> coding
tests, test                 -> testing_awareness
scope, control              -> scope_control
docs, doc                   -> documentation
tools, tool                 -> tool_use
plan                         -> planning
researching                  -> research
reviewing                    -> review
debug, debugging             -> debugging
maintain                     -> maintenance
```

Normalization:

1. trim;
2. invariant lowercase;
3. replace spaces/hyphens with underscore only for recognized aliases;
4. map alias to canonical key;
5. drop unknown keys;
6. remove duplicates preserving first canonical occurrence;
7. keep at most first 3 canonical skills.

The server must not dynamically create new skills from arbitrary model text.

## 12. Skill XP allocation

Reward XP is distributed across normalized `skillsUsed`:

```text
1 skill    100
2 skills    60 / 40
3 skills    50 / 30 / 20
0 skills     no skill XP; hero still receives quest XP
```

Use **cumulative floor allocation** to avoid floating point and guarantee exact conservation.

For weights `[50,30,20]` and total XP `95`:

```text
boundary1 = floor(95 * 50 / 100) = 47  -> skill1 47
boundary2 = floor(95 * 80 / 100) = 76  -> skill2 29
skill3 = 95 - 76                         -> skill3 19
```

Result:

```text
47 + 29 + 19 = 95
```

This intentionally matches the standard UI fixture.

If `finalXp == 0`, all skill deltas are zero.

## 13. Skill progression presentation

MVP persists cumulative XP per hero skill. It does **not** need an independent skill-level formula for correctness. Presentation may derive a simple rank later, but no rank is persisted or used for rewards in rule `1.0.0`.

This avoids introducing a second progression curve before the dashboard needs it.

## 14. Trust / Risk

Initial hero values:

```text
trust = 50
risk  = 20
```

Clamp after each quest:

```text
0..100
```

### 14.1 Trust delta

Base result:

```text
success     +1
partial      0
failed      -2
blocked      0
abandoned    0
```

Additional:

```text
scope violation   -3 each
user correction   -1 each
```

Then:

```text
trustAfter = clamp(trustBefore + totalDelta, 0, 100)
```

### 14.2 Risk delta

Base result:

```text
success with zero scope violations   -1
success with scope violation(s)        0 base delta
partial                               +1
failed                                +3
blocked                               +1
abandoned                             +1
```

Additional:

```text
scope violation   +5 each
user correction   +1 each
```

Then clamp `0..100`.

### 14.3 Standard fixture

Successful clean coding quest from defaults:

```text
trust 50 -> 51
risk  20 -> 19
```

## 15. Traits

Traits are persistent behavioral descriptors, **not an achievements system**. They do not award XP and do not unlock items in MVP.

Initial trait set is deliberately small:

### `precise_executor` — `Точный исполнитель`

Progress +1 for each `success` quest with:

```text
scopeViolations == 0
userCorrections == 0
```

Activate at progress `5`.

### `test_scout` — `Разведчик тестов`

Progress +1 for each `success` `coding` or `debugging` quest with:

```text
testsMentioned == true
```

Activate at progress `5`.

### `quest_finisher` — `Завершитель квестов`

Progress +1 for each quest finished with result `success` or `partial`.

Activate at progress `10`.

### Trait state

```text
locked   progress below threshold
active   threshold reached
```

Once active, the trait remains active in `1.0.0`; future dynamic/decaying personality traits require a new trait rule version and separate design.

Deferred trait ideas (`streak_runner`, `code_sentinel`, `risky_hero`) are not part of the initial rule because they introduce streak/state-window semantics that should be designed explicitly.

## 16. Project stats

For each `(hero, project)` update atomically with quest completion:

```text
questsStarted
questsFinished
questsSucceeded
xpEarned
lastQuestAtUtc
```

Do not make these counters the reward source of truth. They are projections for cards/dashboard and can be rebuilt from quest/reward history if necessary.

## 17. Immutable history

After a quest is finished:

- its result/summary/metrics are immutable in MVP;
- its reward breakdown is immutable;
- its rule versions are immutable;
- its XP ledger event is immutable.

Administrative correction/reversal is post-MVP and must be implemented as a compensating event, not destructive mutation of history.

## 18. Engine test matrix

Minimum golden/boundary coverage:

- each quest type × each result multiplier;
- standard `95 XP` fixture;
- every individual bonus;
- each penalty at 0, 1 and maximum accepted count;
- bonus + penalty combinations;
- clamp final XP at zero;
- trust/risk clamp at 0 and 100;
- skill allocation for totals 0,1,2,3,5,95 and max possible XP;
- alias normalization/duplicates/unknown skills;
- level boundaries at 0, 99, 100, 249, 250, 449, 450;
- trait threshold exactly before/at/after activation;
- replay of stored inputs under explicit rule version.

Golden changes are reviewed as product-rule changes, not ordinary test updates.
