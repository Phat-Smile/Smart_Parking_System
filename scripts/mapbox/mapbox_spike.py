#!/usr/bin/env python3
"""TECH-03: reproducible Mapbox Geocoding/Directions/Matrix API spike.

Offline mode uses deterministic public mock responses. Live mode is opt-in and
requires MAPBOX_ACCESS_TOKEN; it can incur Mapbox usage charges.
No token, provider URL, precise user location or raw provider JSON is logged.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import math
import os
import pathlib
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
from typing import Callable, Any

BASE = "https://api.mapbox.com"
ORIGIN = (106.700981, 10.776530)  # static *demo* location, never user GPS
DESTINATIONS = [
    ("lot-b", 106.695400, 10.782100),
    ("lot-c", 106.705200, 10.770500),
    ("lot-a", 106.710000, 10.775000),
]


class ProviderError(Exception):
    def __init__(self, kind: str, status: int | None = None):
        self.kind = kind
        self.status = status
        super().__init__(kind)  # no secret URL or provider response in error


def _validate_point(point: tuple[float, float]) -> None:
    longitude, latitude = point
    if not all(isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value) for value in point):
        raise ValueError("Coordinates must be finite")
    if not (-180 <= longitude <= 180 and -90 <= latitude <= 90):
        raise ValueError("Longitude/latitude out of range")


def _positive_number(value: Any) -> float | None:
    if value is None:
        return None
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ProviderError("invalid_provider_payload")
    if not math.isfinite(value) or value < 0:
        raise ProviderError("invalid_provider_payload")
    return float(value)


class MapboxClient:
    def __init__(self, token: str, *, opener: Callable[..., Any] = urllib.request.urlopen,
                 timeout: float = 6.0, base_url: str = BASE):
        if not token.strip():
            raise ValueError("MAPBOX_ACCESS_TOKEN is required for live mode")
        if not base_url.startswith("https://"):
            raise ValueError("Only HTTPS Mapbox endpoints are permitted")
        if timeout <= 0:
            raise ValueError("Timeout must be positive")
        self.__token = token
        self.__opener = opener
        self.__timeout = timeout
        self.__base_url = base_url.rstrip("/")

    def _get(self, path: str, parameters: dict[str, str]) -> dict[str, Any]:
        if not path.startswith("/"):
            raise ValueError("Invalid provider path")
        query = urllib.parse.urlencode({**parameters, "access_token": self.__token}, safe=",;")
        request = urllib.request.Request(
            self.__base_url + path + "?" + query,
            headers={"Accept": "application/json", "User-Agent": "SmartParking-OJT-MapboxSpike/1.0"},
            method="GET",
        )
        try:
            with self.__opener(request, timeout=self.__timeout) as response:
                content = response.read(1024 * 1024 + 1)
                if len(content) > 1024 * 1024:
                    raise ProviderError("payload_too_large")
                data = json.loads(content)
        except urllib.error.HTTPError as exc:
            # Never expose exception string: it includes the URL with access token.
            kind = {401: "unauthorized", 403: "forbidden", 422: "invalid_request",
                    429: "rate_limited"}.get(exc.code, "provider_unavailable")
            raise ProviderError(kind, exc.code) from None
        except (urllib.error.URLError, TimeoutError, OSError):
            raise ProviderError("network_or_timeout") from None
        except (UnicodeError, json.JSONDecodeError):
            raise ProviderError("invalid_provider_payload") from None
        if not isinstance(data, dict):
            raise ProviderError("invalid_provider_payload")
        return data

    def geocode(self, query: str) -> dict[str, Any]:
        query = query.strip()
        if not query or len(query) > 256 or ";" in query or len(query.split()) > 20:
            raise ValueError("Geocoding input must contain 1-20 words, <=256 characters and no semicolon")
        data = self._get("/search/geocode/v6/forward", {
            "q": query, "country": "vn", "limit": "5", "autocomplete": "false", "language": "vi"
        })
        features = data.get("features")
        if not isinstance(features, list):
            raise ProviderError("invalid_provider_payload")
        results = []
        for feature in features[:5]:
            if not isinstance(feature, dict):
                raise ProviderError("invalid_provider_payload")
            geometry = feature.get("geometry")
            properties = feature.get("properties")
            if not isinstance(geometry, dict) or not isinstance(properties, dict):
                raise ProviderError("invalid_provider_payload")
            coords = geometry.get("coordinates")
            if not isinstance(coords, list) or len(coords) < 2:
                raise ProviderError("invalid_provider_payload")
            point = (coords[0], coords[1])
            try:
                _validate_point(point)
            except ValueError:
                raise ProviderError("invalid_provider_payload") from None
            results.append({"longitude": point[0], "latitude": point[1],
                            "featureType": properties.get("feature_type", "unknown")})
        return {"featureCount": len(results), "features": results}

    def search_box_suggest(self, query: str, session_token: str) -> dict[str, Any]:
        """Contract exploration ONLY: Search Box supported regions exclude Vietnam.

        Session token is caller-owned and must also be used by /retrieve in an
        actual search session. Do not use this method for Vietnam live traffic.
        """
        query = query.strip()
        if not query or len(query) > 256:
            raise ValueError("Search Box query must contain 1..256 characters")
        try:
            parsed = uuid.UUID(session_token)
        except (ValueError, AttributeError):
            raise ValueError("A UUIDv4 Search Box session_token is required") from None
        if parsed.version != 4:
            raise ValueError("A UUIDv4 Search Box session_token is required")
        data = self._get("/search/searchbox/v1/suggest", {
            "q": query, "session_token": session_token, "limit": "5", "country": "us"
        })
        suggestions = data.get("suggestions")
        if not isinstance(suggestions, list):
            raise ProviderError("invalid_provider_payload")
        for suggestion in suggestions:
            if not isinstance(suggestion, dict) or not isinstance(suggestion.get("mapbox_id"), str):
                raise ProviderError("invalid_provider_payload")
        return {"suggestionCount": len(suggestions), "requiresRetrieve": True,
                "note": "USA mock contract only; Search Box geography excludes Vietnam"}

    def directions(self, origin: tuple[float, float], destination: tuple[float, float]) -> dict[str, Any]:
        _validate_point(origin)
        _validate_point(destination)
        points = ";".join(f"{lng:.6f},{lat:.6f}" for lng, lat in (origin, destination))
        data = self._get(f"/directions/v5/mapbox/driving/{points}", {"overview": "false", "alternatives": "false"})
        if data.get("code") == "NoRoute":
            return {"reachable": False, "durationSeconds": None, "distanceMeters": None}
        if data.get("code") != "Ok":
            raise ProviderError("provider_failed")
        routes = data.get("routes")
        if not isinstance(routes, list) or not routes:
            raise ProviderError("invalid_provider_payload")
        if not isinstance(routes[0], dict):
            raise ProviderError("invalid_provider_payload")
        duration, distance = _positive_number(routes[0].get("duration")), _positive_number(routes[0].get("distance"))
        if duration is None or distance is None:
            raise ProviderError("invalid_provider_payload")
        return {"reachable": True, "durationSeconds": duration, "distanceMeters": distance}

    def matrix(self, origin: tuple[float, float], destinations: list[tuple[float, float]],
               *, profile: str = "driving") -> dict[str, Any]:
        if not 1 <= len(destinations) <= 10:
            raise ValueError("This S1 spike requires 1 to 10 destination lots")
        if profile not in ("driving", "driving-traffic"):
            raise ValueError("Unsupported profile")
        if profile == "driving-traffic" and len(destinations) + 1 > 10:
            raise ValueError("Traffic Matrix accepts at most 10 coordinates including origin")
        for point in [origin, *destinations]:
            _validate_point(point)
        points = ";".join(f"{lng:.6f},{lat:.6f}" for lng, lat in [origin, *destinations])
        indices = ";".join(str(i) for i in range(1, len(destinations) + 1))
        data = self._get(f"/directions-matrix/v1/mapbox/{profile}/{points}", {
            "sources": "0", "destinations": indices, "annotations": "duration,distance"
        })
        if data.get("code") != "Ok":
            raise ProviderError("provider_failed")
        duration_rows = data.get("durations")
        distance_rows = data.get("distances")
        n = len(destinations)
        if (not isinstance(duration_rows, list) or len(duration_rows) != 1
                or not isinstance(duration_rows[0], list) or len(duration_rows[0]) != n
                or not isinstance(distance_rows, list) or len(distance_rows) != 1
                or not isinstance(distance_rows[0], list) or len(distance_rows[0]) != n):
            raise ProviderError("invalid_provider_payload")
        rows = []
        for index in range(n):
            duration = _positive_number(duration_rows[0][index])
            distance = _positive_number(distance_rows[0][index])
            rows.append({"index": index, "reachable": duration is not None and distance is not None,
                         "durationSeconds": duration, "distanceMeters": distance})
        return {"profile": profile, "originCount": 1, "destinationCount": n, "items": rows}


class _OfflineResponse:
    def __init__(self, payload: dict[str, Any]):
        self.payload = json.dumps(payload).encode("utf-8")

    def __enter__(self):
        return self

    def __exit__(self, *_):
        return False

    def read(self, _: int):
        return self.payload


def offline_provider(request, timeout):
    """A deterministic provider stub; no actual network or pricing usage."""
    parsed = urllib.parse.urlparse(request.full_url)
    query = urllib.parse.parse_qs(parsed.query)
    if parsed.path == "/search/searchbox/v1/suggest":
        return _OfflineResponse({"suggestions": [{"mapbox_id": "mock-not-a-real-id", "name": "Demo Place"}]})
    if parsed.path == "/search/geocode/v6/forward":
        return _OfflineResponse({"features": [{"geometry": {"coordinates": [106.700981, 10.77653]},
                   "properties": {"feature_type": "place"}}]})
    if parsed.path.startswith("/directions-matrix/"):
        count = len(query["destinations"][0].split(";"))
        return _OfflineResponse({"code": "Ok", "durations": [[120.0 * (i + 1) for i in range(count)]],
                                 "distances": [[1000.0 * (i + 1) for i in range(count)]]})
    if parsed.path.startswith("/directions/"):
        return _OfflineResponse({"code": "Ok", "routes": [{"duration": 120.0, "distance": 1000.0}]})
    raise AssertionError("Unexpected API path")


def run_spike(client: MapboxClient, mode: str) -> dict[str, Any]:
    cases = []
    tests: list[tuple[str, Callable[[], Any]]] = [
        ("GEO-01", lambda: client.geocode("1 Le Duan, Ho Chi Minh City, Vietnam")),
        ("DIR-01", lambda: client.directions(ORIGIN, (DESTINATIONS[0][1], DESTINATIONS[0][2]))),
        ("MAT-01", lambda: client.matrix(ORIGIN, [(lng, lat) for _, lng, lat in DESTINATIONS])),
    ]
    if mode == "MOCK":
        tests.append(("BOX-01", lambda: client.search_box_suggest(
            "Central Park, New York", "5b4a9ec6-3333-4ff0-8d0f-c816cd037bac")))
    for name, test in tests:
        try:
            result = test()
            if name == "GEO-01" and result["featureCount"] == 0:
                raise ProviderError("no_vietnam_address_match")
            if name == "DIR-01" and not result["reachable"]:
                raise ProviderError("no_driving_route_for_static_demo_points")
            if name == "MAT-01" and not any(item["reachable"] for item in result["items"]):
                raise ProviderError("no_reachable_demo_lots")
            # No raw provider payload, token or exact query string in evidence.
            cases.append({"id": name, "status": "PASS", "result": result})
        except (ProviderError, ValueError) as exc:
            cases.append({"id": name, "status": "FAIL", "error": str(exc),
                          "httpStatus": getattr(exc, "status", None)})
    return {"task": "TECH-03", "mode": mode, "environment": "demo_static_coordinates",
            "runAtUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
            "note": "Mock PASS is NOT evidence of live provider availability or Vietnam geocoding quality. Search Box live skipped: Vietnam is outside advertised coverage.",
            "cases": cases,
            "passed": sum(item["status"] == "PASS" for item in cases),
            "total": len(cases)}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--live", action="store_true", help="Opt-in: make three billable Mapbox requests")
    parser.add_argument("--output", type=pathlib.Path, help="Optional JSON evidence file (never includes a token)")
    parser.add_argument("--timeout", type=float, default=6.0)
    args = parser.parse_args(argv)
    if args.live:
        token = os.environ.get("MAPBOX_ACCESS_TOKEN", "")
        if not token:
            print("Live test BLOCKED: MAPBOX_ACCESS_TOKEN is not configured.", file=sys.stderr)
            return 2
        client = MapboxClient(token, timeout=args.timeout)
    else:
        client = MapboxClient("offline-dummy", opener=offline_provider, timeout=args.timeout)
    report = run_spike(client, "LIVE" if args.live else "MOCK")
    formatted = json.dumps(report, ensure_ascii=False, indent=2)
    print(formatted)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(formatted + "\n", encoding="utf-8")
    return 0 if report["passed"] == report["total"] else 1


if __name__ == "__main__":
    sys.exit(main())
