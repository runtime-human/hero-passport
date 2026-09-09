#!/usr/bin/env bash
set -euo pipefail

: "${HERO_PASSPORT_PUBLISH_DIR:?HERO_PASSPORT_PUBLISH_DIR must point to the published Hero Passport bundle}"

CODEX_VERSION=0.153.4
CODEX_SHA256=f479424eca092484dc40d87ae28c44f4cc40234a60045d6131e493800d814a30
CODEX_URL="https://github.com/openai/codex/releases/download/rust-v${CODEX_VERSION}/codex-x86_64-unknown-linux-musl.tar.gz"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
publish_dir="$(cd "$HERO_PASSPORT_PUBLISH_DIR" && pwd)"
app_dll="$publish_dir/HeroPassport.App.dll"
skill_dir="$publish_dir/skills/hero-passport"

[[ -f "$app_dll" ]] || { echo "Missing packaged HeroPassport.App.dll" >&2; exit 1; }
[[ -f "$skill_dir/SKILL.md" ]] || { echo "Missing packaged Hero Passport Agent Skill" >&2; exit 1; }

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

archive="$work_dir/codex.tar.gz"
extract_dir="$work_dir/codex-extract"
bin_dir="$work_dir/bin"
mkdir -p "$extract_dir" "$bin_dir"

curl --fail --location --silent --show-error --retry 3 --output "$archive" "$CODEX_URL"
printf '%s  %s\n' "$CODEX_SHA256" "$archive" | sha256sum --check --status

tar -xzf "$archive" -C "$extract_dir"
codex_source="$(find "$extract_dir" -type f -name 'codex*' -print -quit)"
[[ -n "$codex_source" ]] || { echo "Pinned Codex archive did not contain a binary" >&2; exit 1; }
install -m 0755 "$codex_source" "$bin_dir/codex"
codex_bin="$bin_dir/codex"

version_output="$($codex_bin --version 2>&1)"
printf 'Codex version: %s\n' "$version_output"
grep -F "$CODEX_VERSION" <<<"$version_output" >/dev/null

export CODEX_HOME="$work_dir/codex-home"
export HOME="$work_dir/home"
mkdir -p "$CODEX_HOME" "$HOME"

project_dir="$work_dir/project"
mkdir -p "$project_dir/.agents/skills"
git -C "$project_dir" init -q
ln -s "$skill_dir" "$project_dir/.agents/skills/hero-passport"
[[ -f "$project_dir/.agents/skills/hero-passport/SKILL.md" ]]

cd "$project_dir"
"$codex_bin" mcp add hero-passport -- dotnet "$app_dll" mcp --project-root "$project_dir"

list_output="$($codex_bin mcp list 2>&1)"
printf '%s\n' "$list_output"
grep -F "hero-passport" <<<"$list_output" >/dev/null

get_output="$($codex_bin mcp get hero-passport 2>&1)"
printf '%s\n' "$get_output"
grep -F "hero-passport" <<<"$get_output" >/dev/null
grep -F "HeroPassport.App.dll" <<<"$get_output" >/dev/null
grep -F -- "--project-root" <<<"$get_output" >/dev/null

[[ -f "$CODEX_HOME/config.toml" ]] || { echo "Codex did not persist isolated MCP configuration" >&2; exit 1; }
grep -F 'mcp_servers.hero-passport' "$CODEX_HOME/config.toml" >/dev/null

printf 'Codex host configuration smoke passed for %s\n' "$CODEX_VERSION"
python3 "$repo_root/tests/qualification/codex-host-runtime-smoke.py" \
  --codex "$codex_bin" \
  --project-dir "$project_dir"
