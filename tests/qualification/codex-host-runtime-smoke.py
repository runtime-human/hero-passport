#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import subprocess
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any

EXPECTED_TOOLS = {
    "hero.bootstrap",
    "hero.configure",
    "hero.get_context",
    "hero.create",
    "hero.list",
    "hero.activate",
    "hero.archive",
    "hero.restore",
    "hero.start_quest",
    "hero.finish_quest",
    "hero.get_card",
}
EXPECTED_CODEX_NAMESPACE = "mcp__hero_passport"
EXPECTED_CODEX_TOOLS = {tool.replace(".", "_") for tool in EXPECTED_TOOLS}
EXPECTED_REPO_SKILL_RELATIVE = ".agents/skills/hero-passport/SKILL.md"


class CaptureServer(ThreadingHTTPServer):
    def __init__(self) -> None:
        super().__init__(("127.0.0.1", 0), CaptureHandler)
        self.requests: list[dict[str, Any]] = []
        self.lock = threading.Lock()

    @property
    def base_url(self) -> str:
        host, port = self.server_address
        return f"http://{host}:{port}/v1"

    def record(self, payload: dict[str, Any]) -> None:
        with self.lock:
            self.requests.append(payload)


class CaptureHandler(BaseHTTPRequestHandler):
    server: CaptureServer

    def log_message(self, _format: str, *_args: object) -> None:
        return None

    def do_GET(self) -> None:
        if self.path.endswith("/v1/models") or self.path.endswith("/models"):
            self._send_json(
                {
                    "object": "list",
                    "data": [
                        {
                            "id": "hero-passport-qualification-model",
                            "object": "model",
                            "created": 0,
                            "owned_by": "qualification",
                        }
                    ],
                }
            )
            return
        self.send_error(404, f"unexpected GET {self.path}")

    def do_POST(self) -> None:
        length = int(self.headers.get("content-length", "0"))
        raw = self.rfile.read(length)

        if not (self.path.endswith("/v1/responses") or self.path.endswith("/responses")):
            self.send_error(404, f"unexpected POST {self.path}")
            return

        try:
            payload = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            self.send_error(400, f"invalid JSON: {exc}")
            return

        if not isinstance(payload, dict):
            self.send_error(400, "expected Responses request object")
            return

        self.server.record(payload)
        response_id = "resp-hero-passport-runtime-smoke"
        events = [
            {"type": "response.created", "response": {"id": response_id}},
            {
                "type": "response.output_item.done",
                "item": {
                    "type": "message",
                    "role": "assistant",
                    "id": "msg-hero-passport-runtime-smoke",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "HERO_PASSPORT_RUNTIME_SMOKE",
                        }
                    ],
                },
            },
            {
                "type": "response.completed",
                "response": {
                    "id": response_id,
                    "usage": {
                        "input_tokens": 1,
                        "input_tokens_details": None,
                        "output_tokens": 1,
                        "output_tokens_details": None,
                        "total_tokens": 2,
                    },
                },
            },
        ]
        body = "".join(
            f"event: {event['type']}\ndata: {json.dumps(event)}\n\n" for event in events
        ).encode("utf-8")

        self.send_response(200)
        self.send_header("content-type", "text/event-stream")
        self.send_header("content-length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)
        self.wfile.flush()

    def _send_json(self, payload: dict[str, Any]) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(200)
        self.send_header("content-type", "application/json")
        self.send_header("content-length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def namespace_tools(payload: dict[str, Any]) -> dict[str, set[str]]:
    discovered: dict[str, set[str]] = {}
    tools = payload.get("tools")
    if not isinstance(tools, list):
        return discovered

    for spec in tools:
        if not isinstance(spec, dict) or spec.get("type") != "namespace":
            continue
        namespace = spec.get("name")
        nested = spec.get("tools")
        if not isinstance(namespace, str) or not isinstance(nested, list):
            continue

        names = discovered.setdefault(namespace, set())
        for tool in nested:
            if not isinstance(tool, dict) or tool.get("type") != "function":
                continue
            name = tool.get("name")
            if isinstance(name, str):
                names.add(name)

    return discovered


def developer_texts(payload: dict[str, Any]) -> list[str]:
    texts: list[str] = []
    inputs = payload.get("input")
    if not isinstance(inputs, list):
        return texts

    for item in inputs:
        if (
            not isinstance(item, dict)
            or item.get("type") != "message"
            or item.get("role") != "developer"
        ):
            continue
        content = item.get("content")
        if not isinstance(content, list):
            continue

        for span in content:
            if not isinstance(span, dict) or span.get("type") != "input_text":
                continue
            text = span.get("text")
            if isinstance(text, str):
                texts.append(text)

    return texts


def normalized_path(path: Path) -> str:
    return str(path).replace("\\", "/").rstrip("/")


def skill_root_aliases(texts: list[str]) -> dict[str, str]:
    aliases: dict[str, str] = {}
    for text in texts:
        for raw_line in text.splitlines():
            line = raw_line.strip()
            if not line.startswith("- `") or "` = `" not in line or not line.endswith("`"):
                continue
            left, right = line.split("` = `", 1)
            alias = left.removeprefix("- `")
            root = right.removesuffix("`")
            if alias and root:
                aliases[alias] = root.replace("\\", "/").rstrip("/")
    return aliases


def require_repo_skill_visible(texts: list[str], project_dir: Path) -> None:
    if not texts:
        raise SystemExit("Codex Responses request did not contain developer input_text messages")
    if not any("### Available skills" in text for text in texts):
        raise SystemExit("Codex developer context did not contain the available skills catalog")

    skill_entries = [
        line.strip()
        for text in texts
        for line in text.splitlines()
        if line.strip().startswith("- hero-passport:")
    ]
    if not skill_entries:
        raise SystemExit("Codex available skills catalog did not advertise hero-passport")

    expected_skill_path = normalized_path(project_dir / EXPECTED_REPO_SKILL_RELATIVE)
    if any(f"(file: {expected_skill_path})" in entry for entry in skill_entries):
        return

    expected_root = normalized_path(project_dir / ".agents" / "skills")
    aliases = skill_root_aliases(texts)
    matching_aliases = {
        alias for alias, root in aliases.items() if root == expected_root
    }
    if any(
        f"(file: {alias}/hero-passport/SKILL.md)" in entry
        for alias in matching_aliases
        for entry in skill_entries
    ):
        return

    raise SystemExit(
        "Codex advertised hero-passport, but not from the qualification project's repo skill root. "
        f"expected={expected_skill_path}; entries={skill_entries}; aliases={aliases}"
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--codex", required=True, type=Path)
    parser.add_argument("--project-dir", required=True, type=Path)
    args = parser.parse_args()

    codex = args.codex.resolve()
    project_dir = args.project_dir.resolve()
    if not codex.is_file():
        raise SystemExit(f"Codex binary not found: {codex}")
    if not (project_dir / ".git").exists():
        raise SystemExit(f"Qualification project is not a Git repository: {project_dir}")

    server = CaptureServer()
    thread = threading.Thread(target=server.serve_forever, name="responses-capture", daemon=True)
    thread.start()

    provider = (
        "model_providers.hero_passport_qualification="
        f"{{ name = 'Hero Passport qualification capture', base_url = '{server.base_url}', "
        "wire_api = 'responses', requires_openai_auth = false, request_max_retries = 0, "
        "stream_max_retries = 0 }"
    )
    command = [
        str(codex),
        "exec",
        "--json",
        "-m",
        "hero-passport-qualification-model",
        "-c",
        'model_provider="hero_passport_qualification"',
        "-c",
        "mcp_optional_startup_grace_ms=0",
        "-c",
        provider,
        "Return exactly HERO_PASSPORT_RUNTIME_SMOKE. Do not call any tools.",
    ]

    try:
        completed = subprocess.run(
            command,
            cwd=project_dir,
            env=os.environ.copy(),
            text=True,
            capture_output=True,
            timeout=45,
            check=False,
        )
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=2)

    if completed.stdout:
        print(completed.stdout, end="")
    if completed.stderr:
        print(completed.stderr, end="", file=os.sys.stderr)
    if completed.returncode != 0:
        raise SystemExit(f"codex exec failed with exit code {completed.returncode}")

    if not server.requests:
        raise SystemExit("Codex did not send a Responses API request")

    discovered: dict[str, set[str]] = {}
    observed_developer_texts: list[str] = []
    for request in server.requests:
        observed_developer_texts.extend(developer_texts(request))
        for namespace, tools in namespace_tools(request).items():
            discovered.setdefault(namespace, set()).update(tools)

    require_repo_skill_visible(observed_developer_texts, project_dir)

    hero_tools = discovered.get(EXPECTED_CODEX_NAMESPACE, set())
    missing = sorted(EXPECTED_CODEX_TOOLS - hero_tools)
    unexpected = sorted(hero_tools - EXPECTED_CODEX_TOOLS)
    if missing or unexpected:
        raise SystemExit(
            "Codex launched Hero Passport MCP but its model-visible tool surface was not exact. "
            f"missing={missing}; unexpected={unexpected}; observed={sorted(hero_tools)}; "
            f"namespaces={sorted(discovered)}"
        )

    print(
        "Codex host runtime smoke passed: "
        f"skill=hero-passport namespace={EXPECTED_CODEX_NAMESPACE} raw_tools={len(EXPECTED_TOOLS)} "
        f"model_visible_tools={len(hero_tools)} responses_requests={len(server.requests)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
