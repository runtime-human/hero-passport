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


def nested_namespace_tools(payload: dict[str, Any]) -> tuple[set[str], set[str]]:
    names: set[str] = set()
    namespaces: set[str] = set()
    tools = payload.get("tools")
    if not isinstance(tools, list):
        return names, namespaces

    for spec in tools:
        if not isinstance(spec, dict) or spec.get("type") != "namespace":
            continue
        namespace = spec.get("name")
        if isinstance(namespace, str):
            namespaces.add(namespace)
        nested = spec.get("tools")
        if not isinstance(nested, list):
            continue
        for tool in nested:
            if not isinstance(tool, dict) or tool.get("type") != "function":
                continue
            name = tool.get("name")
            if isinstance(name, str):
                names.add(name)

    return names, namespaces


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
        "stream_max_retries = 0 }}"
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

    discovered: set[str] = set()
    namespaces: set[str] = set()
    for request in server.requests:
        request_tools, request_namespaces = nested_namespace_tools(request)
        discovered.update(request_tools)
        namespaces.update(request_namespaces)

    missing = sorted(EXPECTED_TOOLS - discovered)
    if missing:
        observed = sorted(discovered)
        raise SystemExit(
            "Codex launched the turn but did not expose the complete Hero Passport MCP tool set. "
            f"missing={missing}; observed={observed}; namespaces={sorted(namespaces)}"
        )

    if "hero-passport" not in namespaces:
        raise SystemExit(
            "Hero Passport tools were not exposed through the configured hero-passport namespace; "
            f"observed namespaces={sorted(namespaces)}"
        )

    print(
        "Codex host runtime smoke passed: "
        f"namespace=hero-passport tools={len(EXPECTED_TOOLS)} responses_requests={len(server.requests)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
