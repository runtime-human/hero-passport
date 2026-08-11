# Hero Passport Documentation

**Current architecture:** v3.2  
**Snapshot:** 2026-08-11

## Start here

```text
PRODUCT-SPEC.md     what the product is and how it behaves
ARCHITECTURE.md     system boundaries and runtime design
AGENT-SKILL.md      ambient agent lifecycle policy
```

For implementation, also read the subsystem’s normative deep dive.

## Normative precedence

When documents overlap, use this order for the relevant topic:

1. `superpowers/specs/2026-08-11-hero-passport-v3.2-design.md` — consolidated accepted v3.2 product semantics;
2. `WIRE-CONTRACT.md` — exact HP-MCP/2 fields, schemas, tool order, annotations, results/errors;
3. `PERSISTENCE-RELIABILITY.md` — SQLite transactions, concurrency, crash recovery, backup;
4. `PROJECT-IDENTITY.md` — Git/filesystem project identity;
5. `ENGINE-SPEC.md` — exact deterministic game rules and versioned thresholds;
6. `AGENT-SKILL.md` — official Skill trigger/lifecycle/report/presentation behavior;
7. subsystem overview docs below;
8. roadmap/integration/reference material.

Older v3/v3.1 material is historical only where explicitly marked superseded. It must never override v3.2.

## Product / architecture

- [`PRODUCT-SPEC.md`](PRODUCT-SPEC.md)
- [`ARCHITECTURE.md`](ARCHITECTURE.md)
- [`API-CONTRACTS.md`](API-CONTRACTS.md)
- [`DATA-MODEL.md`](DATA-MODEL.md)
- [`CONFIGURATION.md`](CONFIGURATION.md)
- [`SECURITY-PRIVACY.md`](SECURITY-PRIVACY.md)

## Agent / protocol

- [`AGENT-SKILL.md`](AGENT-SKILL.md)
- [`MCP-CONTRACT.md`](MCP-CONTRACT.md)
- [`WIRE-CONTRACT.md`](WIRE-CONTRACT.md)
- [`INTEROPERABILITY.md`](INTEROPERABILITY.md)
- [`integrations/README.md`](integrations/README.md)

## Deterministic engine

- [`ENGINE-SPEC.md`](ENGINE-SPEC.md)

Key v3.2 terms:

```text
Risk                    -> retired; use Strain
QuestDedupKeyV1         -> retired; use explicit mutation request IDs
max 16 open Quests      -> retired; one open Quest per Hero+Project
4-tool-only MCP surface -> retired; v3.2 has 11 explicit tools
```

## Persistence / platform

- [`PROJECT-IDENTITY.md`](PROJECT-IDENTITY.md)
- [`PERSISTENCE-RELIABILITY.md`](PERSISTENCE-RELIABILITY.md)
- [`DEPENDENCIES.md`](DEPENDENCIES.md)
- [`DEPLOYMENT-MODES.md`](DEPLOYMENT-MODES.md)
- [`DISTRIBUTION.md`](DISTRIBUTION.md)

## Quality / decisions / research

- [`TESTING-QUALITY.md`](TESTING-QUALITY.md)
- [`DECISION-LOG.md`](DECISION-LOG.md)
- [`ECOSYSTEM-BENCHMARK.md`](ECOSYSTEM-BENCHMARK.md)
- [`REFERENCES.md`](REFERENCES.md)
- [`ROADMAP.md`](ROADMAP.md)

## Accepted design and plan

Current:

- `superpowers/specs/2026-08-11-hero-passport-v3.2-design.md`
- `superpowers/plans/2026-08-11-hero-passport-v3.2-implementation.md`

The 2026-08-10 v3.1 design/plan are retained only as superseded pointers for history.

## Documentation maintenance rule

When an architectural decision changes:

1. update the authoritative deep dive;
2. update consolidated spec/decision log;
3. update overview docs that repeat the contract;
4. update implementation plan/tests;
5. run stale-contract search;
6. never leave contradictory active guidance because “the deep dive is newer”.

Important stale-search terms after v3.2:

```text
QuestDedupKeyV1
16 open quests
Trust/Risk
risk_before / risk_after
start idempotent=false
exactly four tools
ModelContextProtocol 2.0.0
SQLite >=3.51.3
```

Occurrences are acceptable only inside explicit historical/supersession notes.
