"""Validate Git naming for pull requests targeting develop."""

import datetime as dt
import os
import re
import subprocess
import sys


BRANCH_PATTERNS = (
    re.compile(r"features/(?:Implementation|Design)_[A-Z][A-Za-z0-9]*\Z"),
    re.compile(r"hotfix/Bug_[A-Z][A-Za-z0-9]*\Z"),
    re.compile(r"release/sprint_[1-9][0-9]*\Z"),
)
TYPES = "feat|fix|refactor|test|docs|ci|chore|build|perf"
CONVENTIONAL = re.compile(
    rf"(?P<type>{TYPES})(?:\((?P<scope>[a-z][a-z0-9-]*)\))?!?: (?P<description>.+)\Z"
)
VAGUE_DESCRIPTIONS = {"update", "fix", "done", "final", "changes", "test", "login", "ui"}


def valid_branch(branch: str) -> bool:
    return any(pattern.fullmatch(branch) for pattern in BRANCH_PATTERNS)


def valid_message(message: str, *, require_scope: bool) -> bool:
    match = CONVENTIONAL.fullmatch(message.strip())
    if not match or (require_scope and not match.group("scope")):
        return False
    description = match.group("description").strip()
    words = description.split()
    return (
        len(words) >= 2
        and len(description) >= 8
        and words[0].lower() not in VAGUE_DESCRIPTIONS
        and description[0].islower()
    )


def git(*arguments: str) -> str:
    return subprocess.check_output(
        ["git", *arguments], text=True, encoding="utf-8"
    ).strip()


def new_commit_errors(base: str, head: str, cutover_epoch: int) -> list[str]:
    errors: list[str] = []
    for line in git("rev-list", "--reverse", "--parents", f"{base}..{head}").splitlines():
        commit, *parents = line.split()
        committed_at = int(git("show", "-s", "--format=%ct", commit))
        if committed_at < cutover_epoch:
            continue
        subject = git("show", "-s", "--format=%s", commit)
        if len(parents) > 1:
            errors.append(f"{commit[:12]}: merge commits are not allowed on new feature work")
        elif not valid_message(subject, require_scope=False):
            errors.append(f"{commit[:12]}: invalid commit message: {subject}")
    return errors


def main() -> int:
    branch = os.environ["POLICY_HEAD_BRANCH"]
    title = os.environ["POLICY_PR_TITLE"]
    base = os.environ["POLICY_BASE_SHA"]
    head = os.environ["POLICY_HEAD_SHA"]
    cutover = dt.datetime.fromisoformat(
        os.environ["POLICY_CUTOVER_UTC"].replace("Z", "+00:00")
    )
    errors: list[str] = []
    if not valid_branch(branch):
        errors.append(f"invalid source branch: {branch}")
    if not valid_message(title, require_scope=True):
        errors.append(f"invalid PR title: {title}")
    errors.extend(new_commit_errors(base, head, int(cutover.timestamp())))
    for error in errors:
        print(f"Git Policy: {error}", file=sys.stderr)
    if errors:
        return 1
    print("Git Policy: branch, PR title, and new commit messages pass")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
