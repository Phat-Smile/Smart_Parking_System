from __future__ import annotations

import os
from typing import Any

from opentelemetry import metrics, trace
from opentelemetry.exporter.otlp.proto.grpc.metric_exporter import OTLPMetricExporter
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.grpc import GrpcAioInstrumentorServer
from opentelemetry.instrumentation.system_metrics import SystemMetricsInstrumentor
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
from opentelemetry.sdk.resources import Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.sdk.trace.sampling import (
    ALWAYS_OFF,
    ALWAYS_ON,
    ParentBased,
    TraceIdRatioBased,
)

_EXCLUDED_GRPC_METHOD_PREFIXES = (
    "/grpc.health.v1.Health/",
    "/grpc.reflection.",
)


def should_trace_grpc_method(handler_call_details: Any) -> bool:
    """Skip technical health/reflection RPCs while tracing application RPCs."""
    method = getattr(handler_call_details, "method", None)
    return not isinstance(method, str) or not method.startswith(_EXCLUDED_GRPC_METHOD_PREFIXES)


def _resource_attributes() -> dict[str, str]:
    attributes = {
        "service.name": os.getenv("OTEL_SERVICE_NAME", "ai-service"),
        "service.namespace": "ParkingSystem",
        "service.version": os.getenv("OTEL_SERVICE_VERSION", "0.1.0"),
        "deployment.environment": os.getenv("APP_ENVIRONMENT", "Production"),
    }
    for item in os.getenv("OTEL_RESOURCE_ATTRIBUTES", "").split(","):
        key, separator, value = item.partition("=")
        if separator and key.strip() and value.strip():
            attributes[key.strip()] = value.strip()
    return attributes


def _sampler() -> Any:
    sampler_name = os.getenv("OTEL_TRACES_SAMPLER", "parentbased_always_on").casefold()
    try:
        ratio = min(max(float(os.getenv("OTEL_TRACES_SAMPLER_ARG", "1.0")), 0.0), 1.0)
    except ValueError:
        ratio = 1.0

    if sampler_name == "always_off":
        return ALWAYS_OFF
    if sampler_name == "always_on":
        return ALWAYS_ON
    if sampler_name == "traceidratio":
        return TraceIdRatioBased(ratio)
    if sampler_name == "parentbased_always_off":
        return ParentBased(root=ALWAYS_OFF)
    if sampler_name == "parentbased_traceidratio":
        return ParentBased(root=TraceIdRatioBased(ratio))
    return ParentBased(root=ALWAYS_ON)


def current_trace_ids() -> tuple[str, str]:
    span_context = trace.get_current_span().get_span_context()
    if not span_context.is_valid:
        return "", ""
    return f"{span_context.trace_id:032x}", f"{span_context.span_id:016x}"


def _sanitize_server_span(span: Any, scope: dict[str, Any]) -> None:
    # Route templates and status/method are retained by the instrumentation.
    # Raw path/query values can contain user identifiers or secrets.
    for attribute in ("url.full", "url.path", "url.query", "http.url", "http.target"):
        span.set_attribute(attribute, "")


def configure_ai_telemetry(app: Any) -> tuple[TracerProvider, MeterProvider]:
    resource = Resource.create(_resource_attributes())
    tracer_provider = TracerProvider(resource=resource, sampler=_sampler())
    meter_readers = []
    if os.getenv("OTEL_EXPORTER_OTLP_ENDPOINT", "").strip():
        tracer_provider.add_span_processor(BatchSpanProcessor(OTLPSpanExporter()))
        meter_readers.append(
            PeriodicExportingMetricReader(OTLPMetricExporter(), export_interval_millis=10_000)
        )

    meter_provider = MeterProvider(resource=resource, metric_readers=meter_readers)
    trace.set_tracer_provider(tracer_provider)
    metrics.set_meter_provider(meter_provider)

    FastAPIInstrumentor.instrument_app(
        app,
        tracer_provider=tracer_provider,
        meter_provider=meter_provider,
        # FastAPI instrumentation matches exclusions against the full request URL,
        # not just the path component.
        excluded_urls=r".*/health/(?:live|ready)(?:\?.*)?$",
        server_request_hook=_sanitize_server_span,
    )
    SystemMetricsInstrumentor().instrument(meter_provider=meter_provider)
    GrpcAioInstrumentorServer(filter_=should_trace_grpc_method).instrument()
    return tracer_provider, meter_provider
