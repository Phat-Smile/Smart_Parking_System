import json
import re
from datetime import date
from pathlib import Path

SEED = json.loads((Path(__file__).parent.parent / "app" / "data" / "seed.json").read_text(encoding="utf-8"))


def test_every_seed_key_can_be_looked_up_through_the_api():
    # The API only accepts exactly 12 digits, so any other key is unreachable dead data.
    assert SEED, "seed must not be empty"
    assert all(re.fullmatch(r"\d{12}", key) for key in SEED)


def test_every_record_is_complete_and_well_formed():
    for key, record in SEED.items():
        assert record["fullName"].strip(), key
        assert record["gender"] in {"M", "F"}, key
        date.fromisoformat(record["dob"])


def test_the_dotnet_stub_test_numbers_are_present():
    assert {"000000000001", "000000000002", "000000000003", "000000000004"} <= set(SEED)
