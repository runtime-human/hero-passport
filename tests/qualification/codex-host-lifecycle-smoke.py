#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import subprocess
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any, Iterable

NAMESPACE = "mcp__hero_passport"
MODEL = "hero-passport-qualification-model"

BOOTSTRAP_CALL_ID = "call-lifecycle-bootstrap"
START_CALL_ID = "call-lifecycle-start"
FINISH_CALL_ID = "call-lifecycle-finish"
CONTEXT_CALL_ID = "call-lifecycle-context"
CARD_CALL_ID = "call-lifecycle-card"
START_REPLAY_CALL_ID = "call-lifecycle-start-replay"
FINISH_REPLAY_CALL_ID = "call-lifecycle-finish-replay"

BOOTSTRAP_REQUEST_ID = "01994c0b-5f00-7000-8000-000000000001"
START_REQUEST_ID = "01994c0b-5f00-7000-8000-000000000002"
FINISH_REQUEST_ID = "01994c0b-5f00-7000-8000-000000000003"

PHASE1_MARKER = "HERO_PASSPORT_LIFECYCLE_PHASE1"
REPLAY_MARKER = "HERO_PASSPORT_LIFECYCLE_REPLAY"

BOOTSTRAP_ARGS = {
    "bootstrapRequestId": BOOTSTRAP_REQUEST_ID,
    "locale": "en-US",
    "heroName": "Codex Nova",
    "presentationStyle": "rpg_engineering",
    "autoStartQuest": True,
    "autoFinishQuest": True,
}


def start_args(hero_id: str) -> dict[str, Any]:
    return {
        "startRequestId": START_REQUEST_ID,
        "heroId": hero_id,
        "questType": "coding",
        "title": "Codex host lifecycle checkpoint",
        "goal": "Qualify Hero Passport lifecycle persistence across a real Codex host restart.",
    }


def finish_args(quest_id: str) -> dict[str, Any]:
    return {
        "finishRequestId": FINISH_REQUEST_ID,
        "questId": quest_id,
        "result": "success",
        "summary": "Codex completed the deterministic release qualification lifecycle before host restart.",
        "metrics": {
            "testsMentioned": False,
            "scopeViolations": 0,
            "userCorrections": 0,
            "buildStatus": "not_run",
            "buildEvidence": "none",
            "testsStatus": "not_run",
            "testsEvidence": "none",
        },
        "skillsUsed": ["coding"],
    }


def completed_event(response_id: str) -> dict[str, Any]:
    return {
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
    }


def function_call_event(response_id: str, call_id: str, name: str, arguments: dict[str, Any]) -> list[dict[str, Any]]:
    return [
        {"type": "response.created", "response": {"id": response_id}},
        {
            "type": "response.output_item.done",
            "item": {
                "type": "function_call",
                "call_id": call_id,
                "namespace": NAMESPACE,
                "name": name,
                "arguments": json.dumps(arguments, separators=(",", ":")),
            },
        },
        completed_event(response_id),
    ]


def message_events(response_id: str, marker: str) -> list[dict[str, Any]]:
    return [
        {"type": "response.created", "response": {"id": response_id}},
        {
            "type": "response.output_item.done",
            "item": {
                "type": "message",
                "role": "assistant",
                "id": f"msg-{marker.lower()}",
                "content": [{"type": "output_text", "text": marker}],
            },
        },
        completed_event(response_id),
    ]


def function_call_output(payload: dict[str, Any], call_id: str) -> tuple[bool, Any]:
    inputs = payload.get("input")
    if not isinstance(inputs, list):
        return False, None
    for item in inputs:
        if (
            isinstance(item, dict)
            and item.get("type") == "function_call_output"
            and item.get("call_id") == call_id
        ):
            return True, item.get("output")
    return False, None


def walk_json(value: Any, depth: int = 0) -> Iterable[Any]:
    if depth > 12:
        return
    yield value
    if isinstance(value, dict):
        for nested in value.values():
            yield from walk_json(nested, depth + 1)
    elif isinstance(value, list):
        for nested in value:
            yield from walk_json(nested, depth + 1)
    elif isinstance(value, str):
        stripped = value.strip()
        if not stripped or stripped[0] not in "{[\"-0123456789tfn":
            return
        try:
            decoded = json.loads(stripped)
        except json.JSONDecodeError:
            return
        if decoded != value:
            yield from walk_json(decoded, depth + 1)


def values_for_key(output: Any, key: str) -> list[Any]:
    values: list[Any] = []
    for node in walk_json(output):
        if isinstance(node, dict) and key in node:
            values.append(node[key])
    return values


def require_value(output: Any, key: str, expected: Any) -> None:
    values = values_for_key(output, key)
    if expected not in values:
        raise RuntimeError(f"expected {key}={expected!r}; observed={values!r}; output={output!r}")


def require_string(output: Any, key: str) -> str:
    values = [value for value in values_for_key(output, key) if isinstance(value, str) and value]
    if not values:
        raise RuntimeError(f"expected non-empty {key}; output={output!r}")
    return values[0]


def require_output(payload: dict[str, Any], call_id: str) -> Any:
    found, output = function_call_output(payload, call_id)
    if not found:
        raise RuntimeError(f"Codex did not return MCP output for {call_id}")
    return output


class CaptureServer(ThreadingHTTPServer):
    def __init__(self) -> None:
        super().__init__(("127.0.0.1", 0), CaptureHandler)
        self.lock = threading.Lock()
        self.requests: list[dict[str, Any]] = []
        self.stage = "phase1-bootstrap"
        self.hero_id: str | None = None
        self.quest_id: str | None = None

    @property
    def base_url(self) -> str:
        host, port = self.server_address
        return f"http://{host}:{port}/v1"

    def begin_replay(self) -> None:
        with self.lock:
            if self.stage != "phase1-done" or self.hero_id is None or self.quest_id is None:
                raise RuntimeError(
                    f"phase 1 did not complete before restart: stage={self.stage} hero={self.hero_id} quest={self.quest_id}"
                )
            self.stage = "replay-context"

    def require_replay_done(self) -> None:
        with self.lock:
            if self.stage != "replay-done":
                raise RuntimeError(f"replay lifecycle did not complete: stage={self.stage}")

    def events_for(self, payload: dict[str, Any]) -> list[dict[str, Any]]:
        with self.lock:
            self.requests.append(payload)
            response_id = f"resp-hero-passport-lifecycle-{len(self.requests)}"

            if self.stage == "phase1-bootstrap":
                self.stage = "phase1-bootstrap-output"
                return function_call_event(response_id, BOOTSTRAP_CALL_ID, "hero_bootstrap", BOOTSTRAP_ARGS)

            if self.stage == "phase1-bootstrap-output":
                output = require_output(payload, BOOTSTRAP_CALL_ID)
                require_value(output, "replayed", False)
                self.hero_id = require_string(output, "heroId")
                self.stage = "phase1-start-output"
                return function_call_event(response_id, START_CALL_ID, "hero_start_quest", start_args(self.hero_id))

            if self.stage == "phase1-start-output":
                output = require_output(payload, START_CALL_ID)
                require_value(output, "replayed", False)
                self.quest_id = require_string(output, "questId")
                self.stage = "phase1-finish-output"
                return function_call_event(response_id, FINISH_CALL_ID, "hero_finish_quest", finish_args(self.quest_id))

            if self.stage == "phase1-finish-output":
                output = require_output(payload, FINISH_CALL_ID)
                require_value(output, "replayed", False)
                require_value(output, "alreadyFinalized", False)
                require_value(output, "xpGained", 85)
                self.stage = "phase1-done"
                return message_events(response_id, PHASE1_MARKER)

            if self.stage == "replay-context":
                self.stage = "replay-context-output"
                return function_call_event(response_id, CONTEXT_CALL_ID, "hero_get_context", {})

            if self.stage == "replay-context-output":
                output = require_output(payload, CONTEXT_CALL_ID)
                require_value(output, "setupCompleted", True)
                require_value(output, "heroId", self.hero_id)
                require_value(output, "displayName", "project")
                require_value(output, "openQuests", [])
                self.stage = "replay-card-output"
                return function_call_event(response_id, CARD_CALL_ID, "hero_get_card", {"heroId": self.hero_id})

            if self.stage == "replay-card-output":
                output = require_output(payload, CARD_CALL_ID)
                require_value(output, "heroId", self.hero_id)
                require_value(output, "totalXp", 85)
                self.stage = "replay-start-output"
                return function_call_event(
                    response_id,
                    START_REPLAY_CALL_ID,
                    "hero_start_quest",
                    start_args(self.hero_id or ""),
                )

            if self.stage == "replay-start-output":
                output = require_output(payload, START_REPLAY_CALL_ID)
                require_value(output, "replayed", True)
                require_value(output, "questId", self.quest_id)
                self.stage = "replay-finish-output"
                return function_call_event(
                    response_id,
                    FINISH_REPLAY_CALL_ID,
                    "hero_finish_quest",
                    finish_args(self.quest_id or ""),
                )

            if self.stage == "replay-finish-output":
                output = require_output(payload, FINISH_REPLAY_CALL_ID)
                require_value(output, "replayed", True)
                require_value(output, "questId", self.quest_id)
                require_value(output, "xpGained", 85)
                self.stage = "replay-done"
                return message_events(response_id, REPLAY_MARKER)

            raise RuntimeError(f"unexpected Responses request after lifecycle completion: stage={self.stage}")


class CaptureHandler(BaseHTTPRequestHandler):
    server: CaptureServer

    def log_message(self, _format: str, *_args: object) -> None:
        return None

    def do_GET(self) -> None:
        if self.path.endswith("/v1/models") or self.path.endswith("/models"):
            self._send_json(
                {
                    "object": "list",
                    "data": [{"id": MODEL, "object": "model", "created": 0, "owned_by": "qualification"}],
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
            if not isinstance(payload, dict):
                raise ValueError("expected Responses request object")
            events = self.server.events_for(payload)
        except (UnicodeDecodeError, json.JSONDecodeError, ValueError, RuntimeError) as exc:
            self.send_error(500, f"lifecycle qualification failed: {exc}")
            return

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


def run_codex(codex: Path, project_dir: Path, server: CaptureServer, prompt: str, marker: str) -> None:
    provider = (
        "model_providers.hero_passport_qualification="
        f"{{ name = 'Hero Passport lifecycle qualification', base_url = '{server.base_url}', "
        "wire_api = 'responses', requires_openai_auth = false, request_max_retries = 0, "
        "stream_max_retries = 0 }"
    )
    command = [
        str(codex),
        "exec",
        "--ephemeral",
        "--json",
        "-m",
        MODEL,
        "-c",
        'model_provider="hero_passport_qualification"',
        "-c",
        "mcp_optional_startup_grace_ms=0",
        "-c",
        provider,
        prompt,
    ]
    completed = subprocess.run(
        command,
        cwd=project_dir,
        env=os.environ.copy(),
        text=True,
        capture_output=True,
        timeout=90,
        check=False,
    )
    if completed.stdout:
        print(completed.stdout, end="")
    if completed.stderr:
        print(completed.stderr, end="", file=os.sys.stderr)
    if completed.returncode != 0:
        raise RuntimeError(f"codex exec failed with exit code {completed.returncode}")
    if marker not in completed.stdout:
        raise RuntimeError(f"codex exec completed without lifecycle marker {marker}")


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
    hero_home = os.environ.get("HERO_PASSPORT_HOME")
    if not hero_home:
        raise SystemExit("HERO_PASSPORT_HOME must be stable across both Codex lifecycle phases")

    server = CaptureServer()
    thread = threading.Thread(target=server.serve_forever, name="lifecycle-responses", daemon=True)
    thread.start()
    try:
        run_codex(
            codex,
            project_dir,
            server,
            "Use Hero Passport to bootstrap Codex Nova, start the qualification quest, finish it, then return the requested marker.",
            PHASE1_MARKER,
        )
        server.begin_replay()
        run_codex(
            codex,
            project_dir,
            server,
            "Recover the persisted Hero Passport state after host restart, inspect the card, replay the same Start and Finish identities, then return the requested marker.",
            REPLAY_MARKER,
        )
        server.require_replay_done()
    except RuntimeError as exc:
        raise SystemExit(str(exc)) from exc
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=2)

    print(
        "Codex host lifecycle restart smoke passed: "
        f"hero={server.hero_id} quest={server.quest_id} persistent_home={Path(hero_home).name} "
        f"responses_requests={len(server.requests)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
