# Hero Passport documentation

**Baseline date:** 2026-08-10  
**Status:** architecture/specification approved for implementation planning; product code not yet started.

This directory is the source of truth for Hero Passport. The documents deliberately separate product requirements, architecture, protocol contracts and implementation sequencing so agents only need to load the relevant slice of context.

## Reading order

### For product/design work

1. [`PRODUCT-SPEC.md`](PRODUCT-SPEC.md)
2. [`ENGINE-SPEC.md`](ENGINE-SPEC.md)
3. [`ROADMAP.md`](ROADMAP.md)

### For implementation work

1. [`ARCHITECTURE.md`](ARCHITECTURE.md)
2. the feature-specific specification (`MCP-CONTRACT.md`, `DATA-MODEL.md`, etc.)
3. [`TESTING-QUALITY.md`](TESTING-QUALITY.md)
4. [`DECISION-LOG.md`](DECISION-LOG.md)
5. [`superpowers/plans/2026-08-10-hero-passport-implementation.md`](superpowers/plans/2026-08-10-hero-passport-implementation.md)

### For Codex integration

1. [`integrations/CODEX.md`](integrations/CODEX.md)
2. [`MCP-CONTRACT.md`](MCP-CONTRACT.md)
3. [`SECURITY-PRIVACY.md`](SECURITY-PRIVACY.md)

## Canonical ownership

| Concern | Canonical document |
|---|---|
| Product positioning, user loop, scope | `PRODUCT-SPEC.md` |
| Project/module boundaries, flows, runtime topology | `ARCHITECTURE.md` |
| MCP tools and schemas | `MCP-CONTRACT.md` |
| XP/levels/skills/traits/trust-risk | `ENGINE-SPEC.md` |
| Tables, constraints, migrations, transactions | `DATA-MODEL.md` |
| Threat model, privacy, local data policy | `SECURITY-PRIVACY.md` |
| Test strategy, CI/release gates | `TESTING-QUALITY.md` |
| Codex setup and instructions | `integrations/CODEX.md` |
| Delivery sequence | `ROADMAP.md` |
| Architecture decisions | `DECISION-LOG.md` |
| External authoritative sources | `REFERENCES.md` |

If two documents disagree, resolve the contradiction in the canonical owner and update the dependent document in the same change.

## Document status vocabulary

- **Accepted** — implementation may rely on it.
- **Proposed** — requires review before implementation.
- **Deferred** — intentionally post-MVP.
- **Rejected** — evaluated and intentionally not selected.

## Change policy

Changes to any of these require an ADR/decision-log entry and compatibility review:

- MCP tool names or required fields;
- persisted schema semantics;
- XP/trust/risk calculation order or constants;
- privacy/data-retention boundary;
- module dependency direction;
- supported runtime/dependency baseline;
- MVP scope exclusions.

## Source policy

For framework/protocol/tooling facts, prefer the latest official documentation available on the baseline date. Third-party material may inform product research but must not override official protocol/runtime documentation.

The external source snapshot used for this architecture is catalogued in [`REFERENCES.md`](REFERENCES.md).
