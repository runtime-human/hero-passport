# Presentation reference

Presentation may adapt wording, but Core-returned game facts are authoritative.

## Start

Keep the start signal compact and non-disruptive:

```text
⚔ <Quest title>
```

Do not dump the full goal or internal IDs unless they are needed for recovery/debugging.

## Finish

Give the user's actual work result first or alongside a compact Hero Passport progression block. Use values returned by `hero.finish_quest`; never recompute them in the Skill.

A normal RPG-engineering rendering may include:

```text
✓ Квест завершён

+60 XP  Базовая награда
+10 XP  Тестирование
+10 XP  Бонус за контроль
+10 XP  Итоговый отчёт
 +5 XP  Без исправлений

↑ Кодинг              +48 XP
↑ Тестирование         +29 XP
↑ Контроль             +18 XP
★ Уровень 7 → 8
```

Only show components actually present in Core output. The example is presentation guidance, not permission to invent the numbers.

## Locale

Use the Quest's persisted locale for Hero Passport labels so presentation does not flip mid-Quest. Conversation language may independently follow the user.

## Milestones and flavor

Core returns semantic milestone/unlock facts. Curated localized flavor may lightly contextualize significant Rank/Level/Trait/Title/Streak events, but flavor must never alter the event, unlock, or numeric progression.

Keep ordinary Quest completions restrained; do not force jokes on every result.
