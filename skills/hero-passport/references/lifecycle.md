# Lifecycle reference

## Onboarding

When `hero.get_context` returns `setupCompleted=false`:

1. obtain locale (`ru-RU` or `en-US`), initial Hero name, presentation style, auto-start and auto-finish preferences;
2. generate one fresh UUIDv7 `bootstrapRequestId`;
3. call `hero.bootstrap` exactly once for that intent;
4. if the response is ambiguous, retry the same request ID and identical arguments;
5. never use `hero.configure` to create the initial Hero.

After setup, `hero.configure` changes preferences only.

## What counts as a Quest

Likely meaningful Quest work:

- implementation;
- debugging;
- review that produces a project result;
- architecture/planning tied to a project decision;
- project research needed for a decision;
- documentation;
- maintenance;
- testing as meaningful project work.

Do not auto-start for casual conversation, a short factual explanation, or clarification that does not yet become project work.

## Same-goal continuation

Keep related follow-up work in the same Quest when it is necessary to complete the same coherent outcome: tests for the same fix, adjacent documentation needed by the change, or corrective work discovered while implementing it.

Do not fragment a coherent goal into micro-Quests.

## Explicit goal switch

If the user clearly switches to an independent goal before the current Quest is complete:

- if the old goal produced a useful completed subset, finish it as `partial`;
- if it produced no scored useful result, finish it as `abandoned`;
- then start the new coherent goal if permitted.

If the switch is ambiguous, keep the old Quest open and do not silently create another one for the same Hero+Project slot.

## Finish outcomes

- `success`: requested coherent goal accomplished;
- `partial`: useful subset delivered but goal not fully accomplished;
- `blocked`: an external condition prevents meaningful continuation;
- `failed`: attempt ended without a usable requested result;
- `abandoned`: intentionally stopped without scored result.

## Stable error handling

- `HP001`: setup is required; hydrate and bootstrap.
- `HP002`: setup already exists; call `hero.get_context` and continue from persisted state.
- `HP133`: the selected Hero already owns an open Quest in this Project; resolve/resume/finish that Quest instead of bypassing the invariant.
- `HP135`: a mutation request ID was reused with changed canonical scope/arguments; do not turn it into a fake retry with a new ID.
- `HP136`: another distinct finalization is already durable; never overwrite it.
- `HP202`: transient database contention; retry only with the same retry-safe mutation identity and arguments where applicable.

Do not automatically abandon a Quest just to make another start succeed.
