# Hero Passport Documentation

**Normative snapshot:** 11 August 2026  
**Architecture:** v3.1 — portable local MCP core after project-identity, persistence-reliability and wire-contract deep dives

This directory is the source of truth for product and implementation decisions.

## Normative precedence

When documents disagree, use this precedence and fix the disagreement in the same change:

```text
1. PRODUCT-SPEC.md
   product scope and user-visible guarantees

2. ARCHITECTURE.md
   system/layer/runtime boundaries

3. Deep-dive contracts
   PROJECT-IDENTITY.md
   PERSISTENCE-RELIABILITY.md
   WIRE-CONTRACT.md

4. API-CONTRACTS.md / MCP-CONTRACT.md
   semantic API and compact MCP overview

5. ENGINE-SPEC.md / DATA-MODEL.md / CONFIGURATION.md / SECURITY-PRIVACY.md
   game and infrastructure details

6. DECISION-LOG.md
   rationale and superseded decisions

7. ROADMAP.md / superpowers implementation plan
   ordering only; never permission to contradict a normative spec
```

The three deep-dive files intentionally have higher precedence than older compact clauses because they were produced specifically to resolve ambiguities found in architecture v3.

## Core design

- [`PRODUCT-SPEC.md`](PRODUCT-SPEC.md)
- [`ARCHITECTURE.md`](ARCHITECTURE.md)
- [`PROJECT-IDENTITY.md`](PROJECT-IDENTITY.md)
- [`PERSISTENCE-RELIABILITY.md`](PERSISTENCE-RELIABILITY.md)
- [`WIRE-CONTRACT.md`](WIRE-CONTRACT.md)
- [`API-CONTRACTS.md`](API-CONTRACTS.md)
- [`MCP-CONTRACT.md`](MCP-CONTRACT.md)
- [`INTEROPERABILITY.md`](INTEROPERABILITY.md)
- [`ENGINE-SPEC.md`](ENGINE-SPEC.md)
- [`DATA-MODEL.md`](DATA-MODEL.md)
- [`CONFIGURATION.md`](CONFIGURATION.md)
- [`SECURITY-PRIVACY.md`](SECURITY-PRIVACY.md)
- [`TESTING-QUALITY.md`](TESTING-QUALITY.md)

## Deployment and distribution

- [`DEPLOYMENT-MODES.md`](DEPLOYMENT-MODES.md)
- [`DISTRIBUTION.md`](DISTRIBUTION.md)
- [`DEPENDENCIES.md`](DEPENDENCIES.md)

## Research and decisions

- [`ECOSYSTEM-BENCHMARK.md`](ECOSYSTEM-BENCHMARK.md)
- [`DECISION-LOG.md`](DECISION-LOG.md)
- [`REFERENCES.md`](REFERENCES.md)
- [`ROADMAP.md`](ROADMAP.md)

## Host integrations

Start with [`integrations/README.md`](integrations/README.md). Host pages describe launch/configuration and qualification status; they never define alternate Hero Passport product semantics.

```text
integrations/
  README.md
  CODEX.md
  CHATGPT.md
  VSCODE.md
  JETBRAINS.md
  ZED.md
  CURSOR.md
  CLAUDE-CODE.md
```

## Agentic implementation artifacts

- [`superpowers/specs/2026-08-10-hero-passport-design.md`](superpowers/specs/2026-08-10-hero-passport-design.md)
- [`superpowers/plans/2026-08-10-hero-passport-implementation.md`](superpowers/plans/2026-08-10-hero-passport-implementation.md)

The implementation plan is synchronized to v3.1. If a task conflicts with a normative contract above, the task is wrong.

## Key v3.1 corrections

```text
Project identity
  Git worktree identity uses git-common-dir, not per-worktree git-dir
  monorepo is one project by default; explicit --project-root creates a scope
  submodule is a separate project by default
  Git safety failures never silently fall back to standalone identity

Quest retry identity
  LogicalQuestKeyV1 retired before release
  QuestDedupKeyV1 hashes exact SafeTextV1 declaration with CASE PRESERVED
  this is retry/dedup identity, not semantic task understanding

SQLite
  all read-modify-write operations begin non-deferred Serializable writer transaction
  selected Microsoft.Data.Sqlite 10.0.10 behavior = BEGIN IMMEDIATE
  count=15 concurrent start race must finish at exactly 16
  runtime SQLite must qualify >=3.51.3 for supported WAL path
  never File.Copy a live DB; online backup uses SQLite BackupDatabase

HP-MCP/2 wire
  start_quest idempotentHint=false; it is only open-request retry-safe
  success structuredContent + equivalent minified JSON TextContent
  tool errors: isError=true + safe TextContent, no structuredContent
  C# SDK generated schemas do not replace explicit runtime validation
  SafeTextV1 uses Unicode-scalar-aware bounds
  canonical UUIDv7 and fixed millisecond UTC timestamps
```

## Version axes

```text
MCP revision          negotiated protocol revision
HP-MCP/2              Hero Passport four-tool contract epoch
product version       e.g. 0.1.0
configVersion         local config schema
EF migration          database schema version
reward rule           deterministic RPG calculation version
QuestDedupKey V1      open-start retry/dedup algorithm
project-identity/1    local project fingerprint algorithm
SafeTextV1            model-text validation/normalization policy
```

Do not collapse these into a generic per-call `schemaVersion`.
