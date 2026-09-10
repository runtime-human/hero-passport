#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import subprocess
import threading
from dataclasses import dataclass
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any

NAMESPACE = "mcp__hero_passport"
MODEL = "hero-passport-qualification-model"

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


@dataclass(frozen=True)
class ToolCall:
    call_id: str
    model_name: str
    arguments: dict[str, Any]


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


def call_events(response_id: str, call: ToolCall) -> list[dict[str, Any]]:
    return [
        {"type": "response.created", "response": {"id": response_id}},
        {
            "type": "response.output_item.done",
            "item": {
                "type": "function_call",
                "call_id": call.call_id,
                "namespace": NAMESPACE,
                "name": call.model_name,
                "arguments": json.dumps(call.arguments, separators=(",", ":")),
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


def has_function_call_output(payload: dict[str, Any], call_id: str) -> bool:
    inputs = payload.get("input")
    if not isinstance(inputs, list):
        return False
    return any(
        isinstance(item, dict)
        and item.get("type") == "function_call_output"
        and item.get("call_id") == call_id
        for item in inputs
    )


class SequenceServer(ThreadingHTTPServer):
    def __init__(self, calls: list[ToolCall], marker: str) -> None:
        if not calls:
            raise ValueError("at least one tool call is required")
        super().__init__(("127.0.0.1", 0), SequenceHandler)
        self.calls = calls
        self.marker = marker
        self.lock = threading.Lock()
        self.requests = 0
        self.next_index = 0
        self.awaiting_call_id: str | None = None
        self.completed = False

    @property
    def base_url(self) -> str:
        host, port = self.server_address
        return f"http://{host}:{port}/v1"

    def events_for(self, payload: dict[str, Any]) -> list[dict[str, Any]]:
        with self.lock:
            self.requests += 1
            response_id = f"resp-hero-passport-lifecycle-{self.requests}"

            if self.completed:
                raise RuntimeError("unexpected Responses request after lifecycle sequence completed")

            if self.awaiting_call_id is not None:
                if not has_function_call_output(payload, self.awaiting_call_id):
                    raise RuntimeError(
                        f"Codex did not return MCP function_call_output for {self.awaiting_call_id}"
                    )
                self.awaiting_call_id = None

            if self.next_index < len(self.calls):
                call = self.calls[self.next_index]
                self.next_index += 1
                self.awaiting_call_id = call.call_id
                return call_events(response_id, call)

            self.completed = True
            return message_events(response_id, self.marker)

    def verify_complete(self) -> None:
        with self.lock:
            if not self.completed or self.awaiting_call_id is not None or self.next_index != len(self.calls):
                raise RuntimeError(
                    "Codex lifecycle provider sequence incomplete: "
                    f"completed={self.completed} awaiting={self.awaiting_call_id} "
                    f"next={self.next_index}/{len(self.calls)} requests={self.requests}"
                )


class SequenceHandler(BaseHTTPRequestHandler):
    server: SequenceServer

    def log_message(self, _format: str, *_args: object) -> None:
        return None

    def do_GET(self) -> None:
        if self.path.endswith("/v1/models") or self.path.endswith("/models"):
            self._send_json(
                {
                    "object": "list",
                    "data": [
                        {
                            "id": MODEL,
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
            if not isinstance(payload, dict):
                raise ValueError("expected Responses request object")
            events = self.server.events_for(payload)
        except (UnicodeDecodeError, json.JSONDecodeError, ValueError, RuntimeError) as exc:
            print(f"Codex lifecycle provider error: {exc}", file=os.sys.stderr, flush=True)
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


def run_sequence(
    codex: Path,
    project_dir: Path,
    calls: list[ToolCall],
    marker: str,
    prompt: str,
) -> str:
    server = SequenceServer(calls, marker)
    thread = threading.Thread(target=server.serve_forever, name="lifecycle-responses", daemon=True)
    thread.start()
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
    try:
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
        server.verify_complete()
        return completed.stdout
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=2)


def tool_results(stdout: str, tool_name: str) -> list[dict[str, Any]]:
    results: list[dict[str, Any]] = []
    for line in stdout.splitlines():
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        if not isinstance(event, dict) or event.get("type") != "item.completed":
            continue
        item = event.get("item")
        if (
            not isinstance(item, dict)
            or item.get("type") != "mcp_tool_call"
            or item.get("tool") != tool_name
        ):
            continue
        if item.get("status") != "completed" or item.get("error") is not None:
            raise RuntimeError(f"Codex reported failed MCP call {tool_name}: {item}")
        result = item.get("result")
        if not isinstance(result, dict):
            raise RuntimeError(f"Codex MCP call {tool_name} did not publish a result object: {item}")
        structured = result.get("structured_content")
        if not isinstance(structured, dict):
            raise RuntimeError(
                f"Codex MCP call {tool_name} did not publish structured_content: {result}"
            )
        results.append(structured)
    return results


def single_tool_result(stdout: str, tool_name: str) -> dict[str, Any]:
    results = tool_results(stdout, tool_name)
    if len(results) != 1:
        raise RuntimeError(f"expected exactly one completed {tool_name} call; observed={len(results)}")
    return results[0]


def require_equal(obj: dict[str, Any], key: str, expected: Any) -> None:
    actual = obj.get(key)
    if actual != expected:
        raise RuntimeError(f"expected {key}={expected!r}; actual={actual!r}; object={obj!r}")


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
        raise SystemExit("HERO_PASSPORT_HOME must be stable across Codex lifecycle restarts")

    try:
        bootstrap_stdout = run_sequence(
            codex,
            project_dir,
            [ToolCall("call-lifecycle-bootstrap", "hero_bootstrap", BOOTSTRAP_ARGS)],
            "HERO_PASSPORT_LIFECYCLE_BOOTSTRAP",
            "Bootstrap the deterministic Hero Passport qualification state.",
        )
        bootstrap = single_tool_result(bootstrap_stdout, "hero.bootstrap")
        require_equal(bootstrap, "replayed", False)
        hero = bootstrap.get("hero")
        if not isinstance(hero, dict) or not isinstance(hero.get("heroId"), str):
            raise RuntimeError(f"bootstrap did not return a Hero identity: {bootstrap}")
        hero_id = hero["heroId"]

        start_stdout = run_sequence(
            codex,
            project_dir,
            [ToolCall("call-lifecycle-start", "hero_start_quest", start_args(hero_id))],
            "HERO_PASSPORT_LIFECYCLE_START",
            "Start the deterministic Hero Passport qualification quest.",
        )
        start = single_tool_result(start_stdout, "hero.start_quest")
        require_equal(start, "replayed", False)
        quest = start.get("quest")
        if not isinstance(quest, dict) or not isinstance(quest.get("questId"), str):
            raise RuntimeError(f"start did not return a Quest identity: {start}")
        quest_id = quest["questId"]

        finish_stdout = run_sequence(
            codex,
            project_dir,
            [ToolCall("call-lifecycle-finish", "hero_finish_quest", finish_args(quest_id))],
            PHASE1_MARKER,
            "Finish the deterministic Hero Passport qualification quest before restart.",
        )
        finish = single_tool_result(finish_stdout, "hero.finish_quest")
        require_equal(finish, "replayed", False)
        require_equal(finish, "alreadyFinalized", False)
        reward = finish.get("reward")
        if not isinstance(reward, dict):
            raise RuntimeError(f"finish did not return reward projection: {finish}")
        require_equal(reward, "xpGained", 85)

        replay_stdout = run_sequence(
            codex,
            project_dir,
            [
                ToolCall("call-lifecycle-context", "hero_get_context", {}),
                ToolCall("call-lifecycle-card", "hero_get_card", {"heroId": hero_id}),
                ToolCall(
                    "call-lifecycle-start-replay",
                    "hero_start_quest",
                    start_args(hero_id),
                ),
                ToolCall(
                    "call-lifecycle-finish-replay",
                    "hero_finish_quest",
                    finish_args(quest_id),
                ),
            ],
            REPLAY_MARKER,
            "Recover the persisted Hero Passport state after restart, inspect it, and replay the original request identities.",
        )

        context = single_tool_result(replay_stdout, "hero.get_context")
        require_equal(context, "setupCompleted", True)
        active_hero = context.get("activeHero")
        if not isinstance(active_hero, dict):
            raise RuntimeError(f"restarted context did not contain active Hero: {context}")
        require_equal(active_hero, "heroId", hero_id)
        project = context.get("project")
        if not isinstance(project, dict):
            raise RuntimeError(f"restarted context did not contain project binding: {context}")
        require_equal(project, "displayName", project_dir.name)
        require_equal(context, "openQuests", [])

        card = single_tool_result(replay_stdout, "hero.get_card")
        card_hero = card.get("hero")
        if not isinstance(card_hero, dict):
            raise RuntimeError(f"card did not contain Hero projection: {card}")
        require_equal(card_hero, "heroId", hero_id)
        require_equal(card_hero, "totalXp", 85)

        start_replay = single_tool_result(replay_stdout, "hero.start_quest")
        require_equal(start_replay, "replayed", True)
        replay_quest = start_replay.get("quest")
        if not isinstance(replay_quest, dict):
            raise RuntimeError(f"Start replay did not contain Quest projection: {start_replay}")
        require_equal(replay_quest, "questId", quest_id)

        finish_replay = single_tool_result(replay_stdout, "hero.finish_quest")
        require_equal(finish_replay, "replayed", True)
        replay_reward = finish_replay.get("reward")
        if not isinstance(replay_reward, dict):
            raise RuntimeError(f"Finish replay did not contain reward projection: {finish_replay}")
        require_equal(replay_reward, "xpGained", 85)
    except RuntimeError as exc:
        raise SystemExit(str(exc)) from exc

    print(
        "Codex host lifecycle restart smoke passed: "
        f"hero={hero_id} quest={quest_id} persistent_home={Path(hero_home).name} "
        "host_processes=4 replayed=true"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
