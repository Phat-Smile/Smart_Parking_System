"""Scan git diff for sensitive credentials and secrets."""

import os
import re
import subprocess
import sys

SECRET_PATTERNS = [
    (
        "Private Key",
        re.compile(r"-----BEGIN (?:RSA|EC|DSA|OPENSSH|PGP) PRIVATE KEY-----"),
    ),
    (
        "GitHub Token",
        re.compile(r"(?:ghp_[0-9a-zA-Z]{36}|github_pat_[0-9a-zA-Z_]{82})"),
    ),
    (
        "AWS Access Key ID",
        re.compile(r"(?<![A-Z0-9])(A3T[A-Z0-9]|AKIA|AGPA|AIDA|AROA|AIPA|ANPA|ANVA|ASIA)[A-Z0-9]{16}(?![A-Z0-9])"),
    ),
    (
        "Slack Token",
        re.compile(r"xox[baprs]-[0-9a-zA-Z]{10,48}"),
    ),
    (
        "Hardcoded Password in Connection String",
        re.compile(
            r"(?:Password|pwd)\s*=\s*(?!(\$\{.*\}|\*+|password|secret|changeme|postgres|admin|<.*>|123456))[^\s;'\"]{8,}",
            re.IGNORECASE,
        ),
    ),
    (
        "Hardcoded VNPay Secret Key",
        re.compile(
            r"(?:vnp_HashSecret|vnpay_hash_secret|vnpay:hashsecret)\s*[:=]\s*[\"'](?!(demo|sandbox|secret|\$\{.*\}|<.*>))[0-9a-zA-Z]{24,64}[\"']",
            re.IGNORECASE,
        ),
    ),
]

EXCLUDED_EXTENSIONS = (
    ".lock",
    ".json",
    ".Designer.cs",
    ".trx",
    ".xml",
    ".md",
    ".txt",
    ".svg",
    ".bak",
)

EXCLUDED_PATHS = (
    "package-lock.json",
    ".git/",
    "docs/",
    "tests/",
    "scripts/database/",
    "scripts/ci/test_",
    "scratch/",
)


def is_excluded_path(path: str) -> bool:
    normalized = path.replace("\\", "/")
    if any(normalized.endswith(ext) for ext in EXCLUDED_EXTENSIONS):
        return True
    if any(excluded in normalized for excluded in EXCLUDED_PATHS):
        return True
    return False


def git(*arguments: str) -> str:
    return subprocess.check_output(
        ["git", *arguments], text=True, encoding="utf-8", errors="replace"
    ).strip()


def scan_diff(diff_output: str) -> list[str]:
    """Inspect added lines in a unified git diff."""
    errors: list[str] = []
    current_file = ""
    current_line = 0

    for line in diff_output.splitlines():
        if line.startswith("+++ b/"):
            current_file = line[6:]
            continue

        if line.startswith("@@"):
            # Parse line number from chunk header: @@ -a,b +c,d @@
            match = re.search(r"\+(\d+)", line)
            if match:
                current_line = int(match.group(1)) - 1
            continue

        if line.startswith("+") and not line.startswith("+++"):
            current_line += 1
            if not current_file or is_excluded_path(current_file):
                continue

            content = line[1:].strip()
            for name, pattern in SECRET_PATTERNS:
                if pattern.search(content):
                    errors.append(
                        f"{current_file}:{current_line}: potential {name} found"
                    )
        elif not line.startswith("-"):
            current_line += 1

    return errors


def main() -> int:
    base_sha = os.environ.get("POLICY_BASE_SHA")
    head_sha = os.environ.get("POLICY_HEAD_SHA") or "HEAD"

    if len(sys.argv) > 1:
        diff_target = sys.argv[1]
        try:
            diff_output = git("diff", "-U0", diff_target)
        except subprocess.CalledProcessError as exc:
            print(f"Secret Scanner: git diff failed: {exc}", file=sys.stderr)
            return 1
    elif base_sha:
        try:
            diff_output = git("diff", "-U0", f"{base_sha}..{head_sha}")
        except subprocess.CalledProcessError as exc:
            print(f"Secret Scanner: git diff failed: {exc}", file=sys.stderr)
            return 1
    else:
        # Fallback to local unstaged/staged diff
        try:
            diff_output = git("diff", "-U0", "HEAD")
        except subprocess.CalledProcessError:
            diff_output = ""

    errors = scan_diff(diff_output)
    if errors:
        for err in errors:
            print(f"Secret Scanner VIOLATION: {err}", file=sys.stderr)
        return 1

    print("Secret Scanner: no sensitive credentials detected in diff")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
