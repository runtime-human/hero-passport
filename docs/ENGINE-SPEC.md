# Hero Passport — deterministic RPG engine specification

**Status:** Accepted rule set v1  
**Snapshot:** 2026-08-10  
**Reward rule:** `reward/1.0.0`  
**Trust/Risk rule:** `trust-risk/1.0.0`  
**Trait rule:** `traits/1.0.0`

## 1. Engine boundary

The RPG engine is a pure deterministic Domain component.

Inputs are typed canonical values. Outputs are typed numeric/state changes.

It does **not**:

```text
read/write SQLite
read configuration
use MCP types
render localized text
call an LLM
read code/diffs/logs
inspect Git
use DateTime.UtcNow directly
perform network I/O
```

`displayText`, emoji, Russian/English labels and line formatting belong to `HeroPassport.App/Presentation`.

This separation prevents a punctuation/localization change from becoming a game-rule change.

---

## 2. Quest types

Canonical stable keys:

```text
planning
research
coding
review
debugging
documentation
maintenance
```

Unknown types are rejected. Do not persist arbitrary model-invented quest types.

Base XP:

| Quest type | Base XP |
|---|---:|
| planning | 30 |
| research | 40 |
| coding | 60 |
| review | 50 |
| debugging | 70 |
| documentation | 40 |
| maintenance | 40 |

Base values are part of `reward/1.0.0`.

---

## 3. Quest result

Canonical keys and integer permille multiplier:

| Result | Multiplier permille |
|---|---:|
| success | 1000 |
| partial | 600 |
| failed | 200 |
| blocked | 300 |
| abandoned | 0 |

Formula:

```text
resultXp = floor(baseXp * multiplierPermille / 1000)
```

Use integer arithmetic only.

No `double`/`decimal` is needed for v1 reward calculation.

---

## 4. Quality input

Application constructs a typed metrics input from the validated MCP/CLI contract:

```text
testsMentioned: bool
scopeViolations: integer 0..20
userCorrections: integer 0..20
buildStatus: not_run | passed | failed | unknown
testsStatus: not_run | passed | failed | unknown
summaryLength: integer derived by Application after normalization
```

The engine receives values, not raw logs or source code.

### Why build/tests statuses are stored even though reward v1 does not directly score pass/fail

They are useful compact historical quality facts and leave room for a future rule version. Reward v1 intentionally does not grant extra XP based on self-reported `passed` beyond the explicit tests-mentioned signal; we avoid pretending agent-reported evidence is independently verified.

A future local verifier can create a new rule version without changing historical reports.

---

## 5. QuestQualityFlags

Derived before reward calculation:

```text
HasTestsMentioned     = testsMentioned
HasCleanScope         = scopeViolations == 0
HasClearSummary       = normalized summary length >= 40
HasNoUserCorrections  = userCorrections == 0
HasBuildPassed        = buildStatus == passed
HasTestsPassed        = testsStatus == passed
```

Conceptual type:

```csharp
public readonly record struct QuestQualityFlags(
    bool HasTestsMentioned,
    bool HasCleanScope,
    bool HasClearSummary,
    bool HasNoUserCorrections,
    bool HasBuildPassed,
    bool HasTestsPassed);
```

Flags are explicit so the reward breakdown is explainable/testable.

---

## 6. Reward bonuses

`reward/1.0.0`:

```text
HasTestsMentioned       +10
HasCleanScope           +10
HasClearSummary         +10
HasNoUserCorrections     +5
```

Bonuses are additive integers.

Russian presentation labels are not engine data, but canonical mapping includes:

```text
clean_scope -> Бонус за контроль
```

---

## 7. Reward penalties

`reward/1.0.0`:

```text
scopeViolations  -25 each
userCorrections  -10 each
missing/short summary -10
```

Where:

```text
missing/short = normalized summary length < 40
```

Note that clean-scope bonus and scope-violation penalty cannot apply simultaneously because they derive from the same counter.

`HasNoUserCorrections` bonus and correction penalty similarly cannot both apply.

Canonical Russian presentation term:

```text
scope_violation -> Выход за задачу
```

---

## 8. Final XP formula

```text
baseXp     = QuestTypeBase[type]
resultXp   = floor(baseXp * resultMultiplierPermille / 1000)
bonusXp    = sum(applicable bonuses)
penaltyXp  = sum(applicable penalties as positive magnitude)
rawXp      = resultXp + bonusXp - penaltyXp
xpGained   = max(0, rawXp)
```

Reward never makes total XP negative.

Persist breakdown components with the quest report.

---

## 9. Canonical 95 XP golden

Input:

```text
questType = coding
result = success
testsMentioned = true
scopeViolations = 0
userCorrections = 0
summary length >= 40
```

Calculation:

```text
coding base                  60
success ×1.0                 60
Tests mentioned             +10
Clean scope                 +10
Clear summary               +10
No user corrections          +5
--------------------------------
XP gained                    95
```

This fixture is immutable for `reward/1.0.0`.

Any document/example saying the same inputs produce 85 XP is stale and must fail documentation review.

---

## 10. Level curve

`totalXp` is the authoritative hero progression value.

XP required to advance from level `L`:

```text
xpToNext(L) = 100 + 50 * (L - 1)
```

Total XP threshold at the beginning of level `L`:

```text
threshold(L) = (L - 1) * (25L + 50)
```

Examples:

| Level | threshold | XP to next |
|---:|---:|---:|
| 1 | 0 | 100 |
| 2 | 100 | 150 |
| 3 | 250 | 200 |
| 4 | 450 | 250 |
| 5 | 700 | 300 |
| 6 | 1000 | 350 |

Derived card values:

```text
level
levelXp = totalXp - threshold(level)
levelXpRequired = xpToNext(level)
xpRemaining = levelXpRequired - levelXp
```

Implement lookup/formula with checked integer behavior. Set a sane persistence maximum if needed to avoid integer overflow; ordinary MVP values are tiny.

---

## 11. Skills

Canonical keys are persisted; localized labels are presentation-only.

Minimum canonical set:

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

`scope_control` Russian label is exactly:

```text
Контроль
```

Do not rename the persisted key when changing UI wording.

---

## 12. SkillKeyNormalizer

Small documented aliases only.

Initial examples:

```text
code, implementation      -> coding
test, tests               -> testing_awareness
scope, control            -> scope_control
doc, docs                 -> documentation
tool, tools               -> tool_use
plan                      -> planning
research                  -> research
debug, debugging          -> debugging
review                    -> review
maintenance               -> maintenance
```

Algorithm:

```text
trim
lowercase invariant
normalize alias -> canonical
reject unknown
remove duplicate preserving first occurrence
max 3 canonical skills
```

MCP schema already limits three items, but Application normalization remains authoritative.

Do not auto-create a skill from an unknown LLM string.

---

## 13. Skill XP distribution

All quest XP is attributed to declared skills.

Weights:

```text
1 skill: 100%
2 skills: 60%, 40%
3 skills: 50%, 30%, 20%
```

Use cumulative-floor allocation to guarantee conservation under integer rounding.

For total `X` and cumulative weights `C[i]`:

```text
boundary[i] = floor(X * C[i] / 100)
allocation[0] = boundary[0]
allocation[i] = boundary[i] - boundary[i-1]
last absorbs exact remainder by cumulative 100%
```

Golden for 95 XP / three skills:

```text
floor(95*50/100) = 47
floor(95*80/100) = 76 -> second = 29
floor(95*100/100)=95 -> third  = 19

47 + 29 + 19 = 95
```

Invariant:

```text
sum(skill allocations) == xpGained
```

If `xpGained == 0`, valid declared skills receive zero and no invented minimum XP.

---

## 14. Trust/Risk initial state

Default new hero:

```text
Trust = 50
Risk  = 20
```

Clamp every result to:

```text
0..100
```

---

## 15. Trust rules v1

```text
success               +1 Trust
failed                -2 Trust
scope violation       -3 Trust each
user correction       -1 Trust each
```

Other result types have no direct trust delta in v1 unless another rule above applies.

Formula applies all relevant deltas then clamps once.

No hidden stochastic factor.

---

## 16. Risk rules v1

```text
success with zero scope violation   -1 Risk
partial                              +1 Risk
failed                               +3 Risk
blocked                              +1 Risk
abandoned                            +1 Risk
scope violation                      +5 Risk each
user correction                      +1 Risk each
```

For `success` with one or more scope violations, do not apply the clean-success `-1`; apply violation deltas.

Clamp 0..100 after summed delta.

---

## 17. Traits v1

Traits are persistent behavioral characteristics, not an achievement shelf.

MVP has exactly three fully specified traits.

### `precise_executor`

Russian: `Точный исполнитель`.

Progress +1 when:

```text
result == success
AND scopeViolations == 0
AND userCorrections == 0
```

Unlock at progress >= 5.

### `test_scout`

Russian: `Разведчик тестов`.

Progress +1 when:

```text
questType in {coding, debugging}
AND result == success
AND testsMentioned == true
```

Unlock at progress >= 5.

### `quest_finisher`

Russian: `Завершитель квестов`.

Progress +1 when:

```text
result in {success, partial}
```

Unlock at progress >= 10.

### Trait invariant

Under `traits/1.0.0`:

```text
unlocked -> remains unlocked
```

No automatic relock.

Deferred traits such as streak/risk/code-sentinel require separate temporal definitions and are not partially implemented.

---

## 18. Rule result types

Conceptual deterministic result:

```csharp
public sealed record RewardBreakdown(
    int BaseXp,
    int ResultXp,
    int BonusXp,
    int PenaltyXp,
    int XpGained,
    QuestQualityFlags Quality,
    string RewardRuleVersion);
```

Trust/Risk and traits use separate typed results/version identifiers.

Do not embed localized strings in these objects.

---

## 19. Rule versioning

Persist rule versions on completed report/event because historical outcomes must remain explainable.

Version changes required when semantics change, including:

```text
base XP
multiplier
bonus/penalty amount
bonus/penalty condition
level formula if historical before/after projections depend on it
skill distribution rule
Trust/Risk delta
trait progress/unlock condition
```

Presentation wording does not increment a game-rule version.

Skill alias addition that maps another spelling to an existing canonical key normally increments a normalization/config semantic version only if historical interpretation needs distinguishing; it does not retroactively rewrite stored canonical keys.

---

## 20. Historical immutability

Once a quest completes:

```text
its reward outcome is immutable
its rule versions are immutable
retry reads stored outcome
```

A future recalculation experiment must be an explicit new feature producing a separate derived projection—not silent mutation of earned XP.

---

## 21. Deferred engine mechanics

Not in v1:

```text
achievements
items/artifacts
streak engine
EWMA reliability
season resets
random loot
LLM judge
code quality scoring
XP per line/diff/file/token
negative total XP
skill decay
trait relocking
team/shared XP
```

No extension framework is created for these before an accepted product requirement exists.

---

## 22. Engine test invariants

At minimum:

```text
same inputs + same rule version -> identical result
xpGained >= 0
sum(skill XP) == xpGained
Trust/Risk stay 0..100
historical retry never invokes current reward calculation
unknown keys rejected
95 XP golden stable
all level thresholds exact
traits unlock exactly at threshold
unlocked traits never relock under v1
```

Property-style loops over bounded integer inputs are encouraged without adding a property-testing library unless ordinary xUnit loops become unwieldy.

## 23. Presentation mapping

The engine owns canonical semantic keys only.

Example separation:

```text
Domain key: scope_control
Presentation RU: Контроль

Domain reward component: clean_scope_bonus
Presentation RU: Бонус за контроль

Domain issue: scope_violation
Presentation RU: Выход за задачу
```

This mapping is golden-tested in App presentation tests, not reward tests.
