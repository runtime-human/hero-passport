# Hero Passport — Distribution

**Status:** Accepted v3 distribution strategy  
**Snapshot:** 2026-08-11

## 1. Principle

Distribution should make the same Hero Passport server easy to launch from different MCP hosts. Do not create per-host binaries or forks.

```text
one product binary/package
one HP-MCP contract
many configuration snippets
```

---

## 2. Delivery order

### Development

```text
dotnet run --project src/HeroPassport.App -- mcp ...
```

### Primary 0.1 package

.NET global/local tool providing:

```text
hero-passport
```

Why first:

- natural C#/.NET packaging;
- cross-platform command;
- straightforward NuGet release;
- works with stdio host configs;
- compatible with official MCP Registry NuGet metadata if/when used.

### Later

```text
framework-dependent archives if useful
self-contained per-RID binaries
single-file only after SQLite/native packaging tests
```

Do not make NativeAOT/single-file a 0.1 release blocker.

---

## 3. Package identity

Product/tool package naming must be stable before publication. Package metadata must clearly identify:

```text
project repository
license
version
supported framework/runtime
command name
MCP server purpose
privacy summary
```

Do not publish experimental package IDs and later make host docs depend on them if a final identity has not been selected.

---

## 4. Host installation model

Host pages explain how to point to the same command:

```text
command: hero-passport
args: mcp [binding args]
```

Host-native package/gallery mechanisms are optional convenience layers. They must not become required for the core server.

---

## 5. Integration snippet renderer

Post-0.1 convenience may add:

```text
hero-passport integration show codex
hero-passport integration show vscode
hero-passport integration show jetbrains
hero-passport integration show zed
hero-passport integration show cursor
hero-passport integration show claude-code
```

It prints a current example and official documentation pointer.

Do not automatically mutate third-party config by default. If an installer is ever added, it requires host-specific write/backup/rollback testing and explicit user action.

---

## 6. MCP Registry

The official MCP Registry is in preview as of this snapshot. It supports NuGet packages with `registryType: "nuget"` and standard `server.json` metadata.

Hero Passport policy:

```text
0.1 runtime has no Registry dependency
Registry publication is distribution metadata only
reevaluate Registry stability at release/publication time
```

When publication is chosen:

1. pick an immutable server registry name;
2. make NuGet package identity stable;
3. include required `mcp-name: <SERVER_NAME>` ownership marker in package README according to Registry rules;
4. generate/validate `server.json` from release metadata;
5. verify stdio package invocation on supported OSes;
6. do not put local project paths or secrets in Registry metadata.

Do not reserve/document a final registry name prematurely if it has not been published.

---

## 7. Host galleries/extensions

Use only when they reduce installation friction without creating a separate runtime implementation.

Examples:

- VS Code MCP/server discovery mechanisms;
- Zed ecosystem/Registry direction;
- JetBrains import/configuration UI;
- OpenAI plugins for public remote deployments in the future.

The standard package remains the source runtime.

---

## 8. OpenAI private/public distinction

### Private

OpenAI Secure MCP Tunnel can forward to a private local Hero Passport stdio process. This is deployment documentation, not a Hero Passport package variant.

### Public

Public ChatGPT plugin/server distribution requires a stable publicly reachable HTTPS MCP endpoint and therefore belongs to the future hosted/HTTP architecture, not the local dotnet tool release.

Do not market the local package as a public ChatGPT plugin.

---

## 9. Release artifacts

0.1 release should produce/validate at minimum:

```text
NuGet tool package
checksums/build provenance as available in CI
release notes
contract snapshot diff/status
migration compatibility status
host qualification matrix
SBOM/package dependency output if repository tooling supports it cheaply
```

Self-contained artifacts later should include per-RID checksum and actual native SQLite version verification.

---

## 10. Versioning

The package version is the Hero Passport product version.

Do not encode MCP revision or HP-MCP epoch in package name.

Release notes separately state:

```text
Product: 0.1.0
HP-MCP: 2
preferred MCP semantics: 2026-07-28
SDK compatibility policy: negotiated supported revisions
DB migration head
rule versions
```

---

## 11. Upgrade contract

A tool update must not silently destroy local game state.

Release qualification includes:

```text
upgrade previous released package
run EF migrations
open old DB
read existing history
finish/retry historical quest if fixture covers it
verify rule version interpretation
```

If package downgrade is unsupported, state it explicitly rather than attempting reverse migrations automatically.

---

## 12. Distribution review triggers

Revisit this strategy when:

```text
MCP Registry reaches a stable maturity relevant to publication
non-.NET users materially struggle with tool installation
self-contained binaries become a major adoption requirement
public ChatGPT/plugin distribution is approved
an IDE requires a signed extension/container format rather than an executable command
```

Until then, keep distribution boring and standard.
