from types import SimpleNamespace
import unittest

from opentelemetry import metrics, trace
from opentelemetry.instrumentation.asgi import OpenTelemetryMiddleware
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.trace import TracerProvider

from app.main import app, meter_provider, tracer_provider
from app.telemetry import should_trace_grpc_method


class TelemetryFilterTests(unittest.TestCase):
    def test_fastapi_auto_configuration_is_disabled_and_explicit_telemetry_is_registered_once(self) -> None:
        self.assertFalse(app._telemetry["auto_configure"])
        self.assertIsInstance(tracer_provider, TracerProvider)
        self.assertIsInstance(meter_provider, MeterProvider)
        self.assertIs(trace.get_tracer_provider(), tracer_provider)
        self.assertIs(metrics.get_meter_provider(), meter_provider)
        self.assertTrue(app._is_instrumented_by_opentelemetry)

        stack = app.build_middleware_stack()
        otel_middleware_count = 0
        current = stack
        while current is not None:
            if isinstance(current, OpenTelemetryMiddleware):
                otel_middleware_count += 1
            current = getattr(current, "app", None)

        self.assertEqual(otel_middleware_count, 1)

    def test_excludes_grpc_health_and_reflection_but_keeps_application_rpc(self) -> None:
        excluded = (
            "/grpc.health.v1.Health/Check",
            "/grpc.reflection.v1.ServerReflection/ServerReflectionInfo",
            "/grpc.reflection.v1alpha.ServerReflection/ServerReflectionInfo",
        )
        for method in excluded:
            with self.subTest(method=method):
                self.assertFalse(should_trace_grpc_method(SimpleNamespace(method=method)))

        self.assertTrue(
            should_trace_grpc_method(
                SimpleNamespace(method="/parking.v1.ParkingService/GetAvailability")
            )
        )
