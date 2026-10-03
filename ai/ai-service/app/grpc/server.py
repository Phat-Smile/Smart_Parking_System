import asyncio

import grpc
from grpc_health.v1 import health, health_pb2, health_pb2_grpc
from grpc_reflection.v1alpha import reflection


async def start_server(
    host: str = "0.0.0.0",
    port: int = 50051,
    *,
    enable_reflection: bool = False,
) -> tuple[grpc.aio.Server, health.aio.HealthServicer, int]:
    server = grpc.aio.server()
    health_servicer = health.aio.HealthServicer()
    health_pb2_grpc.add_HealthServicer_to_server(health_servicer, server)

    if enable_reflection:
        reflection.enable_server_reflection(
            (health.SERVICE_NAME, reflection.SERVICE_NAME),
            server,
        )

    bound_port = server.add_insecure_port(f"{host}:{port}")
    if bound_port == 0:
        raise RuntimeError(f"Could not bind the gRPC listener to {host}:{port}.")

    await health_servicer.set("", health_pb2.HealthCheckResponse.SERVING)
    await health_servicer.set(health.SERVICE_NAME, health_pb2.HealthCheckResponse.SERVING)
    await server.start()
    return server, health_servicer, bound_port


async def serve(
    host: str = "0.0.0.0",
    port: int = 50051,
    *,
    enable_reflection: bool = False,
) -> None:
    server, health_servicer, _ = await start_server(host, port, enable_reflection=enable_reflection)
    try:
        await server.wait_for_termination()
    finally:
        await health_servicer.enter_graceful_shutdown()
        await server.stop(grace=5)


if __name__ == "__main__":
    asyncio.run(serve())
