import json
import logging
import sys
import time
import uuid
from collections import defaultdict
from typing import Any

from opentelemetry import trace


CORRELATION_HEADER = b"x-correlation-id"


class StructuredJsonFormatter(logging.Formatter):
    def format(self, record: logging.LogRecord) -> str:
        event: dict[str, Any] = {
            "timestamp": self.formatTime(record, datefmt="%Y-%m-%dT%H:%M:%S%z"),
            "level": record.levelname,
            "logger": record.name,
            "message": record.getMessage(),
        }
        for name in ("method", "path", "status_code", "duration_ms", "trace_id", "span_id", "correlation_id", "exception_type"):
            if hasattr(record, name):
                event[name] = getattr(record, name)
        return json.dumps(event, separators=(",", ":"), ensure_ascii=True)


def _http_logger() -> logging.Logger:
    logger = logging.getLogger("parking.ai.http")
    if not logger.handlers:
        handler = logging.StreamHandler(sys.stdout)
        handler.setFormatter(StructuredJsonFormatter())
        logger.addHandler(handler)
    logger.setLevel(logging.INFO)
    logger.propagate = False
    return logger


def _resolve_correlation_id(headers: list[tuple[bytes, bytes]]) -> str:
    values = [value for name, value in headers if name.lower() == CORRELATION_HEADER]
    if len(values) == 1:
        try:
            value = values[0].decode("ascii")
            parsed = uuid.UUID(value)
            if value and len(value) <= 128 and value == value.strip() and parsed.int != 0:
                return value
        except (UnicodeDecodeError, ValueError, AttributeError):
            pass
    return str(uuid.uuid4())


class CorrelationAndRequestLoggingMiddleware:
    def __init__(self, app: Any) -> None:
        self.app = app
        self.logger = _http_logger()

    async def __call__(self, scope: dict[str, Any], receive: Any, send: Any) -> None:
        if scope.get("type") != "http":
            await self.app(scope, receive, send)
            return

        correlation_id = _resolve_correlation_id(scope.get("headers", []))
        span_context = trace.get_current_span().get_span_context()
        trace_id = f"{span_context.trace_id:032x}" if span_context.is_valid else ""
        span_id = f"{span_context.span_id:016x}" if span_context.is_valid else ""
        state = dict(scope.get("state", {}))
        state["correlation_id"] = correlation_id
        state["trace_id"] = trace_id
        state["span_id"] = span_id
        scope["state"] = state
        headers = [(name, value) for name, value in scope.get("headers", []) if name.lower() != CORRELATION_HEADER]
        headers.append((CORRELATION_HEADER, correlation_id.encode("ascii")))
        scope["headers"] = headers

        started_at = time.perf_counter()
        status_code = 500
        response_started = False

        async def send_with_correlation(message: dict[str, Any]) -> None:
            nonlocal status_code, response_started
            if message.get("type") == "http.response.start":
                response_started = True
                status_code = int(message.get("status", 500))
                response_headers = [
                    (name, value)
                    for name, value in message.get("headers", [])
                    if name.lower() != CORRELATION_HEADER
                ]
                response_headers.append((CORRELATION_HEADER, correlation_id.encode("ascii")))
                message["headers"] = response_headers
            await send(message)

        method = scope.get("method", "")
        path = scope.get("path", "/")
        try:
            await self.app(scope, receive, send_with_correlation)
        except Exception as exception:
            self.logger.error(
                "Unhandled HTTP request exception",
                extra={"exception_type": type(exception).__name__, "trace_id": trace_id, "span_id": span_id, "correlation_id": correlation_id},
            )
            if not response_started:
                status_code = 500
                body = json.dumps(
                    {
                        "type": "about:blank",
                        "title": "An unexpected error occurred.",
                        "status": 500,
                        "detail": "An unexpected error occurred. Please try again later.",
                        "instance": path,
                        "traceId": trace_id,
                        "correlationId": correlation_id,
                        "errorCode": "internal.unexpected",
                    },
                    separators=(",", ":"),
                ).encode("utf-8")
                await send_with_correlation(
                    {
                        "type": "http.response.start",
                        "status": 500,
                        "headers": [(b"content-type", b"application/problem+json")],
                    }
                )
                await send_with_correlation(
                    {"type": "http.response.body", "body": body, "more_body": False}
                )
        finally:
            duration_ms = (time.perf_counter() - started_at) * 1000
            log_level = (
                logging.ERROR
                if status_code >= 500
                else logging.INFO
                if status_code >= 400
                else logging.DEBUG
                if path in {"/health/live", "/health/ready"}
                else logging.INFO
            )
            self.logger.log(
                log_level,
                "HTTP request completed",
                extra={
                    "method": method,
                    "path": path,
                    "status_code": status_code,
                    "duration_ms": round(duration_ms, 3),
                    "trace_id": trace_id,
                    "span_id": span_id,
                    "correlation_id": correlation_id,
                },
            )


def validation_problem_details(request: Any, errors: list[Any]) -> dict[str, Any]:
    grouped: dict[str, list[str]] = defaultdict(list)
    for error in errors:
        location = error.get("loc", ())
        field = ".".join(str(part) for part in location if part != "body") or "_"
        grouped[field].append("The value is invalid.")
    return {
        "type": "about:blank",
        "title": "One or more validation errors occurred.",
        "status": 400,
        "detail": "Correct the invalid fields and retry.",
        "instance": request.url.path,
        "errors": dict(grouped),
        "traceId": getattr(request.state, "trace_id", ""),
        "correlationId": getattr(request.state, "correlation_id", ""),
        "errorCode": "request.validation_failed",
    }
