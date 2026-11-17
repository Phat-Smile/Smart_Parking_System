"""Server-side, OpenAI-compatible Chat Completions adapter.

No provider credentials, URLs or arbitrary prompts are accepted from callers.
The adapter is deliberately small so tests can replace it without network access.
"""

import json
import os
import re
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen


class LlmProviderError(Exception):
    """Safe-to-handle provider failure (never includes secrets or response bodies)."""


class OpenAICompatibleChatProvider:
    def choose(self, system_prompt: str, data: dict) -> dict:
        key = os.getenv("AI_LLM_API_KEY", "").strip()
        model = os.getenv("AI_LLM_MODEL", "").strip()
        url = os.getenv(
            "AI_LLM_CHAT_COMPLETIONS_URL",
            "https://api.openai.com/v1/chat/completions",
        ).strip()
        parts = urlsplit(url)
        if not key or not model:
            raise LlmProviderError("provider_not_configured")
        if parts.scheme != "https" or not parts.hostname or parts.username or parts.password or parts.fragment:
            raise LlmProviderError("provider_url_invalid")

        try:
            timeout = float(os.getenv("AI_LLM_TIMEOUT_SECONDS", "3"))
            max_tokens = int(os.getenv("AI_LLM_MAX_OUTPUT_TOKENS", "400"))
        except ValueError as exc:
            raise LlmProviderError("provider_config_invalid") from exc
        if not 0.1 <= timeout <= 15 or not 64 <= max_tokens <= 2000:
            raise LlmProviderError("provider_config_invalid")

        # Some provider gateways filter urllib's default User-Agent. Keep the
        # app identity by default; allow an explicit server-side QA override.
        user_agent = os.getenv("AI_LLM_USER_AGENT", "ParkWise-AI/1.0")
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9 ._+/-]{0,127}", user_agent):
            raise LlmProviderError("provider_config_invalid")

        payload = {
            "model": model,
            "temperature": 0,
            "max_tokens": max_tokens,
            "response_format": {"type": "json_object"},
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": json.dumps(data, ensure_ascii=False, separators=(",", ":"))},
            ],
        }
        req = Request(
            url=url,
            data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
            headers={
                "Authorization": f"Bearer {key}",
                "Content-Type": "application/json",
                "User-Agent": user_agent,
            },
            method="POST",
        )
        try:
            with urlopen(req, timeout=timeout) as response:
                raw = response.read(32769)
        except (HTTPError, URLError, TimeoutError, OSError) as exc:
            raise LlmProviderError("provider_unavailable") from exc
        if len(raw) > 32768:
            raise LlmProviderError("provider_response_too_large")
        try:
            body = json.loads(raw)
            choice = body["choices"][0]
            if choice.get("finish_reason") != "stop":
                raise LlmProviderError("provider_response_incomplete")
            content = choice["message"]["content"]
            if not isinstance(content, str):
                raise ValueError("content must be a string")
            result = json.loads(content)
            if not isinstance(result, dict):
                raise ValueError("content must be an object")
            return result
        except (KeyError, IndexError, TypeError, ValueError) as exc:
            raise LlmProviderError("provider_response_invalid") from exc
