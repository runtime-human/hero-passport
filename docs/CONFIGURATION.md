# Hero Passport — Configuration and Binding

**Status:** Accepted v3  
**Snapshot:** 2026-08-11

## 1. Configuration philosophy

Hero Passport owns only Hero Passport configuration. MCP hosts own their own server-registration files.

```text
Hero Passport config != Codex config
Hero Passport config != VS Code mcp.json
Hero Passport config != Cursor/Claude/JetBrains/Zed config
```

The product does not mutate third-party configuration by default.

---

## 2. Config v1

Keep local configuration intentionally small:

```json
{
  "configVersion": 1,
  "locale": "ru",
  "presentation": "compact",
  "diagnostics": {
    "fileLogging": false
  }
}
```

Unknown fields are rejected.

Config does not contain:

```text
API keys/model provider keys
Codex tokens
workspace path
MCP host definitions
plugins
generic metadata
active quest IDs
raw environment dumps
```

Active/default hero is product state, not presentation config.

---

## 3. Precedence

For settings that support all levels:

```text
explicit CLI/startup option
> HERO_PASSPORT_* environment override
> config.json
> built-in default
```

Do not create environment overrides for every internal constant. Environment variables exist only for concrete deployment/test needs.

---

## 4. App data roots

### Windows

Use non-roaming local application data:

```text
%LOCALAPPDATA%\HeroPassport\
  data\hero-passport.db
  config\config.json
  state\logs\...
```

Do not use roaming `%APPDATA%` for the SQLite database.

### macOS

```text
~/Library/Application Support/HeroPassport/
  data/hero-passport.db
  config/config.json
  state/logs/
```

### Linux

Follow XDG:

```text
$XDG_DATA_HOME/hero-passport/hero-passport.db
$XDG_CONFIG_HOME/hero-passport/config.json
$XDG_STATE_HOME/hero-passport/logs/
```

Fallbacks:

```text
~/.local/share/hero-passport
~/.config/hero-passport
~/.local/state/hero-passport
```

---

## 5. Test/development root

`HERO_PASSPORT_HOME` overrides all roots for isolated dev/test runs.

Example layout:

```text
$HERO_PASSPORT_HOME/
  data/
  config/
  state/
```

Rules:

- integration/e2e tests always use a unique temp root;
- no test reads/writes the user's real Hero Passport database;
- cleanup is best-effort after process teardown;
- test path is never embedded in product snapshots/tool output.

---

## 6. Project binding is startup context

Project binding is explicitly separate from `config.json` and from model-facing tool arguments.

Supported stdio launch:

```text
hero-passport mcp [--project-root <path>] [--hero <selector>]
```

Resolution:

```text
--project-root provided
  -> normalize/validate local path
  -> discover Git root when applicable
  -> resolve Project identity

not provided
  -> use process cwd as starting path
  -> discover Git root when applicable
  -> fallback to cwd identity
```

`--project-root` is not stored as the project identity. Persist a versioned fingerprint/display name.

---

## 7. Why `--project-root` is required as a capability

Host configuration is not standardized:

```text
Codex       has mcp_servers.<id>.cwd
VS Code     has stdio cwd and ${workspaceFolder}
JetBrains   has Working directory/project level
Zed         custom local config exposes command/args/env
other hosts differ
```

A command argument is therefore the portable fallback when a host does not expose an explicit cwd field.

Host documentation should prefer the host-native project-scoped mechanism where it exists; otherwise pass `--project-root`.

---

## 8. No MCP Roots dependency

Do not design project identity around MCP Roots. Roots are deprecated in the 2026 protocol line and not consistently supported by hosts.

The supported local profile is a server process bound to one project identity at launch.

A single globally shared stdio process cannot reliably change project on each stateless call without a host-provided binding channel. Hero Passport will not guess project from goal text, client name or current editor file.

---

## 9. Hero binding

Default behavior resolves active/default hero from local product state.

Optional startup binding:

```text
--hero <name-or-id>
```

Use cases:

```text
one user runs different heroes for different agent profiles
one hero is shared across several MCP clients
project-specific server config pins a hero deliberately
```

Host/client name never implicitly selects a hero.

Unknown/ambiguous selector returns configuration/binding error before processing tool calls.

---

## 10. Invocation metadata

MCP client info from protocol metadata may be normalized into in-memory `InvocationOrigin` for diagnostics.

Default policy:

```text
do not persist raw client name/version
never use it for hero/project selection
never use it for reward/auth
redact/bound it in local diagnostic logs
```

---

## 11. `doctor`

`hero-passport doctor` is the primary support boundary.

Checks:

```text
Hero Passport version
.NET runtime/OS/architecture
data/config/state roots
configVersion and unknown fields
selected/default hero binding
database readability/writability
native sqlite_version()
journal_mode
synchronous
foreign_keys
applied/latest EF migrations
suspicious migration-lock state
seed/canonical keys
project binding when --project-root/cwd is available
MCP manifest exact four tools
protocol-version policy not accidentally pinned
```

Diagnostics default to path-safe summaries. An explicit verbose/local diagnostic mode may show local paths to the user in terminal, but they are never copied into MCP output/log telemetry automatically.

---

## 12. Host integration descriptors

Future convenience can model a host-neutral descriptor:

```text
server name
command = hero-passport
args = [mcp, optional binding args]
transport = stdio
```

From that, a CLI command may **print** host-specific snippets:

```text
hero-passport integration show codex
hero-passport integration show vscode
...
```

This is deferred to integration polish and is not a new runtime adapter.

Default behavior must not edit:

```text
~/.codex/config.toml
.vscode/mcp.json
.cursor/mcp.json
Claude configs
JetBrains settings
Zed settings
```

---

## 13. Future Streamable HTTP configuration

Own HTTP hosting is not part of config v1/0.1.0.

When introduced, do not overload local `config.json` with a generic web-server platform. A deployment profile defines:

```text
listen address/port
project binding mode
auth mode
Origin/Host security policy
TLS/reverse proxy assumptions
```

Loopback project-scoped HTTP and public multi-tenant HTTP are separate profiles, not a boolean `http=true`.

---

## 14. Secrets

0.1 local stdio Hero Passport needs no product API secret.

If a host needs credentials for its own remote integration, those remain host/tunnel configuration, not Hero Passport game config.

Future remote HTTP authentication secrets/tokens are handled through ASP.NET/MCP authorization mechanisms and secure credential stores/environment indirection, not persisted in `config.json` by default.

---

## 15. Config evolution

`configVersion` changes only when local config shape/semantics require migration.

Rules:

```text
unknown future config version -> fail clearly
unknown property in current version -> reject
migration must be deterministic
never silently discard an unknown security-relevant setting
```

Config version is independent of HP-MCP, EF schema and product version.
