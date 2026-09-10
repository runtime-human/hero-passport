#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import stat
import subprocess
from pathlib import Path, PurePosixPath
from zipfile import ZipFile

VERSION_PATTERN = re.compile(r"^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$")
SHA_PATTERN = re.compile(r"^[0-9a-f]{40}$")
HEX256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
FORMAT = "hero-passport-release/1"
PRODUCT = "Hero Passport"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path) -> dict[str, object]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise SystemExit(f"invalid JSON file {path.name}: {error}") from error
    if not isinstance(value, dict):
        raise SystemExit(f"JSON root must be an object: {path.name}")
    return value


def require_manifest(manifest: dict[str, object], *, version: str, source_sha: str, file_count: int) -> None:
    expected = {
        "format": FORMAT,
        "product": PRODUCT,
        "version": version,
        "sourceSha": source_sha,
        "fileCount": file_count,
    }
    for key, value in expected.items():
        if manifest.get(key) != value:
            raise SystemExit(f"manifest mismatch for {key}: expected {value!r}, got {manifest.get(key)!r}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify and safely extract an exact Hero Passport release archive.")
    parser.add_argument("--release-dir", required=True, type=Path)
    parser.add_argument("--extract-dir", required=True, type=Path)
    parser.add_argument("--version", required=True)
    parser.add_argument("--source-sha", required=True)
    args = parser.parse_args()

    release_dir = args.release_dir.resolve()
    extract_dir = args.extract_dir.resolve()
    version = args.version.strip()
    source_sha = args.source_sha.strip().lower()

    if not VERSION_PATTERN.fullmatch(version):
        raise SystemExit(f"invalid release version: {version}")
    if not SHA_PATTERN.fullmatch(source_sha):
        raise SystemExit("source SHA must be exactly 40 lowercase hexadecimal characters")
    if not release_dir.is_dir():
        raise SystemExit(f"release directory does not exist: {release_dir}")

    archive_name = f"hero-passport-{version}.zip"
    archive_path = release_dir / archive_name
    external_manifest_path = release_dir / f"hero-passport-{version}.manifest.json"
    checksum_path = release_dir / "SHA256SUMS"
    for path in (archive_path, external_manifest_path, checksum_path):
        if not path.is_file():
            raise SystemExit(f"missing release file: {path.name}")

    checksum_lines = checksum_path.read_text(encoding="utf-8").splitlines()
    if len(checksum_lines) != 1:
        raise SystemExit("SHA256SUMS must contain exactly one checksum line")
    parts = checksum_lines[0].split()
    if len(parts) != 2 or parts[1] != archive_name or not HEX256_PATTERN.fullmatch(parts[0]):
        raise SystemExit("SHA256SUMS does not contain the canonical release archive entry")
    actual_sha256 = sha256_file(archive_path)
    if parts[0] != actual_sha256:
        raise SystemExit("release archive SHA-256 does not match SHA256SUMS")

    external_manifest = load_json(external_manifest_path)
    external_count = external_manifest.get("fileCount")
    if not isinstance(external_count, int) or isinstance(external_count, bool) or external_count <= 0:
        raise SystemExit("external manifest fileCount must be a positive integer")
    require_manifest(external_manifest, version=version, source_sha=source_sha, file_count=external_count)
    if external_manifest.get("archive") != archive_name:
        raise SystemExit("external manifest archive name mismatch")
    if external_manifest.get("sha256") != actual_sha256:
        raise SystemExit("external manifest archive SHA-256 mismatch")

    root_name = f"hero-passport-{version}"
    if extract_dir.exists():
        shutil.rmtree(extract_dir)
    extract_dir.mkdir(parents=True)

    seen: set[str] = set()
    payload_files = 0
    internal_manifest_bytes: bytes | None = None
    with ZipFile(archive_path, "r") as archive:
        for info in archive.infolist():
            name = info.filename
            if name in seen:
                raise SystemExit(f"duplicate ZIP entry: {name}")
            seen.add(name)
            if "\\" in name:
                raise SystemExit(f"ZIP entry uses a backslash: {name}")
            pure = PurePosixPath(name)
            if pure.is_absolute() or any(part in ("", ".", "..") for part in pure.parts):
                raise SystemExit(f"unsafe ZIP entry path: {name}")
            if not pure.parts or pure.parts[0] != root_name:
                raise SystemExit(f"ZIP entry escapes canonical release root: {name}")
            mode = info.external_attr >> 16
            if stat.S_ISLNK(mode):
                raise SystemExit(f"ZIP symlink entries are not permitted: {name}")

            destination = extract_dir.joinpath(*pure.parts)
            if info.is_dir():
                destination.mkdir(parents=True, exist_ok=True)
                continue

            destination.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(info, "r") as source, destination.open("wb") as target:
                shutil.copyfileobj(source, target)

            if pure.name == "release-manifest.json" and len(pure.parts) == 2:
                internal_manifest_bytes = destination.read_bytes()
            else:
                payload_files += 1

    if payload_files != external_count:
        raise SystemExit(f"archive payload count mismatch: expected {external_count}, got {payload_files}")
    if internal_manifest_bytes is None:
        raise SystemExit("release archive is missing root release-manifest.json")
    try:
        internal_manifest = json.loads(internal_manifest_bytes.decode("utf-8"))
    except (UnicodeError, json.JSONDecodeError) as error:
        raise SystemExit(f"invalid internal release manifest: {error}") from error
    if not isinstance(internal_manifest, dict):
        raise SystemExit("internal release manifest root must be an object")
    require_manifest(internal_manifest, version=version, source_sha=source_sha, file_count=external_count)

    app_dll = extract_dir / root_name / "HeroPassport.App.dll"
    if not app_dll.is_file():
        raise SystemExit("release archive is missing HeroPassport.App.dll")
    result = subprocess.run(
        ["dotnet", str(app_dll), "--version"],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    if result.returncode != 0:
        raise SystemExit(f"release --version failed with exit code {result.returncode}: {result.stderr.strip()}")
    if result.stdout.strip() != version:
        raise SystemExit(f"release --version mismatch: expected {version!r}, got {result.stdout.strip()!r}")

    print(
        f"Hero Passport release archive verified: version={version} source={source_sha} "
        f"sha256={actual_sha256} files={external_count}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
