import unittest

import post_pr_comment as pc


class FakeGitHub:
    def __init__(self, comments: list[dict]) -> None:
        self.comments = comments
        self.calls: list[tuple[str, str, dict | None]] = []

    def __call__(self, method: str, path: str, payload: dict | None) -> object:
        self.calls.append((method, path, payload))
        return self.comments if method == "GET" else {}


class PostPrCommentTests(unittest.TestCase):
    def test_parses_https_and_ssh_repo_urls(self) -> None:
        self.assertEqual(
            pc.parse_repo("https://github.com/Phat-Smile/Smart_Parking_System.git"),
            ("Phat-Smile", "Smart_Parking_System"),
        )
        self.assertEqual(
            pc.parse_repo("git@github.com:Phat-Smile/Smart_Parking_System"),
            ("Phat-Smile", "Smart_Parking_System"),
        )
        with self.assertRaises(ValueError):
            pc.parse_repo("https://example.com/x/y")

    def test_failure_body_has_marker_report_and_neutralized_mentions(self) -> None:
        body = pc.build_body("FAILURE", "ping @someone", "http://ci/1", "0123456789abcdef")
        self.assertIn(pc.MARKER, body)
        self.assertIn("failure", body)
        self.assertIn("01234567", body)
        self.assertNotIn("@someone", body)
        self.assertIn("http://ci/1", body)

    def test_long_report_is_truncated(self) -> None:
        body = pc.build_body("FAILURE", "x" * 50000, "", "abc")
        self.assertLess(len(body), pc.MAX_REPORT_CHARS + 500)

    def test_failure_creates_a_comment_when_none_exists(self) -> None:
        github = FakeGitHub([{"id": 1, "body": "unrelated"}])
        action = pc.upsert(github, "o", "r", 7, "FAILURE", "body")
        self.assertEqual(action, "created")
        self.assertEqual(github.calls[-1][0], "POST")

    def test_failure_updates_the_existing_ci_comment(self) -> None:
        github = FakeGitHub([{"id": 9, "body": pc.MARKER + " old"}])
        action = pc.upsert(github, "o", "r", 7, "FAILURE", "new")
        self.assertEqual(action, "updated")
        method, path, payload = github.calls[-1]
        self.assertEqual((method, path), ("PATCH", "/repos/o/r/issues/comments/9"))
        self.assertEqual(payload, {"body": "new"})

    def test_success_updates_an_old_failure_comment_but_never_creates_one(self) -> None:
        with_old = FakeGitHub([{"id": 3, "body": pc.MARKER}])
        self.assertEqual(pc.upsert(with_old, "o", "r", 7, "SUCCESS", "ok"), "updated")
        without = FakeGitHub([])
        self.assertEqual(pc.upsert(without, "o", "r", 7, "SUCCESS", "ok"), "skipped")
        self.assertEqual([c[0] for c in without.calls], ["GET"])


if __name__ == "__main__":
    unittest.main()
