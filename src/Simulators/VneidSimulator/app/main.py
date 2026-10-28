from fastapi import FastAPI

from app.api.cccd import router as cccd_router
from app.api.health import router as health_router

app = FastAPI(title="VNeID Simulator", version="1.0.0")
app.include_router(health_router)
app.include_router(cccd_router)
