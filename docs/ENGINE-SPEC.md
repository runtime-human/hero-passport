# Hero Passport — Deterministic RPG Engine Specification

**Status:** Accepted v3.2.1 game contract  
**Snapshot:** 2026-08-11

Rule versions:

```text
reward/2.0.0
hero-progression/2.0.0
skill-progression/2.0.0
skill-allocation/1.0.0
trust-strain/1.0.0
streak/1.0.0
unlock/2.0.0
rank/1.0.0
```

## 1. Engine boundary

The engine is a pure deterministic Domain component. It receives canonical typed data and returns numeric/semantic changes.

It does not read SQLite/config/Git/files, call MCP/LLMs/network, inspect source/diffs/logs, localize strings, select presentation flavor or read the system clock.

Historical completed outcomes keep the exact rule versions/deltas that produced them.

The engine is deterministic **given validated bounded attestations**. It does not claim those agent-supplied signals were independently verified.

## 2. Quest types and base XP

| Quest type | Base XP |
|---|---:|
| `planning` | 30 |
| `research` | 40 |
| `coding` | 60 |
| `review` | 50 |
| `debugging` | 70 |
| `documentation` | 40 |
| `maintenance` | 40 |

Unknown values are rejected.

## 3. Outcome multiplier

Use integer permille only:

| Result | Permille |
|---|---:|
| `success` | 1000 |
| `partial` | 600 |
| `blocked` | 300 |
| `failed` | 100 |
| `abandoned` | 0 |

## 4. Bounded attestations

Canonical validated input:

```text
testsMentioned: bool
scopeViolations: 0..20
userCorrections: 0..20
buildStatus: not_run | passed | failed | unknown
buildEvidence: observed | reported | none
testsStatus: not_run | passed | failed | unknown
testsEvidence: observed | reported | none
summaryScalarLength: derived after SafeText
```

`observed` means the agent asserts it directly ran/saw the relevant result. It is not independent Core verification.

Cross-field validation occurs before the engine. Raw logs/source/diffs are never engine input.

Derived flags:

```text
HasObservedTestsPassed = testsStatus == passed && testsEvidence == observed
HasCleanScope          = scopeViolations == 0
HasClearSummary        = summaryScalarLength >= 40
HasNoUserCorrections   = userCorrections == 0
```

## 5. Reward components — `reward/2.0.0`

Bonuses:

```text
HasObservedTestsPassed +10
HasCleanScope          +10
HasClearSummary        +10
HasNoUserCorrections    +5
```

Penalties:

```text
scope violations  -5 each, maximum magnitude 15
user corrections  -5 each, maximum magnitude 15
```

Absence of a bonus is not an extra penalty.

## 6. XP formula

```text
baseXp      = base table value
bonusXp     = sum applicable bonuses
penaltyXp   = min(scopeViolations, 3) * 5
            + min(userCorrections, 3) * 5
rawXp       = max(0, baseXp + bonusXp - penaltyXp)
questXp     = floor(rawXp * outcomePermille / 1000)
```

No floating point.

No reward term uses elapsed time, tokens, files, lines, diff size, agent identity or model-reported complexity.

## 7. Canonical goldens

Clean successful coding:

```text
60 base
+10 observed tests passed
+10 clean scope
+10 clear summary
 +5 no user corrections
=95 raw
×1.00
=95 XP
```

Same quality:

```text
partial  95 × .60 = 57 XP
blocked  95 × .30 = 28 XP
failed   95 × .10 = 9 XP
```

`abandoned` always yields zero XP.

Two scope violations + one user correction on successful coding with clear summary/no observed tests:

```text
60 + 10 summary - 10 scope - 5 correction = 55 XP
```

## 8. Skills

Canonical keys:

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

MCP accepts canonical keys only.

Calling Hero Passport itself does not justify `tool_use`.

## 9. Skill XP allocation — `skill-allocation/1.0.0`

Input skills are primary -> secondary -> tertiary.

```text
1 skill  = 100
2 skills = 60 / 40
3 skills = 50 / 30 / 20
```

Use cumulative-floor boundaries so allocation sums exactly to Quest XP.

95 XP / 3 skills:

```text
floor(95*50/100)=47 -> first 47
floor(95*80/100)=76 -> second 29
remainder            -> third 19
47+29+19=95
```

Invariant: `sum(skillXpDelta) == questXp`.

## 10. Hero level thresholds — `hero-progression/2.0.0`

`totalXp` is authoritative; Level is derived.

| Lv | XP | Lv | XP | Lv | XP | Lv | XP | Lv | XP |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
|1|0|11|3250|21|10000|31|17500|41|25000|
|2|100|12|3850|22|10750|32|18250|42|25750|
|3|250|13|4500|23|11500|33|19000|43|26500|
|4|450|14|5200|24|12250|34|19750|44|27250|
|5|700|15|5950|25|13000|35|20500|45|28000|
|6|1000|16|6700|26|13750|36|21250|46|28750|
|7|1350|17|7450|27|14500|37|22000|47|29500|
|8|1750|18|8200|28|15250|38|22750|48|30250|
|9|2200|19|8950|29|16000|39|23500|49|31000|
|10|2700|20|9700|30|16750|40|24250|50|31750|

Level 50 is the 0.1 display cap. XP continues accumulating up to the JSON-safe ceiling.

At cap, wire/presentation uses `isLevelCapped=true` and has no next-level requirement. The engine itself does not invent a threshold above the table.

## 11. Skill level thresholds — `skill-progression/2.0.0`

| Skill Lv | XP threshold |
|---:|---:|
|1|0|
|2|50|
|3|125|
|4|225|
|5|350|
|6|500|
|7|675|
|8|875|
|9|1100|
|10|1350|

Skill level 10 is the 0.1 display cap; Skill XP continues accumulating.

## 12. Rank milestones — `rank/1.0.0`

Rank is derived from Hero Level and is cosmetic only.

| Hero level | Rank key | Default EN label |
|---:|---|---|
|1–4|`code_squire`|Code Squire|
|5–9|`code_knight`|Code Knight|
|10–19|`senior_warrior`|Senior Warrior|
|20–34|`staff_paladin`|Staff Paladin|
|35–49|`principal_warlord`|Principal Warlord|
|50|`legendary_architect`|Legendary Architect|

Localized labels/flavor do not change rank rule version.

## 13. Trust/Strain initial state

New Hero:

```text
Trust  = 50
Strain = 20
```

Both clamp once after all Quest components to `0..100`.

These are RPG stats derived from bounded signals, not objective productivity/reliability telemetry.

## 14. Trust/Strain — `trust-strain/1.0.0`

`abandoned` is neutral:

```text
Trust 0
Strain 0
```

Outcome components:

```text
success  -> Trust +1, Strain -1
partial  -> Trust +0, Strain +1
blocked  -> Trust +0, Strain +0
failed   -> Trust +0, Strain +2
```

Positive quality components:

```text
success && scopeViolations==0 && userCorrections==0
  -> Trust +1, Strain -1

observed tests passed
  -> Trust +1
```

Positive Trust components cap at +2/Quest before negative components. Strain recovery components cap at -2/Quest.

Negative components:

```text
scope violation -> Trust -1, Strain +1 each, count capped at 3
user correction -> Trust -1, Strain +1 each, count capped at 3
```

Examples:

```text
clean success + observed tests -> Trust +2, Strain -2
partial, clean                 -> Trust  0, Strain +1
failed + 1 correction          -> Trust -1, Strain +3
blocked with no issue          -> Trust  0, Strain  0
abandoned                      -> Trust  0, Strain  0
```

Trust/Strain never multiply XP or lock features in 0.1.

## 15. Success Streak — `streak/1.0.0`

```text
success -> previous streak + 1
partial | blocked | failed | abandoned -> 0
```

Streak grants no XP multiplier and losing it creates no Trust/Strain penalty.

## 16. Traits/Titles — `unlock/2.0.0`

Traits/Titles are monotonic cosmetic unlocks.

Initial Traits:

| Trait key | Unlock condition |
|---|---|
|`precise_executor`|5 lifetime successful Quests with zero scope violations and zero user corrections|
|`test_scout`|5 successful coding/debugging Quests with observed tests passed|
|`scope_keeper`|10 lifetime successful Quests with zero scope violations|
|`steady_hand`|Success Streak reaches 5|
|`polyglot_crafter`|5 distinct canonical Skills reach Skill Level 3|

Initial Titles:

| Title key | Unlock condition |
|---|---|
|`rising_adventurer`|Hero Level 5|
|`veteran_of_the_merge`|Hero Level 10|
|`skill_specialist`|any Skill reaches Level 5|
|`unbroken_builder`|Success Streak reaches 10|
|`master_of_many_tools`|5 distinct canonical Skills reach Level 5|

Active Title is the unlocked title with highest `(priority, unlockedAtUtc, titleKey)` under fixed catalog priority.

Initial priority high -> low:

```text
master_of_many_tools
unbroken_builder
skill_specialist
veteran_of_the_merge
rising_adventurer
```

Manual equipment is deferred.

## 17. Milestone events and flavor boundary

The engine emits semantic milestone events/keys for level/rank/trait/title/streak changes.

It does **not** choose a flavor line and does not use a hash/mod selector.

Curated/localized flavor belongs to presentation. Wording may evolve between releases without changing historical game facts or rule versions.

## 18. Result model

Conceptual semantic engine output:

```text
RewardBreakdown
SkillProgressDelta[]
HeroLevelDelta
SkillLevelDelta[]
RankDelta?
TrustStrainDelta
StreakDelta
TraitsUnlocked[]
TitlesUnlocked[]
activeTitle
MilestoneEvent[]   # semantic keys only
ruleVersions
```

No localized text/flavor is authoritative engine data.

## 19. Historical immutability

Once a Quest commits:

```text
reward outcome immutable
rule versions immutable
progression deltas immutable
finalization fingerprint immutable
retry returns stored result
```

New balance versions affect only new Quests unless a separately designed non-authoritative projection is added.

## 20. Required engine tests

```text
same canonical input + versions -> same semantic/numeric output
all XP goldens
questXp >=0
Skill allocation exact conservation
Hero/Skill threshold edges
cap handling has no invented next threshold
Rank boundaries
Trust/Strain clamp and positive caps
abandoned neutral
streak transitions
unlock exact threshold + monotonicity
active Title priority
semantic milestone keys only
historical retry never invokes current rules
unknown keys rejected
checked JSON-safe overflow
```
