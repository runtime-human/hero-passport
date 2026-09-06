# Recovery and retry policy

Use this reference after restart, uncertain local memory, ambiguous transport outcomes, multiple open Quests, or stable HP application errors.

## Rehydrate before guessing

Call `hero.get_context`. Treat its persisted setup/settings, active default Hero, current Project, and `openQuests` across all Heroes as authoritative. Local Skill memory is only an orchestration cache.

Version safety:

- expected Skill contract: `hero-passport-skill/1`;
- expected MCP contract: `HP-MCP/2`.

If `skillContractVersion` or `contractVersion` is incompatible, stop automated lifecycle calls and surface concise upgrade guidance. Do not guess another wire shape.

## Open-Quest recovery

For the current project:

- no plausible persisted Quest -> form a new Quest for the selected/default Hero only if its Hero+Project slot is free;
- exactly one clearly matching persisted Quest -> resume its `questId`, even if another host changed the global active-Hero preference;
- several plausible persisted Quests -> do not guess; present a concise choice;
- a different open Quest for the selected Hero -> do not silently abandon it to make a new Start succeed.

Never identify a Quest solely because its title/goal text looks similar. Use the persisted `questId` returned by Core.

## Retry identities

A mutation request ID identifies one canonical intent, not a general operation.

- ambiguous bootstrap result -> same `bootstrapRequestId` and identical arguments;
- ambiguous Start result -> same `startRequestId`, explicit `heroId`, and identical canonical arguments;
- ambiguous Finish result -> same `finishRequestId`, `questId`, and identical finalization payload.

Do not create a fresh ID as a fake retry after a conflict.

## Stable recovery errors

- `HP001` setup required -> use setup/bootstrap path.
- `HP002` setup already completed -> call `hero.get_context` and continue from persisted state.
- `HP133` active Quest exists for the same Hero+Project -> rehydrate; resume/resolve the existing Quest instead of auto-abandoning it.
- `HP135` idempotency conflict -> caller reused a mutation ID with changed canonical scope/arguments. Stop retrying that mutation as if it were identical and surface the conflict.
- `HP136` Quest already finalized differently -> another finalization is authoritative. Do not overwrite or invent replacement facts; rehydrate and render persisted state.
- `HP202` database busy -> bounded retry is allowed only with the exact same retry-safe request identity and arguments.

For unexpected storage/config/project-binding failures, preserve the stable HP error meaning and avoid exposing raw SQL, stack traces, absolute paths, secrets, or request dumps.
