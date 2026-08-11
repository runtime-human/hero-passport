# Hero Passport — Deterministic RPG Engine Specification

**Status:** Accepted v3.2 game contract  
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

It does not read SQLite/config/Git/files, call MCP/LLMs/network, inspect source/diffs/logs, localize strings, or read the system clock.

Historical completed outcomes are immutable and keep the exact rule versions that produced them.

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

## 4. Quality facts

Canonical input:

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

Cross-field validation is performed before the engine. Raw logs/source/diffs are never engine input.

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

Absence of a bonus is not an extra penalty. There is no separate “short summary” subtraction.

## 6. XP formula

```text
baseXp      = base table value
bonusXp     = sum applicable bonuses
penaltyXp   = min(scopeViolations, 3) * 5
            + min(userCorrections, 3) * 5
rawXp       = max(0, baseXp + bonusXp - penaltyXp)
questXp     = floor(rawXp * outcomePermille / 1000)
```

No floating-point arithmetic is required.

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

Same quality, `partial`:

```text
95 × 0.60 = 57 XP
```

Same quality, `blocked`:

```text
95 × 0.30 = 28 XP
```

Same quality, `failed`:

```text
95 × 0.10 = 9 XP
```

`abandoned` always yields zero XP regardless of quality fields.

Two scope violations + one user correction on successful coding with clear summary and no observed tests:

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

MCP accepts canonical keys only. Non-MCP human/import adapters may normalize a small documented alias list before Application.

Calling Hero Passport itself does not justify `tool_use`.

## 9. Skill XP allocation — `skill-allocation/1.0.0`

Input skills are ordered primary -> secondary -> tertiary.

```text
1 skill  = 100
2 skills = 60 / 40
3 skills = 50 / 30 / 20
```

Use cumulative-floor boundaries so integer allocation is conserved exactly.

For 95 XP / three skills:

```text
first boundary  floor(95*50/100)  = 47 -> first 47
second boundary floor(95*80/100)  = 76 -> second 29
final boundary  floor(95*100/100) = 95 -> third 19
47+29+19 = 95
```

Invariant:

```text
sum(skillXpDelta) == questXp
```

## 10. Hero level thresholds — `hero-progression/2.0.0`

`totalXp` is authoritative. Level is derived by the following static threshold table. Threshold means minimum total XP for that level.

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

Level 50 is the 0.1 display cap. XP continues accumulating at the JSON-safe integer ceiling; a later rule version may extend the table without changing historical earned XP.

This table is game content, not recomputed from a hidden adaptive formula.

## 11. Skill level thresholds — `skill-progression/2.0.0`

Each Skill has independent cumulative XP and level:

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

Localized labels/flavor do not change the rank rule version.

## 13. Trust and Strain initial state

New Hero:

```text
Trust  = 50
Strain = 20
```

Both clamp once after all per-Quest components to `0..100`.

## 14. Trust/Strain — `trust-strain/1.0.0`

`abandoned` is completely neutral:

```text
Trust delta  = 0
Strain delta = 0
```

For other results, compose these components:

### Outcome components

```text
success  -> Trust +1, Strain -1
partial  -> Trust +0, Strain +1
blocked  -> Trust +0, Strain +0
failed   -> Trust +0, Strain +2
```

### Positive quality components

```text
success && scopeViolations==0 && userCorrections==0
  -> Trust +1, Strain -1

observed tests passed
  -> Trust +1
```

Positive Trust components are capped at `+2` per Quest before negative correction/violation components. Strain recovery components are capped at `-2` per Quest.

### Negative behavior components

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
abandoned with any facts       -> Trust  0, Strain  0
```

Trust/Strain never multiply XP and never lock product functions in 0.1.

## 15. Success Streak — `streak/1.0.0`

```text
success -> previous streak + 1
partial | blocked | failed | abandoned -> 0
```

Streak is cosmetic/milestone input only. It grants no XP multiplier and losing it creates no Trust/Strain penalty.

## 16. Traits and Titles — `unlock/2.0.0`

Traits and Titles are monotonic cosmetic unlocks. An unlocked key never relocks under this rule version.

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

One active Title is derived deterministically: select the unlocked title with highest `(priority, unlockedAtUtc, titleKey)` where priority is fixed by the rule catalog. Initial priority order, highest first:

```text
master_of_many_tools
unbroken_builder
skill_specialist
veteran_of_the_merge
rising_adventurer
```

Manual title equipment is deferred.

## 17. Milestone flavor

Rank/level/trait/title/streak milestone events may carry a curated `flavorKey` selected deterministically from a bounded catalog. Presentation may lightly contextualize the phrase but cannot change the event.

Flavor selection uses a deterministic stable selector such as `(eventId hash mod availableLineCount)`; it never affects progression.

## 18. Result model

Conceptual semantic outputs include:

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
MilestoneEvent[]
ruleVersions
```

No localized text is authoritative engine data.

## 19. Historical immutability

Once a Quest commits:

```text
reward outcome immutable
rule versions immutable
progression delta immutable
retry returns stored result
```

A future balance version affects only new Quests unless an explicit non-authoritative projection feature is designed.

## 20. Required engine tests

At minimum:

```text
same canonical input + rule versions -> same output
all XP goldens above
questXp >= 0
skill allocations conserve exact XP
Hero/Skill threshold edges exact
Rank boundaries exact
Trust/Strain clamp 0..100
positive Trust cap exact
abandoned neutral
streak transitions exact
unlock exact-at-threshold and monotonicity
active title priority deterministic
historical retry never invokes current rules
unknown keys rejected
checked arithmetic prevents JSON-safe integer overflow
```
