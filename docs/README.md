# Hero Passport Documentation

**Normative snapshot:** 11 August 2026  
**Architecture:** v3 — portable local MCP core with transport-neutral application semantics

This directory is the source of truth for product and implementation decisions. When documents disagree, use this precedence and fix the disagreement in the same change:

```text
1. PRODUCT-SPEC.md — product scope and user-visible guarantees
2. ARCHITECTURE.md — system boundaries and runtime model
3. API-CONTRACTS.md — semantic API/error/versioning conventions
4. MCP-CONTRACT.md — HP-MCP wire contract
5. DATA-MODEL.md / CONFIGURATION.md / SECURITY-PRIVACY.md — infrastructure guarantees
6. DECISION-LOG.md — rationale and superseded decisions
7. ROADMAP.md / implementation plan — implementation order, never a license to contradict specs
```

## Core design

- [`PRODUCT-SPEC.md`](PRODUCT-SPEC.md)
- [`ARCHITECTURE.md`](ARCHITECTURE.md)
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

Start with [`integrations/README.md`](integrations/README.md). Host pages describe configuration and qualification status; they do **not** define alternate Hero Passport APIs.

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

These are synchronized with architecture v3. If an implementation task conflicts with a normative contract above, the task is wrong and must be corrected before coding.

## Terminology

```text
MCP revision       = Model Context Protocol wire revision, negotiated by SDK
HP-MCP/2           = Hero Passport's four-tool semantic contract epoch
product version    = Hero Passport release, e.g. 0.1.0
configVersion      = local config schema
EF migration       = database schema evolution
rule version       = deterministic RPG calculation version
project identity   = local fingerprint algorithm version
```

Do not collapse these version axes into one `schemaVersion` field.
