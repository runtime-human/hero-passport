# Lifecycle policy

Use this reference for Quest activation, onboarding, continuation, explicit goal switches, and finish boundaries. Core state from `hero.get_context` is authoritative whenever local orchestration memory is uncertain.

## Onboarding

When `setupCompleted=false`, conduct only the short setup needed by `hero.bootstrap`:

1. locale (`ru-RU` or `en-US`);
2. initial Hero name;
3. presentation style;
4. `autoStartQuest` preference;
5. `autoFinishQuest` preference and confirmation.

Generate one fresh `bootstrapRequestId`. If transport delivery/result is ambiguous, retry the same request ID with exactly the same arguments. A fresh bootstrap after completed setup is not recovery. `hero.configure` is post-setup preference editing only and never creates the initial Hero.

## Start decision

Prefer one coherent meaningful user goal per Quest. Typical qualifying work: implementation, debugging, review, architecture/planning that produces a project result, project-directed research, testing, documentation, and maintenance.

Do not auto-start for casual chat, short factual explanations, translation/summarization-only requests, or clarification without meaningful project action.

When a Start is appropriate and `autoStartQuest=true`, or the user explicitly requests it:

- normally select `heroId = get_context.activeHero.heroId`;
- pass that `heroId` explicitly to `hero.start_quest`;
- generate one fresh `startRequestId` for this Start intent;
- choose one canonical `questType`;
- write a short title and precise goal;
- retain returned `questId` and persisted `heroId`.

The active Hero pointer is only a default preference. Another host changing it must not retarget an already formed Start intent.

If `autoStartQuest=false`, do not auto-start merely because meaningful work begins. Explicit user intent can still request a Start.

## Continue the same goal

Keep materially related follow-up work in the same Quest: tests for the same change, necessary adjacent documentation, or fixes discovered while completing the same outcome. Do not fragment one goal into micro-Quests.

If waiting for user input or an external decision and the goal is not terminal, keep the Quest open. Uncertainty is not completion.

## Explicit goal switch

If the user clearly switches to an independent goal before the current one is complete:

- useful completed result exists -> finish the old Quest as `partial`;
- no useful result -> finish the old Quest as `abandoned`;
- then form the new Quest if allowed.

If the switch is ambiguous, keep the existing Quest open rather than silently closing it.

## Finish boundary

Finish only when the current goal is genuinely done and the work result is ready to present, or when a truthful terminal state (`blocked`, `failed`, explicit `abandoned`) is reached.

If `autoFinishQuest=false`, do not automatically finalize merely because the work appears complete; explicit user intent may still request finishing.

For a finalization intent:

- generate one fresh `finishRequestId`;
- use the persisted `questId`;
- construct truthful bounded attestations from `references/finish-attestations.md`;
- call `hero.finish_quest` once;
- if delivery/result is ambiguous, retry the identical payload with the same `finishRequestId`.

Never create a new request ID merely to evade an idempotency or finalization conflict.
