import os
from contextlib import asynccontextmanager
from typing import AsyncIterator

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHTTPException

from app.api.health import router as health_router
from app.grpc.server import start_server
from app.http_foundation import CorrelationAndRequestLoggingMiddleware, validation_problem_details
from app.telemetry import configure_ai_telemetry


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    environment = os.getenv("APP_ENVIRONMENT", "Production")
    port = int(os.getenv("AI_GRPC_PORT", "50051"))
    server, health_servicer, _ = await start_server(
        port=port,
        enable_reflection=environment.casefold() == "development",
    )
    app.state.grpc_server = server
    try:
        yield
    finally:
        await health_servicer.enter_graceful_shutdown()
        await server.stop(grace=5)


app = FastAPI(
    title="Parking AI Service",
    lifespan=lifespan,
    telemetry={"auto_configure": False},
)
app.include_router(health_router)
app.add_middleware(CorrelationAndRequestLoggingMiddleware)
tracer_provider, meter_provider = configure_ai_telemetry(app)


@app.exception_handler(RequestValidationError)
async def handle_request_validation(request: Request, exception: RequestValidationError) -> JSONResponse:
    return JSONResponse(
        status_code=400,
        content=validation_problem_details(request, exception.errors()),
        media_type="application/problem+json",
    )


@app.exception_handler(StarletteHTTPException)
async def handle_http_error(request: Request, exception: StarletteHTTPException) -> JSONResponse:
    is_server_error = exception.status_code >= 500
    return JSONResponse(
        status_code=exception.status_code,
        content={
            "type": "about:blank",
            "title": "An unexpected error occurred." if is_server_error else "The request could not be completed.",
            "status": exception.status_code,
            "detail": "An unexpected error occurred. Please try again later."
            if is_server_error
            else "The request could not be processed.",
            "instance": request.url.path,
            "traceId": getattr(request.state, "trace_id", ""),
            "correlationId": getattr(request.state, "correlation_id", ""),
            "errorCode": "internal.unexpected" if is_server_error else "http.request_failed",
        },
        media_type="application/problem+json",
    )
