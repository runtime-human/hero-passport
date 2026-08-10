# Hero Passport — configuration and filesystem contract

**Status:** Accepted baseline  
**Snapshot date:** 2026-08-10  
**Config schema:** `configVersion = 1`

## 1. Design goals

Configuration must be:

- local-first;
- explicit and small;
- cross-platform;
- deterministic;
- safe for Codex/stdio execution;
- isolated in tests;
- strict about unknown/invalid values;
- independent from MCP wire inputs.

A common failure mode in agent tools is pushing host configuration into every tool call. Hero Passport does the opposite: **stable environment/application configuration lives outside the model-facing tool schema**.

The model should not repeatedly choose:

```text
locale
outputMode
heroId
workspacePath
databasePath
logging mode
schemaVersion
```

Those values belong to local configuration/application state or are resolved locally.

---

## 2. Configuration layers and precedence

For a setting that is configurable, precedence is:

```text
1. explicit CLI option for that invocation
2. documented HERO_PASSPORT_* environment variable
3. config.json
4. compiled default
```

Application state stored in SQLite (for example the active hero) is **not** overridden by arbitrary environment binding unless a specific supported feature defines it.

Do not bind all environment variables into a generic configuration dictionary. Hero Passport reads only documented names.

---

## 3. Filesystem locations

### 3.1 Override for tests/portable development

If set:

```text
HERO_PASSPORT_HOME=/absolute/path
```

all local Hero Passport roots are derived underneath it:

```text
$HERO_PASSPORT_HOME/
  data/
    hero-passport.db
  config/
    config.json
  state/
    logs/
```

Primary uses:

- E2E tests;
- isolated development runs;
- portable/manual diagnostics.

Rules:

- path must resolve to an absolute path after normalization;
- create only required directories;
- never emit this path into MCP responses;
- never persist it in the database;
- tests must set their own unique root and clean it up.

### 3.2 Windows

The database is **non-roaming application data** and therefore must not live under roaming `%APPDATA%`.

Default root:

```text
%LOCALAPPDATA%\HeroPassport\
```

Layout:

```text
%LOCALAPPDATA%\HeroPassport\
  data\hero-passport.db
  config\config.json
  state\logs\
```

Use `Environment.SpecialFolder.LocalApplicationData`; do not hardcode `C:\Users\...`.

Rationale: .NET defines `ApplicationData` as roaming and `LocalApplicationData` as non-roaming. A SQLite database and machine-local MCP state should not be profile-roamed between machines.

### 3.3 macOS

Default support root:

```text
~/Library/Application Support/HeroPassport/
```

Layout:

```text
~/Library/Application Support/HeroPassport/
  data/hero-passport.db
  config/config.json
  state/logs/
```

This follows the macOS Application Support convention for app-managed persistent data.

### 3.4 Linux

Respect the XDG Base Directory Specification.

Data:

```text
$XDG_DATA_HOME/hero-passport/
```

or, if unset:

```text
~/.local/share/hero-passport/
```

Configuration:

```text
$XDG_CONFIG_HOME/hero-passport/config.json
```

or:

```text
~/.config/hero-passport/config.json
```

State/logs:

```text
$XDG_STATE_HOME/hero-passport/
```

or:

```text
~/.local/state/hero-passport/
```

Database:

```text
$XDG_DATA_HOME/hero-passport/hero-passport.db
```

Directory creation on Unix should request restrictive user-only permissions where supported. The XDG specification recommends `0700` for newly created destination directories.

---

## 4. IAppDataPaths contract

Application defines a port; Infrastructure owns platform resolution.

Conceptual shape:

```csharp
public sealed record AppDataPaths(
    string DataDirectory,
    string ConfigurationDirectory,
    string StateDirectory,
    string DatabasePath,
    string ConfigurationPath,
    string LogDirectory);

public interface IAppDataPaths
{
    AppDataPaths Current { get; }
}
```

Requirements:

- all values are normalized absolute local paths;
- path calculation is deterministic for the same environment;
- no directory is created by a pure resolver constructor;
- initialization command/service performs creation and reports failures;
- paths do not cross into Domain;
- full paths do not cross into MCP contracts.

---

## 5. config.json v1

The initial config intentionally contains presentation and local operational policy only.

Example:

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

Schema intent:

```text
configVersion: integer, required, exactly 1
locale: "ru" | "en" | "auto", default "auto"
presentation: "compact" | "normal", default "compact"
diagnostics.fileLogging: boolean, default false
additional properties: rejected
```

`verbose` is deliberately not a persistent default for agent-facing output. If a future CLI command needs verbose diagnostics, that is an invocation option.

No config fields for:

```text
API keys
model keys
Codex tokens
raw env vars
workspace path
source roots
MCP HTTP credentials
LLM provider
runtime plugins
arbitrary metadata
```

Hero Passport MVP needs none of those.

---

## 6. Configuration validation

Use typed options plus explicit validation at startup/command boundary.

Failure rules:

- unsupported config version -> `HP300 unsupported_config_version`;
- malformed JSON -> `HP301 invalid_config`;
- unsupported enum/value -> `HP301 invalid_config` with field name;
- unreadable config -> `HP302 config_unavailable`;
- invalid home/data path -> `HP210 app_data_unavailable`.

Do not silently ignore unknown properties. Silent acceptance makes typos look like successful configuration.

The `doctor` command should distinguish:

```text
missing config -> valid, defaults will be used
malformed config -> error
unsupported configVersion -> error
unwritable config directory -> error
```

---

## 7. Application state vs configuration

Keep these concepts separate.

### Configuration

User/operator preference:

```text
locale
presentation mode
local diagnostics policy
```

### SQLite application state

Product state:

```text
heroes
active hero selection
projects
quests
XP
skills
traits
settings that are part of user data
```

Do not put `activeHeroId` in config.json if it is logically part of the user's Hero Passport state. Store it in `app_settings` (or an equivalent typed settings record) with FK/validation behavior defined by the application.

This makes export/reset/data ownership coherent.

---

## 8. Project identity configuration

The normal MCP contract never sends a workspace path.

Local project resolution order:

```text
1. explicit application/CLI project override when a specific command supports one
2. Git repository root detected from server process cwd
3. normalized current working directory fallback
```

Codex has a native `mcp_servers.<id>.cwd` option for stdio servers. That is the correct place to pin a server process to a project when host behavior requires an explicit working directory.

The resolver persists only a versioned fingerprint and display name by default, not the full absolute path.

---

## 9. Environment-variable policy

Supported baseline variable:

```text
HERO_PASSPORT_HOME
```

Additional variables may be introduced only when they solve a real deployment need and are documented here.

Security rules:

- never enumerate environment variables for diagnostics;
- never dump process environment on exceptions;
- never log unknown environment variables;
- never return environment values via MCP;
- do not inherit or forward secrets intentionally from Hero Passport to child processes — MVP launches no child tool process requiring secrets.

Note: a stdio MCP client may launch the server with inherited environment variables. Hero Passport cannot prevent the parent process from doing that, but it can avoid inspecting/logging them.

---

## 10. Codex configuration ownership

Hero Passport does not own `~/.codex/config.toml`.

Preferred installation:

```bash
codex mcp add hero-passport -- hero-passport mcp
```

For project-local explicit configuration, use Codex's own trusted project `.codex/config.toml` and documented fields such as:

```toml
[mcp_servers.hero-passport]
command = "hero-passport"
args = ["mcp"]
cwd = "/path/to/project"
enabled_tools = [
  "hero.start_quest",
  "hero.finish_quest",
  "hero.current_quest",
  "hero.get_card"
]
```

Do not implement a Hero Passport command that mutates Codex configuration in MVP. We may print validated guidance/examples, but Codex remains the owner of its configuration format and lifecycle.

---

## 11. CLI output configuration

Output surfaces are separate:

### Human CLI

Default: concise text.

Commands that are useful to scripts should support:

```text
--json
```

with a versioned command-specific JSON shape.

### MCP

MCP output is determined by tool output schemas and local presentation mode; it is not controlled by a per-call `outputMode` field.

### Diagnostics

stderr and optional local file only.

No normal diagnostic stream shares MCP stdout.

---

## 12. Config migration policy

`configVersion` is independent from:

```text
EF database migration version
MCP protocol version
MCP tool contract compatibility
RPG rule versions
application package version
```

When config v2 is eventually required:

- reader can support the immediately previous version if migration is simple;
- migrations must be deterministic;
- backup original before destructive rewrite;
- `doctor` reports version and migration requirement;
- no silent semantic reinterpretation of a field.

---

## 13. Initialization lifecycle

`hero-passport init` or equivalent first-run initialization should:

```text
resolve paths
validate/create directories
load/validate config or defaults
open database
verify native SQLite version
apply EF migrations under EF migration locking
apply required PRAGMAs/checks
seed canonical default data idempotently
close database
report concise result
```

MCP startup may perform lightweight safe initialization, but should not hide a long/hung migration. Implementation must define a bounded startup policy and clear remediation message.

---

## 14. Doctor contract

`hero-passport doctor` is the canonical local diagnostic entrypoint.

It should report, without secrets or absolute project-path leakage by default:

```text
application version
.NET runtime
OS/architecture
resolved data/config/state directory status
configuration version/status
database present/readable/writable
SQLite native version
journal mode
synchronous mode
foreign_keys state
latest EF migration / pending migration status
possible stale __EFMigrationsLock condition
canonical seed state
MCP tool manifest version/hash
```

A `--verbose` human diagnostic may show full local paths, because a human explicitly requested it in the local terminal. `--json` should still avoid secrets and mark path fields clearly.

Never auto-delete `__EFMigrationsLock` as a routine doctor action. Recovery is explicit.

---

## 15. Test requirements

Configuration/path tests cover:

- Windows LocalApplicationData semantics via injected platform/path resolver;
- Linux XDG variables set/unset/empty;
- macOS Application Support mapping;
- `HERO_PASSPORT_HOME` override;
- relative/invalid override normalization;
- unknown config field rejection;
- malformed config;
- unsupported version;
- config/default precedence;
- test isolation with separate roots;
- no path appears in MCP DTO serialization.

## 16. Primary sources

- .NET `Environment.SpecialFolder`: https://learn.microsoft.com/en-us/dotnet/api/system.environment.specialfolder?view=net-10.0
- XDG Base Directory Specification: https://specifications.freedesktop.org/basedir/latest/
- Apple Application Support directory: https://developer.apple.com/documentation/foundation/url/applicationsupportdirectory
- Codex MCP documentation: https://developers.openai.com/codex/mcp/
- Codex configuration reference: https://developers.openai.com/codex/config-reference/
