import asyncio
import json
import unittest
import uuid
from typing import Any

from fastapi import FastAPI

from app.http_foundation import CorrelationAndRequestLoggingMiddleware, StructuredJsonFormatter


def make_scope(path: str = "/health/live", headers: list[tuple[bytes, bytes]] | None = None) -> dict[str, Any]:
    return {
        "type": "http",
        "asgi": {"version": "3.0"},
        "http_version": "1.1",
        "method": "GET",
        "scheme": "http",
        "path": path,
        "raw_path": path.encode("ascii"),
        "query_string": b"",
        "root_path": "",
        "headers": headers or [],
        "server": ("testserver", 80),
        "client": ("127.0.0.1", 12345),
    }


async def invoke(application: Any, scope: dict[str, Any]) -> list[dict[str, Any]]:
    messages: list[dict[str, Any]] = []

    async def receive() -> dict[str, Any]:
        return {"type": "http.request", "body": b"", "more_body": False}

    async def send(message: dict[str, Any]) -> None:
        messages.append(message)

    await application(scope, receive, send)
    return messages


class HttpFoundationTests(unittest.TestCase):
    def test_request_logs_route_template_without_identifiers_query_or_authorization(self) -> None:
        async def run() -> None:
            app = FastAPI()
            app.add_middleware(CorrelationAndRequestLoggingMiddleware)

            @app.get("/privacy/{identifier}")
            async def private_route(identifier: str) -> dict[str, str]:
                return {"ok": "true"}

            route_value = "SENSITIVE_ROUTE_VALUE_12345"
            query_value = "SENSITIVE_QUERY_VALUE_12345"
            bearer_value = "SENSITIVE_AUTH_VALUE_12345"
            matched = make_scope(
                path=f"/privacy/{route_value}",
                headers=[(b"authorization", f"Bearer {bearer_value}".encode("ascii"))],
            )
            matched["query_string"] = f"query={query_value}".encode("ascii")
            unmatched = make_scope(path=f"/missing/{route_value}")
            unmatched["query_string"] = f"query={query_value}".encode("ascii")

            with self.assertLogs("parking.ai.http", level="INFO") as captured:
                matched_response = await invoke(app, matched)
                unmatched_response = await invoke(app, unmatched)

            self.assertEqual(200, matched_response[0]["status"])
            self.assertEqual(404, unmatched_response[0]["status"])
            self.assertEqual(2, len(captured.records))
            self.assertEqual("/privacy/{identifier}", captured.records[0].path)
            self.assertEqual("<unmatched>", captured.records[1].path)
            for record in captured.records:
                event = StructuredJsonFormatter().format(record)
                self.assertNotIn(route_value, event)
                self.assertNotIn(query_value, event)
                self.assertNotIn(bearer_value, event)
                self.assertEqual("GET", record.method)
                self.assertEqual(200 if record is captured.records[0] else 404, record.status_code)
                self.assertTrue(record.correlation_id)
                self.assertTrue(hasattr(record, "trace_id"))
                self.assertTrue(hasattr(record, "span_id"))

        asyncio.run(run())

    def test_ai_unknown_route_uses_safe_problem_details(self) -> None:
        async def run() -> None:
            from app.main import app

            trace_id = "4bf92f3577b34da6a3ce929d0e0e4736"
            correlation_id = "CF04B675-8A30-4C1A-AB95-0B7B130FD6D7"
            response = await invoke(
                app,
                make_scope(
                    path="/not-configured",
                    headers=[
                        (b"x-correlation-id", correlation_id.encode("ascii")),
                        (b"traceparent", f"00-{trace_id}-00f067aa0ba902b7-01".encode("ascii")),
                    ],
                ),
            )
            headers = dict(response[0]["headers"])
            body = json.loads(response[1]["body"])

            self.assertEqual(404, response[0]["status"])
            self.assertEqual(b"application/problem+json", headers[b"content-type"])
            self.assertEqual("CF04B675-8A30-4C1A-AB95-0B7B130FD6D7", headers[b"x-correlation-id"].decode("ascii"))
            self.assertEqual("http.request_failed", body["errorCode"])
            self.assertEqual(trace_id, body["traceId"])
            self.assertEqual(correlation_id, body["correlationId"])
            self.assertNotEqual(uuid.UUID(correlation_id).hex, body["traceId"])
            self.assertNotIn("traceback", json.dumps(body).lower())

        asyncio.run(run())

    def test_concurrent_requests_keep_w3c_and_correlation_contexts_isolated(self) -> None:
        async def run() -> None:
            from app.main import app

            cases = [
                ("4bf92f3577b34da6a3ce929d0e0e4736", "CF04B675-8A30-4C1A-AB95-0B7B130FD6D7"),
                ("00f067aa0ba902b7a6ce929d0e0e4736", "4B60265B-C216-4DD8-9F07-3E7A1BBA24B3"),
            ]

            async def send(trace_id: str, correlation_id: str) -> dict[str, Any]:
                response = await invoke(
                    app,
                    make_scope(
                        path="/not-configured",
                        headers=[
                            (b"x-correlation-id", correlation_id.encode("ascii")),
                            (b"traceparent", f"00-{trace_id}-00f067aa0ba902b7-01".encode("ascii")),
                        ],
                    ),
                )
                return json.loads(response[1]["body"])

            first, second = await asyncio.gather(*(send(*case) for case in cases))
            self.assertEqual(cases[0][0], first["traceId"])
            self.assertEqual(cases[0][1], first["correlationId"])
            self.assertEqual(cases[1][0], second["traceId"])
            self.assertEqual(cases[1][1], second["correlationId"])

        asyncio.run(run())

    def test_missing_malformed_and_oversized_ids_generate_safe_guid_headers(self) -> None:
        async def run() -> None:
            async def downstream(scope: dict[str, Any], receive: Any, send: Any) -> None:
                await send({"type": "http.response.start", "status": 200, "headers": []})
                await send({"type": "http.response.body", "body": b"ok"})

            app = CorrelationAndRequestLoggingMiddleware(downstream)
            cases = [
                [],
                [(b"x-correlation-id", b"not-a-guid")],
                [(b"x-correlation-id", b"a" * 129)],
                [(b"x-correlation-id", b"invalid-one"), (b"x-correlation-id", b"invalid-two")],
            ]
            for headers in cases:
                response = await invoke(app, make_scope(headers=headers))
                response_headers = dict(response[0]["headers"])
                correlation_id = response_headers[b"x-correlation-id"].decode("ascii")
                self.assertNotEqual("00000000-0000-0000-0000-000000000000", correlation_id)
                uuid.UUID(correlation_id)

        asyncio.run(run())

    def test_valid_id_is_preserved(self) -> None:
        async def run() -> None:
            async def downstream(scope: dict[str, Any], receive: Any, send: Any) -> None:
                await send({"type": "http.response.start", "status": 200, "headers": []})
                await send({"type": "http.response.body", "body": b"ok"})

            correlation_id = "CF04B675-8A30-4C1A-AB95-0B7B130FD6D7"
            response = await invoke(
                CorrelationAndRequestLoggingMiddleware(downstream),
                make_scope(headers=[(b"X-Correlation-ID", correlation_id.encode("ascii"))]),
            )
            response_headers = dict(response[0]["headers"])
            self.assertEqual(correlation_id, response_headers[b"x-correlation-id"].decode("ascii"))

        asyncio.run(run())

    def test_unhandled_error_returns_safe_problem_details_and_never_reraises(self) -> None:
        async def run() -> None:
            async def failing_downstream(scope: dict[str, Any], receive: Any, send: Any) -> None:
                raise RuntimeError("private-password-value")

            response = await invoke(
                CorrelationAndRequestLoggingMiddleware(failing_downstream),
                make_scope(headers=[(b"x-correlation-id", b"4B60265B-C216-4DD8-9F07-3E7A1BBA24B3")]),
            )
            response_headers = dict(response[0]["headers"])
            body = json.loads(response[1]["body"])

            self.assertEqual(500, response[0]["status"])
            self.assertEqual(b"application/problem+json", response_headers[b"content-type"])
            self.assertEqual("An unexpected error occurred.", body["title"])
            self.assertEqual("4B60265B-C216-4DD8-9F07-3E7A1BBA24B3", body["correlationId"])
            self.assertEqual("internal.unexpected", body["errorCode"])
            self.assertNotIn("private-password-value", json.dumps(body))

        asyncio.run(run())


if __name__ == "__main__":
    unittest.main()
