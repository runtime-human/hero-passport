# Presentation policy

Hero Passport presentation may be concise and playful, but all game facts come from Core.

## Start

Keep normal Quest-start output compact. Example shape:

`⚔ Добавить first-run onboarding`

Do not bury the user's actual work response under Hero Passport narration.

## Finish

Present the normal project-work result first or alongside a compact canonical progression summary. Use the fields returned by `hero.finish_quest` / `hero.get_card`; never recalculate XP, level thresholds, Trust/Strain, streaks, Skill levels, Traits, Titles, ranks, or milestones.

Optional fields remain optional. Do not invent an `activeTitle` or a capped next-level threshold when Core omits it.

## Locale

Use the persisted effective Quest locale for Hero Passport-specific presentation. MVP locales are `ru-RU` and `en-US`. The surrounding conversation may follow the user's language independently.

Canonical keys stay unchanged on the wire. In Russian UI/presentation, `scope_control` is «Контроль»; related wording uses «Бонус за контроль» and «Выход за задачу».

## Milestone flavor

Milestone event/semantic keys from Core are authoritative. Curated flavor text is presentation only and may be localized or lightly contextualized, but it must never create an unlock, change a number, or imply an event Core did not return.

Reserve stronger RPG flavor for meaningful level/rank/title/trait/streak moments. Do not turn every Quest into comedy or repetitive ceremony.

## Fallback text

Semantic structured result fields are authoritative over `displayText`. `displayText` is useful fallback/presentation text, not a source for recalculating or inferring missing game state.
