# Hero Passport — Project Identity & Binding Deep Dive

**Status:** Accepted normative deep-dive  
**Snapshot:** 2026-08-11  
**Identity algorithm:** `project-identity/1`  
**Scope:** local project binding, Git/worktree/monorepo/submodule behavior, privacy-preserving durable identity

This document is the detailed source of truth for project identity. If a shorter statement in `ARCHITECTURE.md`, `API-CONTRACTS.md`, `CONFIGURATION.md` or an implementation plan disagrees with this document, this document wins and the shorter document must be corrected.

---

## 1. Problem statement

Hero Passport needs a stable local `ProjectId` without asking the model to send a workspace path and without persisting the full local path.

The identity must behave predictably for:

- a normal Git checkout;
- nested working directories;
- Git linked worktrees;
- monorepos;
- submodules;
- nested repositories;
- non-Git directories;
- symlinks/junctions;
- repository moves and clones;
- hostile or untrusted Git repositories;
- hosts that launch MCP from different working directories.

There is no mandatory MCP workspace primitive suitable for this purpose in the 2026 protocol line. Project binding is therefore a **local launch/application concern**, not a model-facing MCP field.

---

## 2. Definitions

### 2.1 Binding start directory

The directory from which project resolution begins:

```text
explicit --project-root, if supplied
else process current working directory
```

The path exists only in local process memory/configuration.

### 2.2 Git worktree top-level

The path returned by Git `rev-parse --show-toplevel` for the selected worktree.

### 2.3 Git repository anchor

The canonical absolute path returned by Git `rev-parse --path-format=absolute --git-common-dir`.

This is intentionally **not** ordinary `$GIT_DIR`.

Linked Git worktrees have different per-worktree `$GIT_DIR` paths but share `$GIT_COMMON_DIR`. Using the common directory makes the same logical repository resolve to one Hero Passport project across linked worktrees.

### 2.4 Scope

A normalized repository-relative scope used only when the user explicitly binds a subdirectory within a Git repository.

```text
.                 = whole repository
src/backend       = explicit monorepo/subproject scope
```

### 2.5 Workspace fingerprint

A salted local SHA-256 digest used to look up/create a durable `projects` row. It is an identity aid, not authentication material and not encryption.

---

## 3. Binding precedence

Normative resolution order:

```text
1. explicit --project-root <path>
2. process current working directory
3. Git-aware normalization of that start directory when Git repository context exists
4. standalone-directory identity when no Git repository context exists
```

Do not accept `workspacePath`, `projectId` or equivalent path/identity hints from routine HP-MCP calls.

A host can bind a project by:

- launching `hero-passport mcp` with the correct cwd; or
- adding `--project-root <path>` to the local process arguments.

`--project-root` is the portable fallback because MCP hosts use different configuration shapes.

---

## 4. Path pre-validation

Before invoking Git:

1. Reject null/empty explicit root.
2. Convert to an absolute path with `Path.GetFullPath`.
3. Require the path to exist.
4. Require it to be a directory.
5. Remove a trailing directory separator except for filesystem roots.
6. For a final path element that is a symbolic link or junction, resolve the final target with `Directory.ResolveLinkTarget(path, returnFinalTarget: true)` when available and use the resolved target for standalone identity.
7. Do not perform shell expansion, wildcard expansion or command interpolation.

`Directory.ResolveLinkTarget(..., true)` resolves symbolic links and Windows junctions. Intermediate-component aliases and hard-link-style filesystem aliases are not guaranteed to collapse to one standalone identity in v1.

Errors:

```text
HP310 invalid_project_binding
```

Do not echo the absolute path in MCP-facing error text.

---

## 5. Git invocation policy

Git discovery uses the Git executable as an external read-only resolver rather than parsing `.git` internals ourselves.

Invocation rules:

- use `ProcessStartInfo.ArgumentList`; never construct a shell command;
- use `git -C <bindingStart> ...`;
- capture stdout/stderr with strict size bounds;
- set a short startup/command timeout appropriate for local metadata queries;
- set `GIT_OPTIONAL_LOCKS=0`;
- remove inherited repository-location overrides that could redirect discovery away from the bound directory, including `GIT_DIR`, `GIT_WORK_TREE`, `GIT_COMMON_DIR`, `GIT_INDEX_FILE`, `GIT_OBJECT_DIRECTORY`, `GIT_ALTERNATE_OBJECT_DIRECTORIES` and `GIT_CEILING_DIRECTORIES`;
- do not modify Git configuration;
- do not add `safe.directory` entries;
- do not execute Git hooks;
- do not access remotes or the network.

Prefer separate, parseable `rev-parse` calls over depending on the number/order of blank lines from a multi-option command.

Required probes when Git repository discovery succeeds:

```text
git -C <root> rev-parse --is-inside-work-tree
git -C <root> rev-parse --is-bare-repository
git -C <root> rev-parse --path-format=absolute --show-toplevel
git -C <root> rev-parse --path-format=absolute --git-common-dir
git -C <root> rev-parse --show-prefix
git -C <root> rev-parse --show-superproject-working-tree   # diagnostic only
```

The superproject result never silently changes the selected project.

---

## 6. Git trust failures are not standalone-directory fallback

Git intentionally refuses repositories that fail its ownership/safety policy unless the user configures `safe.directory` in protected configuration.

Hero Passport must not weaken that protection.

If a `.git` file/directory marker is discoverable at or above the binding start but Git cannot safely resolve the repository because it is untrusted, malformed or unreadable:

```text
HP311 git_repository_unavailable
```

The human diagnostic may instruct the user to run Git directly and resolve its safety/configuration problem. Hero Passport must not automatically write `safe.directory=*` or another exception.

If the Git executable cannot be started and a `.git` marker is found in the ancestor chain:

```text
HP312 git_required_for_repository_binding
```

This avoids silently treating a Git repository as a different standalone project merely because Git disappeared from PATH.

If Git is unavailable and no `.git` marker exists, standalone identity is allowed.

---

## 7. Bare repositories

A bare repository has no normal working tree and is outside the 0.1 project-aware coding-agent profile.

If Git reports a bare repository:

```text
HP313 bare_repository_unsupported
```

Do not create an accidental standalone-directory project for a bare repository.

---

## 8. Default Git project semantics

When no explicit `--project-root` is supplied and process cwd is anywhere inside a Git worktree:

```text
anchor = canonical absolute Git common directory
scope  = "."
```

The current nested cwd does **not** split the repository into separate Hero Passport projects.

Example:

```text
repo: /work/hero-passport
cwd:  /work/hero-passport/src/HeroPassport.App

identity scope -> .
```

This is important for IDEs and shells that launch tools from different subdirectories.

---

## 9. Monorepo semantics

Default behavior treats one Git repository as one Hero Passport project.

A user who intentionally wants a monorepo subdirectory to have independent Hero Passport progression uses an explicit project root:

```text
hero-passport mcp --project-root /repo/services/billing
```

If the explicit directory is inside a Git worktree:

```text
anchor = Git common directory
scope  = normalized Git top-level-relative explicit-root prefix
```

The scope is derived from Git's repository-relative view (`--show-prefix`), normalized to forward slashes and with no leading/trailing slash. An empty prefix becomes `.`.

Consequences:

- `/repo/services/billing` and the same scope in another linked worktree resolve to the same project;
- `/repo/services/catalog` resolves to a different project;
- ordinary cwd changes within `/repo` do not create new projects when no explicit scope was requested.

Do not automatically infer monorepo boundaries from solution files, package manifests, folders or model text in v1.

---

## 10. Git linked worktrees

Git documents linked worktrees as having a private `$GIT_DIR` while `$GIT_COMMON_DIR` points to the main repository metadata shared by all worktrees.

Therefore `project-identity/1` uses the **Git common directory** as its Git anchor.

Example:

```text
/main/project/.git
/worktrees/feature/.git -> gitfile to /main/project/.git/worktrees/feature

Git common dir for both -> /main/project/.git
```

With scope `.` both worktrees resolve to the same Hero Passport project.

With explicit scope `services/billing`, both worktrees also resolve to the same scoped project.

Branch name, HEAD commit and worktree private Git directory are deliberately absent from project identity.

---

## 11. Submodules

A Git submodule is a separate Git repository and therefore resolves as a separate Hero Passport project by default.

When launched inside the submodule:

```text
anchor = submodule Git common directory
scope  = .
```

`--show-superproject-working-tree` may be recorded transiently for diagnostics but is never used to jump automatically to the parent project.

If the user wants the superproject identity, the host must launch/bind the superproject root explicitly.

This rule avoids surprising cross-project reward aggregation between independently versioned repositories.

---

## 12. Nested repositories

An independently initialized Git repository nested beneath another repository is treated like any other nearest/current Git repository: it receives its own Git anchor and therefore its own Hero Passport project.

No parent-repository walk is performed after a valid inner repository has been resolved.

---

## 13. Sparse checkout

Sparse checkout changes which worktree paths are materialized, not the Git repository anchor.

It does not change project identity.

---

## 14. Standalone non-Git directory identity

If Git confirms that the binding start is not inside a repository, or Git is unavailable and no `.git` marker is found:

```text
kind   = standalone
anchor = canonical/resolved absolute binding directory
scope  = .
```

Standalone identity is intentionally path-based in v1.

Do not invent file-content hashes, scan directory contents or create hidden identity files inside user projects.

---

## 15. Fingerprint algorithm

### 15.1 Installation salt

Generate one installation-local 32-byte random salt using `RandomNumberGenerator.GetBytes(32)` on first initialization.

Persist it with product state as:

```text
project_identity_salt_v1
```

The salt is copied with a normal database backup, so restored state retains project lookup behavior.

### 15.2 Canonical material

For Git:

```text
project-identity/1\0git\0<canonical-common-dir>\0<scope>
```

For standalone:

```text
project-identity/1\0standalone\0<canonical-directory>\0.
```

Use UTF-8 encoding.

### 15.3 Digest

```text
WorkspaceFingerprint = lowerhex(
    SHA-256(
        salt || 0x00 || UTF8(canonicalMaterial)
    )
)
```

The digest is 64 lowercase hexadecimal characters.

Salted hashing reduces straightforward cross-install correlation and precomputed lookup, but it **does not make local paths secret from an attacker who can read both the database and salt**. The fingerprint is not a credential.

Do not log the canonical material or unsalted path.

---

## 16. Display name

`DisplayName` is cosmetic and never participates in identity.

Initial suggestion:

- default Git project: leaf name of the resolved worktree top-level observed on first creation;
- explicit Git scope: leaf name of the explicit scope;
- standalone: leaf directory name.

Because linked worktrees can have different folder names, the first successful creation wins until a later explicit rename feature exists.

A display-name change must never change `ProjectId` or fingerprint.

---

## 17. Move, clone and machine semantics

### Repository move

Moving the main repository metadata/common directory to a new local path changes the Git anchor and therefore produces a new `project-identity/1` fingerprint.

This is an accepted v1 limitation.

Hero Passport does not write a persistent UUID into `.git` merely to detect moves.

A future explicit `project relink`/merge workflow may reconcile projects safely if real usage demands it.

### Fresh clone

A fresh clone is intentionally a different local project because no trustworthy stable repository UUID exists in standard Git metadata.

Remote URLs are mutable, can expose sensitive data and are not unique repository identities; they are not used.

### Different machine/container/WSL namespace

Project identity is local to the filesystem namespace that owns the Hero Passport data store. Sharing one Hero Passport database across Windows, WSL, containers or machines with different path namespaces is outside the 0.1 supported storage/profile model and may produce distinct identities.

---

## 18. Case and filesystem alias policy

For Git repositories, trust Git's canonical absolute path output rather than applying Hero Passport-wide case folding.

Do not lowercase paths globally: Windows supports per-directory case sensitivity and Unix filesystems are normally case-sensitive.

For standalone directories, use platform APIs conservatively (`Path.GetFullPath`, final symlink/junction resolution). v1 does not promise to collapse every possible alias/hard-link/intermediate-symlink path to one identity.

This limitation is preferable to unsafe cross-platform path heuristics.

---

## 19. Privacy boundary

Transiently allowed in the local resolver:

```text
absolute binding start
Git top-level path
Git common directory
repo-relative explicit scope
```

Persisted:

```text
ProjectId
DisplayName
WorkspaceFingerprint
IdentityVersion
CreatedAtUtc
LastSeenAtUtc
```

Forbidden by default:

```text
absolute workspace path
Git remote URL
Git username/email
branch name
HEAD SHA
file list
source contents
```

No project-path value appears in HP-MCP/2 structured results, text results, errors or ordinary logs.

---

## 20. Security properties

The resolver must not:

- invoke a shell;
- interpolate untrusted path text into one command string;
- modify `.git` files;
- modify Git configuration;
- disable Git safe-directory protections;
- fetch or contact remotes;
- execute hooks;
- accept a model-provided workspace path;
- trust inherited Git environment variables over the explicit/local binding root.

Paths beginning with `-`, spaces, quotes, Unicode and shell metacharacters are passed as literal `ArgumentList` values.

---

## 21. Error contract

```text
HP310 invalid_project_binding
  missing path, file instead of directory, unusable explicit binding

HP311 git_repository_unavailable
  repository marker/context exists but Git cannot safely/readably resolve it

HP312 git_required_for_repository_binding
  Git executable unavailable while repository markers indicate Git semantics are required

HP313 bare_repository_unsupported
  selected binding is a bare Git repository
```

These are configuration/binding errors. MCP-facing error messages remain path-free.

---

## 22. Required golden/integration vectors

### Normal Git

1. repo root cwd -> scope `.`;
2. nested cwd -> same fingerprint as root;
3. spaces/Unicode in path -> same expected canonical result;
4. path beginning with dash -> treated literally.

### Worktree

5. main worktree and linked worktree -> same fingerprint;
6. explicit identical relative subproject in both worktrees -> same fingerprint.

### Monorepo

7. cwd `services/a` without explicit root -> whole-repo identity;
8. explicit `services/a` -> scoped identity different from whole repo;
9. explicit `services/b` -> distinct from `services/a`.

### Submodule/nested repo

10. submodule root -> distinct project from superproject;
11. nested independent repository -> distinct from parent;
12. explicit parent root while cwd below -> parent identity.

### Standalone

13. ordinary directory -> stable fingerprint across repeated runs;
14. final symlink/junction alias -> resolves to target where platform API supports it;
15. file path -> HP310;
16. nonexistent path -> HP310.

### Git failure/trust

17. bare repository -> HP313;
18. Git missing + `.git` marker -> HP312;
19. Git missing + no marker -> standalone allowed;
20. unsafe/unreadable repository -> HP311 and no `safe.directory` mutation.

### Privacy

21. persisted database contains fingerprint but not absolute path;
22. normal diagnostics/errors do not contain absolute path;
23. Git remote URL is never queried/persisted.

### Known limitation evidence

24. moving repository common directory -> new fingerprint, documented not accidental;
25. fresh clone -> new fingerprint.

---

## 23. Implementation interfaces

Suggested Infrastructure types:

```csharp
public interface IProjectBindingResolver
{
    ProjectBinding Resolve(
        string? explicitProjectRoot,
        string processWorkingDirectory);
}

public sealed record ProjectBinding(
    string DisplayName,
    string WorkspaceFingerprint,
    string IdentityVersion);
```

Path/Git details do not leave Infrastructure.

Suggested internal components:

```text
ProjectBindingResolver
GitRepositoryProbe
StandaloneDirectoryProbe
ProjectIdentityV1
ProjectIdentitySaltStore
```

Do not create a public Git abstraction framework; these are small product-specific components.

---

## 24. Revisit triggers

Revisit `project-identity/1` only when one of these becomes a demonstrated requirement:

- users frequently move repositories and need progression to follow automatically;
- users intentionally share one Hero Passport data store across machines/namespaces;
- monorepo auto-discovery becomes necessary;
- a standard, trustworthy repository identity primitive appears in Git/MCP/host APIs;
- cloud/team project identity is introduced.

Any replacement algorithm gets a new identity version and an explicit migration/relink policy. Never silently reinterpret stored fingerprints.

---

## 25. Official references verified 2026-08-11

- Git `rev-parse`: https://git-scm.com/docs/git-rev-parse
- Git worktrees: https://git-scm.com/docs/git-worktree
- Git repository layout: https://git-scm.com/docs/gitrepository-layout
- Git `safe.directory` / protected configuration: https://git-scm.com/docs/git-config
- .NET `Directory.ResolveLinkTarget`: https://learn.microsoft.com/dotnet/api/system.io.directory.resolvelinktarget?view=net-10.0
- MCP 2026-07-28 release/deprecations: https://blog.modelcontextprotocol.io/posts/2026-07-28/
