import unittest

import grpc
from grpc_health.v1 import health, health_pb2, health_pb2_grpc
from grpc_reflection.v1alpha import reflection_pb2, reflection_pb2_grpc

from app.grpc.server import start_server


class GrpcHealthTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self) -> None:
        self.server, self.health_servicer, port = await start_server(
            host="127.0.0.1",
            port=0,
            enable_reflection=True,
        )
        self.channel = grpc.aio.insecure_channel(f"127.0.0.1:{port}")

    async def asyncTearDown(self) -> None:
        await self.channel.close()
        await self.health_servicer.enter_graceful_shutdown()
        await self.server.stop(grace=0)

    async def test_standard_health_check_reports_serving(self) -> None:
        response = await health_pb2_grpc.HealthStub(self.channel).Check(
            health_pb2.HealthCheckRequest(service=""),
            timeout=2,
        )

        self.assertEqual(health_pb2.HealthCheckResponse.SERVING, response.status)

    async def test_development_reflection_lists_only_technical_services(self) -> None:
        async def requests():
            yield reflection_pb2.ServerReflectionRequest(list_services="")

        stub = reflection_pb2_grpc.ServerReflectionStub(self.channel)
        responses = stub.ServerReflectionInfo(requests())
        response = await responses.read()
        names = {service.name for service in response.list_services_response.service}

        self.assertEqual(
            {
                health.SERVICE_NAME,
                "grpc.reflection.v1alpha.ServerReflection",
            },
            names,
        )

    async def test_reflection_is_absent_when_disabled(self) -> None:
        server, health_servicer, port = await start_server(
            host="127.0.0.1",
            port=0,
            enable_reflection=False,
        )
        channel = grpc.aio.insecure_channel(f"127.0.0.1:{port}")

        async def requests():
            yield reflection_pb2.ServerReflectionRequest(list_services="")

        try:
            call = reflection_pb2_grpc.ServerReflectionStub(channel).ServerReflectionInfo(requests())
            with self.assertRaises(grpc.aio.AioRpcError) as error:
                await call.read()
            self.assertEqual(grpc.StatusCode.UNIMPLEMENTED, error.exception.code())
        finally:
            await channel.close()
            await health_servicer.enter_graceful_shutdown()
            await server.stop(grace=0)


if __name__ == "__main__":
    unittest.main()
