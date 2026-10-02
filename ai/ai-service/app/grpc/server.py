import asyncio

import grpc


async def serve(host: str = "0.0.0.0", port: int = 50051) -> None:
    server = grpc.aio.server()
    server.add_insecure_port(f"{host}:{port}")
    await server.start()
    try:
        await server.wait_for_termination()
    finally:
        await server.stop(grace=5)


if __name__ == "__main__":
    asyncio.run(serve())
