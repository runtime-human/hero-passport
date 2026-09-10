#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import re
import stat
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo

DETERMINISTIC_ZIP_TIME = (1980, 1, 1, 0, 0, 0)
VERSION_PATTERN = re.compile(r"^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$")
SHA_PATTERN = re.compile(r"^[0-9a-f]{40}$")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def zip_info(name: str, executable: bool = False) -> ZipInfo:
    info = ZipInfo(name, DETERMINISTIC_ZIP_TIME)
    info.create_system = 3
    mode = 0o755 if executable else 0o644
    info.external_attr = (stat.S_IFREG | mode) << 16
    info.compress_type = ZIP_DEFLATED
    return info


def main() -> int:
    parser = argparse.ArgumentParser(description="Build the deterministic Hero Passport release archive.")
    parser.add_argument("--publish-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--version", required=True)
    parser.add_argument("--source-sha", required=True)
    args = parser.parse_args()

    publish_dir = args.publish_dir.resolve()
    output_dir = args.output_dir.resolve()
    version = args.version.strip()
    source_sha = args.source_sha.strip().lower()

    if not publish_dir.is_dir():
        raise SystemExit(f"publish directory does not exist: {publish_dir}")
    if not VERSION_PATTERN.fullmatch(version):
        raise SystemExit(f"invalid release version: {version}")
    if not SHA_PATTERN.fullmatch(source_sha):
        raise SystemExit("source SHA must be exactly 40 lowercase hexadecimal characters")

    output_dir.mkdir(parents=True, exist_ok=True)
    archive_name = f"hero-passport-{version}.zip"
    archive_path = output_dir / archive_name
    checksum_path = output_dir / "SHA256SUMS"
    external_manifest_path = output_dir / f"hero-passport-{version}.manifest.json"
    root_name = f"hero-passport-{version}"

    files: list[Path] = []
    for path in publish_dir.rglob("*"):
        if path.is_symlink():
            raise SystemExit(f"release publish tree must not contain symlinks: {path}")
        if path.is_file():
            files.append(path)
    files = sorted(files, key=lambda value: value.relative_to(publish_dir).as_posix())
    if not files:
        raise SystemExit("release publish tree is empty")

    internal_manifest = {
        "format": "hero-passport-release/1",
        "product": "Hero Passport",
        "version": version,
        "sourceSha": source_sha,
        "fileCount": len(files),
    }
    internal_manifest_bytes = (
        json.dumps(internal_manifest, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n"
    ).encode("utf-8")

    if archive_path.exists():
        archive_path.unlink()

    with ZipFile(archive_path, "w", compression=ZIP_DEFLATED, compresslevel=9, strict_timestamps=True) as archive:
        virtual_entries: list[tuple[str, bytes, bool]] = []
        for path in files:
            relative = path.relative_to(publish_dir).as_posix()
            executable = bool(path.stat().st_mode & 0o111)
            virtual_entries.append((f"{root_name}/{relative}", path.read_bytes(), executable))
        virtual_entries.append((f"{root_name}/release-manifest.json", internal_manifest_bytes, False))

        for archive_name_entry, data, executable in sorted(virtual_entries, key=lambda entry: entry[0]):
            archive.writestr(zip_info(archive_name_entry, executable), data)

    archive_sha256 = sha256_file(archive_path)
    checksum_path.write_text(f"{archive_sha256}  {archive_path.name}\n", encoding="utf-8", newline="\n")
    external_manifest = {
        **internal_manifest,
        "archive": archive_path.name,
        "sha256": archive_sha256,
    }
    external_manifest_path.write_text(
        json.dumps(external_manifest, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )

    print(json.dumps(external_manifest, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
