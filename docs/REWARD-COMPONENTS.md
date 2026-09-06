# Hero Passport — Reward Component History Contract

**Status:** Accepted v3.2.1 focused contract  
**Rule version:** `reward/2.0.0`  
**Snapshot:** 2026-09-06

This document is normative for persisted `quest_reward_components.component_key`, row ordering and signed `xp_delta` semantics. `ENGINE-SPEC.md` remains normative for reward formulas. `DATA-MODEL.md` remains normative for table shape and persistence ownership.

## 1. Why these keys are a contract

Reward component rows are canonical completed-Quest history used for replay, diagnostics and future projection rebuild. Their keys are therefore stable semantic identifiers, not display text, localization keys or implementation-private names.

Once a Quest is committed, its component keys, deltas and rule version are immutable.

## 2. `reward/2.0.0` component catalog

The only persisted component keys for `reward/2.0.0` are, in canonical evaluation order:

| Order | `component_key` | Active when | `xp_delta` |
|---:|---|---|---:|
| 1 | `observed_tests_passed_bonus` | tests passed with `observed` evidence | `+10` |
| 2 | `clean_scope_bonus` | `scopeViolations == 0` | `+10` |
| 3 | `clear_summary_bonus` | normalized summary scalar length >= 40 | `+10` |
| 4 | `no_user_corrections_bonus` | `userCorrections == 0` | `+5` |
| 5 | `scope_violation_penalty` | `scopeViolations > 0` | `-5 * min(scopeViolations, 3)` |
| 6 | `user_correction_penalty` | `userCorrections > 0` | `-5 * min(userCorrections, 3)` |

No other component key is valid under `reward/2.0.0`.

## 3. Row materialization

Inactive components are omitted; zero-delta rows are not stored.

Rows retain the canonical catalog order after inactive entries are filtered. Persisted `ordinal` values are dense and zero-based:

```text
0 .. component_count - 1
```

Example, clean successful coding with observed tests and a clear summary:

```text
0 observed_tests_passed_bonus +10
1 clean_scope_bonus           +10
2 clear_summary_bonus         +10
3 no_user_corrections_bonus    +5
```

Example, two scope violations and one user correction with a clear summary and no observed tests:

```text
0 clear_summary_bonus          +10
1 scope_violation_penalty      -10
2 user_correction_penalty       -5
```

Penalty rows aggregate the capped category delta. Do not emit one row per violation/correction.

## 4. Values intentionally not represented as component rows

These remain authoritative fields on `quest_reports` and are not duplicated in `quest_reward_components`:

```text
base_xp
outcome_permille
bonus_xp
penalty_xp
raw_xp
xp_gained
reward_rule_version
```

In particular, base XP and the outcome multiplier are not component keys.

## 5. Consistency invariants

For every completed report using `reward/2.0.0`:

```text
bonus_xp   = sum(max(component.xp_delta, 0))
penalty_xp = -sum(min(component.xp_delta, 0))
raw_xp     = max(0, base_xp + bonus_xp - penalty_xp)
xp_gained  = floor(raw_xp * outcome_permille / 1000)
```

`component_key` values must be unique within one report because each catalog component can activate at most once.

Persistence and replay must not re-evaluate historical components using current rules. They read the stored report, stored component rows and stored rule version.

## 6. Versioning and presentation

A future reward rule version may introduce, remove or reinterpret component keys only under a new `reward_rule_version`. Existing `reward/2.0.0` rows remain valid forever while their history exists.

Localized labels such as Russian “Бонус за контроль” are presentation only. They never replace or alter the canonical key `clean_scope_bonus`.
