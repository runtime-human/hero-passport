# Documentation map

Hero Passport documentation is normative. When implementation changes architecture/contracts/rules/storage/privacy, update the relevant document in the same PR.

## Read in this order

1. [`PRODUCT-SPEC.md`](PRODUCT-SPEC.md) — what 0.1.0 is and is not.
2. [`ARCHITECTURE.md`](ARCHITECTURE.md) — module boundaries, runtime/persistence/presentation decisions.
3. [`MCP-CONTRACT.md`](MCP-CONTRACT.md) — exact four-tool model and schemas/annotations/token policy.
4. [`ENGINE-SPEC.md`](ENGINE-SPEC.md) — deterministic RPG rules and goldens.
5. [`DATA-MODEL.md`](DATA-MODEL.md) — SQLite schema, migrations, transactions/idempotency.
6. [`CONFIGURATION.md`](CONFIGURATION.md) — config v1, paths, platform behavior and doctor.
7. [`SECURITY-PRIVACY.md`](SECURITY-PRIVACY.md) — threat model and forbidden data.
8. [`TESTING-QUALITY.md`](TESTING-QUALITY.md) — deterministic tests, protocol tests, Codex E2E and agent evals.
9. [`integrations/CODEX.md`](integrations/CODEX.md) — current official Codex integration contract.
10. [`DEPENDENCIES.md`](DEPENDENCIES.md) — accepted/rejected libraries and why.
11. [`ECOSYSTEM-BENCHMARK.md`](ECOSYSTEM-BENCHMARK.md) — multi-pass research of mature MCP projects/apps.
12. [`ROADMAP.md`](ROADMAP.md) — release/milestone order.
13. [`DECISION-LOG.md`](DECISION-LOG.md) — decisions not to rediscover while coding.
14. [`REFERENCES.md`](REFERENCES.md) — dated primary source baseline.

## Consolidated execution docs

- [`superpowers/specs/2026-08-10-hero-passport-design.md`](superpowers/specs/2026-08-10-hero-passport-design.md) — compact consolidated design.
- [`superpowers/plans/2026-08-10-hero-passport-implementation.md`](superpowers/plans/2026-08-10-hero-passport-implementation.md) — task-by-task implementation plan.

Detailed specifications above win over a summary/plan if wording appears inconsistent. Fix the summary/plan immediately rather than coding against ambiguity.

## Source precedence

```text
current official specification/docs
> current official SDK/package docs/source
> current production open-source repository behavior
> reference/example repos
> older Hero Passport reports/docs
```

See `REFERENCES.md` for the actual dated sources.

## Important v2 corrections

Architecture v2 supersedes several first-draft assumptions:

```text
explicit 4-tool MCP registration, no assembly scan
no per-call locale/outputMode/schemaVersion/heroId/projectId/workspacePath
server instructions instead of per-response agentHint
Domain/Application return typed data; App renders displayText
IDbContextFactory + short synchronous SQLite DB segments
Windows LocalApplicationData rather than roaming APPDATA
WAL + synchronous FULL
EF built-in migration lock rather than a custom mutex
agent evaluations in addition to unit/integration tests
```

If you encounter the superseded form in any normative file, treat it as documentation debt and fix it before implementation proceeds.
