"""Dependency-free offline contract/error tests for TECH-03."""
import importlib.util
import json
import pathlib
import unittest
import urllib.error
import urllib.parse

FILE = pathlib.Path(__file__).resolve().parents[2] / "scripts/mapbox/mapbox_spike.py"
spec = importlib.util.spec_from_file_location("mapbox_spike", FILE)
spike = importlib.util.module_from_spec(spec)
import sys
sys.modules[spec.name] = spike
spec.loader.exec_module(spike)


class FakeResponse:
    def __init__(self, value):
        self.content = json.dumps(value).encode()
    def __enter__(self): return self
    def __exit__(self, *_): return None
    def read(self, _): return self.content


class MapboxSpikeTests(unittest.TestCase):
    def setUp(self):
        self.urls = []
        def opener(request, timeout):
            self.urls.append(request.full_url)
            return spike.offline_provider(request, timeout)
        self.client = spike.MapboxClient("SECRET_TEST_TOKEN", opener=opener)

    def test_geocoding_vietnam_country_and_coordinates(self):
        result = self.client.geocode("Ben Thanh Market")
        self.assertEqual(result["featureCount"], 1)
        self.assertEqual(result["features"][0]["longitude"], 106.700981)
        parsed = urllib.parse.urlparse(self.urls[-1])
        params = urllib.parse.parse_qs(parsed.query)
        self.assertEqual(parsed.path, "/search/geocode/v6/forward")
        self.assertEqual(params["country"], ["vn"])
        self.assertEqual(params["autocomplete"], ["false"])

    def test_geocoding_rejects_invalid_query_before_network(self):
        for q in ("", "a;b", "x" * 257):
            with self.subTest(q=q[:12]), self.assertRaises(ValueError):
                self.client.geocode(q)
        self.assertEqual(self.urls, [])

    def test_directions_lon_lat_and_units(self):
        self.assertEqual(self.client.directions(spike.ORIGIN, (106.6954, 10.7821)),
                         {"reachable": True, "durationSeconds": 120.0, "distanceMeters": 1000.0})
        self.assertIn("/106.700981,10.776530;106.695400,10.782100", self.urls[-1])

    def test_matrix_one_to_ten_destinations_not_n_by_n(self):
        destinations = [(106.7 + i / 10000, 10.7) for i in range(10)]
        result = self.client.matrix(spike.ORIGIN, destinations)
        self.assertEqual(result["destinationCount"], 10)
        self.assertEqual(len(result["items"]), 10)
        query = urllib.parse.parse_qs(urllib.parse.urlparse(self.urls[-1]).query)
        self.assertEqual(query["sources"], ["0"])
        self.assertEqual(query["destinations"], [";".join(map(str, range(1, 11)))])

    def test_traffic_matrix_10_coordinate_limit(self):
        with self.assertRaises(ValueError):
            self.client.matrix(spike.ORIGIN, [(106.7, 10.7)] * 10, profile="driving-traffic")

    def test_matrix_null_unreachable_preserves_order(self):
        def opener(*_, **__):
            return FakeResponse({"code": "Ok", "durations": [[2, None]],
                                 "distances": [[4, None]]})
        c = spike.MapboxClient("token", opener=opener)
        response = c.matrix(spike.ORIGIN, [(106.70, 10.77), (106.71, 10.78)])
        self.assertEqual([x["reachable"] for x in response["items"]], [True, False])

    def test_malformed_matrix_is_rejected(self):
        def opener(*_, **__):
            return FakeResponse({"code": "Ok", "durations": [[1]], "distances": [[2]]})
        with self.assertRaises(spike.ProviderError):
            spike.MapboxClient("token", opener=opener).matrix(spike.ORIGIN, [(106.7, 10.7)] * 2)

    def test_negative_values_rejected(self):
        def opener(*_, **__): return FakeResponse({"code": "Ok", "routes": [{"duration": -2, "distance": 4}]})
        with self.assertRaises(spike.ProviderError):
            spike.MapboxClient("token", opener=opener).directions(spike.ORIGIN, spike.ORIGIN)

    def test_no_route_not_confused_with_error(self):
        def opener(*_, **__): return FakeResponse({"code": "NoRoute"})
        response = spike.MapboxClient("token", opener=opener).directions(spike.ORIGIN, spike.ORIGIN)
        self.assertFalse(response["reachable"])

    def test_http_429_is_rate_limited_without_secret_leak(self):
        def opener(req, timeout):
            raise urllib.error.HTTPError(req.full_url, 429, "Too Many Requests", {}, None)
        c = spike.MapboxClient("SECRET_TOKEN", opener=opener)
        with self.assertRaises(spike.ProviderError) as ctx:
            c.geocode("District 1")
        self.assertEqual(ctx.exception.kind, "rate_limited")
        self.assertNotIn("SECRET_TOKEN", str(ctx.exception))

    def test_http_401_and_network_fail(self):
        def unauthorized(req, timeout):
            raise urllib.error.HTTPError(req.full_url, 401, "No", {}, None)
        with self.assertRaises(spike.ProviderError) as ctx:
            spike.MapboxClient("token", opener=unauthorized).geocode("District 1")
        self.assertEqual(ctx.exception.kind, "unauthorized")
        def network_error(*_, **__):
            raise urllib.error.URLError("provider offline")
        with self.assertRaises(spike.ProviderError) as ctx:
            spike.MapboxClient("token", opener=network_error).geocode("District 1")
        self.assertEqual(ctx.exception.kind, "network_or_timeout")

    def test_search_box_suggest_has_uuid_session_and_is_mock_only(self):
        result = self.client.search_box_suggest("Central Park", "5b4a9ec6-3333-4ff0-8d0f-c816cd037bac")
        self.assertEqual(result["suggestionCount"], 1)
        self.assertTrue(result["requiresRetrieve"])
        path = urllib.parse.urlparse(self.urls[-1])
        query = urllib.parse.parse_qs(path.query)
        self.assertEqual(path.path, "/search/searchbox/v1/suggest")
        self.assertEqual(query["country"], ["us"])
        self.assertIn("session_token", query)

    def test_search_box_requires_valid_session_token(self):
        with self.assertRaises(ValueError):
            self.client.search_box_suggest("Central Park", "bad-session")
        self.assertEqual(self.urls, [])

    def test_invalid_coordinates_rejected(self):
        with self.assertRaises(ValueError):
            self.client.directions((200, 10), spike.ORIGIN)

    def test_offline_summary_passes(self):
        c = spike.MapboxClient("offline", opener=spike.offline_provider)
        report = spike.run_spike(c, "MOCK")
        self.assertEqual(report["passed"], report["total"])
        self.assertEqual(report["total"], 4)
        self.assertNotIn("offline", json.dumps(report["cases"]))


if __name__ == "__main__":
    unittest.main()
