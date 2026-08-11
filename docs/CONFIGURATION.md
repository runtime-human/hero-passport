# Hero Passport — Configuration and Binding

**Status:** Accepted v3.1  
**Snapshot:** 2026-08-11

Exact project resolution/fingerprinting is normative in [`PROJECT-IDENTITY.md`](PROJECT-IDENTITY.md). This file owns configuration shape and launch precedence only.

---

## 1. Configuration philosophy

Hero Passport owns Hero Passport configuration. MCP hosts own their registration/configuration files.

```text
Hero Passport config != Codex/VS Code/Cursor/Claude/JetBrains/Zed config
```

Hero Passport does not mutate third-party configuration by default.

---

## 2. Config v1

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

Allowed:

```text
locale: ru | en
presentation: compact | normal
diagnostics.fileLogging: boolean
```

Unknown fields/current-version values are rejected.

Config v1 does not store:

```text
API/model/Codex tokens
workspace path
project fingerprint salt
MCP host definitions
plugins
active quest IDs
raw environment dumps
```

`project_identity_salt_v1` is durable product/database state, not user config.

Active/default hero is product state.

---

## 3. Precedence

Where an option supports all levels:

```text
explicit CLI/startup option
> documented HERO_PASSPORT_* env override
> config.json
> built-in default
```

Do not invent environment variables for internal constants.

---

## 4. Data/config/state roots

### Windows

```text
%LOCALAPPDATA%\HeroPassport\
  data\hero-passport.db
  config\config.json
  state\logs\
```

Do not use roaming `%APPDATA%` for the DB.

### macOS

```text
~/Library/Application Support/HeroPassport/
  data/hero-passport.db
  config/config.json
  state/logs/
```

### Linux

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

## 5. `HERO_PASSPORT_HOME`

Overrides all roots for dev/test isolation:

```text
$HERO_PASSPORT_HOME/
  data/
  config/
  state/
```

Tests use a unique temp root and never touch user state.

The override path does not appear in MCP snapshots/output.

---

## 6. Writable database location

The supported 0.1 writable SQLite/WAL profile is local filesystem on the same host.

Known network filesystems/shares are rejected where reliably detectable:

```text
HP211 unsupported_storage_location
```

Do not use a cloud-sync/network-share folder as a multi-machine SQLite sharing mechanism.

Exact policy: `PERSISTENCE-RELIABILITY.md`.

---

## 7. Project startup binding

Supported stdio command:

```text
hero-passport mcp [--project-root <directory>] [--hero <selector>]
```

Binding start:

```text
explicit --project-root
else process cwd
```

Infrastructure then runs `project-identity/1` from `PROJECT-IDENTITY.md`.

Important consequences:

```text
normal Git nested cwd -> whole repo
linked worktree -> shared Git common-dir identity
explicit in-repo --project-root -> deliberate repo-relative scope
submodule/nested repo -> separate by default
non-Git -> standalone local directory identity
```

Do not simplify this to “hash Git top-level path”; worktree/scoped semantics are part of the contract.

---

## 8. Git binding errors

```text
HP310 invalid_project_binding
HP311 git_repository_unavailable
HP312 git_required_for_repository_binding
HP313 bare_repository_unsupported
```

A Git safety/ownership failure does not fall back to a standalone project and Hero Passport never modifies `safe.directory`.

Git location environment overrides are sanitized by the resolver according to `PROJECT-IDENTITY.md` so inherited variables do not silently redirect the requested binding.

---

## 9. Why `--project-root` exists

MCP host config is not standardized.

Hosts may expose cwd/project settings differently. `--project-root` is the portable explicit fallback.

Absolute path remains process-local binding input and is never sent as routine HP-MCP data or persisted as project identity.

---

## 10. No MCP Roots dependency

0.1 project identity does not depend on MCP Roots.

A single global server cannot infer a different local project from each tool call unless the host provides a trusted binding channel; Hero Passport will not guess from goal text, editor file or client name.

Supported stdio profile is project-bound launch.

---

## 11. Hero binding

Default: active/default local hero.

Optional:

```text
--hero <name-or-id>
```

Unknown/ambiguous selector fails before tool execution.

Client/host name does not implicitly choose hero.

---

## 12. Invocation metadata

MCP client metadata may enter bounded in-memory diagnostics as `InvocationOrigin`.

Default:

```text
not persisted raw
not hero/project selection
not auth
not reward input
```

---

## 13. `doctor`

Normal checks:

```text
Hero Passport/.NET/OS/arch
resolved data/config/state roots
configVersion/unknown properties
hero binding
project binding diagnostics when applicable
DB readability/writability
known local-storage support
actual sqlite_version() and >=3.51.3 qualification
journal_mode=WAL
synchronous=FULL
foreign_keys=ON
EF migrations / suspicious migration-lock state
PRAGMA quick_check
PRAGMA foreign_key_check
canonical seeds
exact MCP four-tool manifest
ProtocolVersion policy
```

Normal doctor is non-destructive. It does not delete WAL/SHM, modify Git safe.directory, drop migration locks, rewrite DB or change host config.

Explicit verbose local diagnostics may show local paths to the user terminal; path data is never copied into MCP responses by default.

---

## 14. Host integration descriptors

A future polish command may print, but not auto-apply, host-specific snippets:

```text
hero-passport integration show codex
hero-passport integration show vscode
...
```

Host configuration remains external and versioned by each host.

---

## 15. Future HTTP config

No HTTP settings in config v1.

Future project-scoped HTTP and future public/multi-tenant HTTP are separate deployment profiles with different auth/project binding/trust models; do not add a generic `http=true` switch.

---

## 16. Config evolution

```text
unknown future configVersion -> fail clearly
unknown property in current version -> reject
migration deterministic
never discard unknown security-relevant settings silently
```

Config version remains independent of HP-MCP, EF migrations, SafeText/Dedup/ProjectIdentity and RPG rule versions.
