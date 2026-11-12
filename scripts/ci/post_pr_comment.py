"""Create or update the single ParkWise CI comment on a GitHub pull request.

Runs from the trusted develop checkout (never from the PR workspace) because it
receives a GitHub token in GH_TOKEN. The report text is treated as data only.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path
from typing import Callable

MARKER = "<!-- parkwise-ci -->"
MAX_REPORT_CHARS = 9000
API = "https://api.github.com"

Request = Callable[[str, str, dict | None], object]


def parse_repo(url: str) -> tuple[str, str]:
    match = re.search(r"github\.com[:/]+([^/]+)/([^/]+?)(?:\.git)?/?$", url.strip())
    if not match:
        raise ValueError(f"not a GitHub repository URL: {url}")
    return match.group(1), match.group(2)


def neutralize_mentions(text: str) -> str:
    """Stop CI output from notifying people: put a zero-width space after every @."""
    return text.replace("@", "@" + chr(0x200B))


def build_body(status: str, report: str, build_url: str, sha: str) -> str:
    short = sha[:8] if sha else "unknown"
    if status == "SUCCESS":
        head = f"**ParkWise CI: passed** for `{short}`."
        detail = ""
    else:
        head = f"**ParkWise CI: {status.lower()}** for `{short}`."
        text = neutralize_mentions(report.strip())
        if len(text) > MAX_REPORT_CHARS:
            text = text[:MAX_REPORT_CHARS] + "\n\n... (cắt bớt, xem failure-report.md trong artifacts)"
        detail = "\n\n" + text if text else ""
    link = f"\n\nBuild: {build_url}" if build_url else ""
    return f"{MARKER}\n{head}{detail}{link}\n"


def find_existing(comments: list[dict]) -> int | None:
    for comment in comments:
        if MARKER in (comment.get("body") or ""):
            return int(comment["id"])
    return None


def upsert(
    request: Request, owner: str, repo: str, pr: int, status: str, body: str
) -> str:
    """Return what was done: created, updated or skipped."""
    comments = request("GET", f"/repos/{owner}/{repo}/issues/{pr}/comments?per_page=100", None)
    existing = find_existing(comments if isinstance(comments, list) else [])
    if existing is not None:
        request("PATCH", f"/repos/{owner}/{repo}/issues/comments/{existing}", {"body": body})
        return "updated"
    if status == "SUCCESS":
        return "skipped"
    request("POST", f"/repos/{owner}/{repo}/issues/{pr}/comments", {"body": body})
    return "created"


def http_request(token: str) -> Request:
    def call(method: str, path: str, payload: dict | None) -> object:
        data = json.dumps(payload).encode("utf-8") if payload is not None else None
        req = urllib.request.Request(API + path, data=data, method=method)
        req.add_header("Authorization", f"Bearer {token}")
        req.add_header("Accept", "application/vnd.github+json")
        req.add_header("User-Agent", "parkwise-ci")
        if data is not None:
            req.add_header("Content-Type", "application/json")
        with urllib.request.urlopen(req, timeout=20) as response:
            raw = response.read().decode("utf-8")
        return json.loads(raw) if raw else None

    return call


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-url", required=True)
    parser.add_argument("--pr", required=True, type=int)
    parser.add_argument("--status", required=True, help="SUCCESS, FAILURE, UNSTABLE, ABORTED")
    parser.add_argument("--build-url", default="")
    parser.add_argument("--sha", default="")
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()

    token = os.environ.get("GH_TOKEN", "")
    if not token:
        print("post_pr_comment: GH_TOKEN is not set, skipping", file=sys.stderr)
        return 0

    report = ""
    if args.report and args.report.exists():
        report = args.report.read_text(encoding="utf-8", errors="replace")

    try:
        owner, repo = parse_repo(args.repo_url)
        body = build_body(args.status, report, args.build_url, args.sha)
        action = upsert(http_request(token), owner, repo, args.pr, args.status, body)
    except (ValueError, urllib.error.URLError, OSError) as exc:
        # A comment failure must never fail the build.
        print(f"post_pr_comment: {type(exc).__name__}: {exc}", file=sys.stderr)
        return 0
    print(f"post_pr_comment: {action}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
