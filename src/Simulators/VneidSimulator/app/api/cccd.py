import json
import re
from pathlib import Path
from typing import Any

from fastapi import APIRouter
from fastapi.responses import JSONResponse
from pydantic import BaseModel

router = APIRouter(prefix="/vneid", tags=["cccd"])

_CCCD_PATTERN = re.compile(r"^\d{12}$")

_SEED: dict[str, Any] = json.loads(
    (Path(__file__).parent.parent / "data" / "seed.json").read_text(encoding="utf-8")
)


class _VerifyRequest(BaseModel):
    cccd: str


@router.post("/cccd/verify")
async def verify_cccd(body: _VerifyRequest) -> JSONResponse:
    if not _CCCD_PATTERN.match(body.cccd):
        return JSONResponse(
            status_code=400,
            content={
                "type": "about:blank",
                "title": "Invalid CCCD format.",
                "status": 400,
                "detail": "CCCD must be exactly 12 digits.",
                "errorCode": "cccd.invalid_format",
            },
            media_type="application/problem+json",
        )

    record = _SEED.get(body.cccd)
    if record is None:
        return JSONResponse(content={"valid": False})

    return JSONResponse(
        content={
            "valid": True,
            "fullName": record["fullName"],
            "dob": record["dob"],
            "gender": record["gender"],
        }
    )
