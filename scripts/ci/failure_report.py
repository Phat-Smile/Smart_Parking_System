"""Summarize a failed ParkWise PR CI run from the ci-results directory.

Reads the step logs written by the pipeline (ci-results/logs/<name>.log plus
<name>.status with the exit code) and every JUnit XML report, then writes a short
Markdown report and a plain-text summary for the Jenkins build description.
The description is plain text on purpose: Jenkins escapes HTML in it by default.
"""

from __future__ import annotations

import argparse
import re
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")

LABELS = {
    "git-policy": "Git Policy",
    "secret-scan": "Secret Scan",
    "migration-drift": "Migration Drift",
    "backend-restore": "Backend Restore",
    "backend-build": "Backend Build",
    "frontend-install": "Frontend Dependencies",
    "frontend-lint": "Frontend Lint",
    "frontend-typecheck": "Frontend Typecheck",
    "frontend-format": "Frontend Format",
    "frontend-vitest": "Frontend Vitest",
    "frontend-build": "Frontend Build",
    "ai-deps": "Python AI Dependencies",
    "ai-compile": "Python AI Compile",
    "ai-unittest": "Python AI Tests",
    "iot-simulator": "IoT Simulator Tests",
    "docker-build-dotnet": "Docker Build (.NET services)",
    "docker-build-web-ai": "Docker Build (Web and AI)",
}

MAX_HIGHLIGHTS = 8


@dataclass(frozen=True)
class FailedStep:
    name: str
    label: str
    exit_code: int
    tail: str
    # The lines that explain the failure: lint errors with file and line, files prettier would change,
    # TypeScript errors, policy violations. Empty when the log has no recognizable pattern.
    highlights: list[str] = field(default_factory=list)


@dataclass(frozen=True)
class FailedCase:
    suite: str
    name: str
    message: str


def label_for(log_name: str) -> str:
    if log_name in LABELS:
        return LABELS[log_name]
    parts = log_name.split("-")
    if parts[0] == "dotnet" and len(parts) >= 3:
        return f"{parts[2].title()} {parts[1].title()} Tests"
    return log_name


def clean_lines(text: str) -> list[str]:
    lines = [ANSI.sub("", line).rstrip() for line in text.splitlines()]
    return [line for line in lines if line.strip()]


def tail_lines(path: Path, count: int) -> str:
    text = path.read_text(encoding="utf-8", errors="replace")
    return "\n".join(clean_lines(text)[-count:])


ESLINT_PROBLEM = re.compile(r"^\s+(\d+):(\d+)\s+error\s+(.*)$")
ESLINT_FILE = re.compile(r"^(?:[A-Za-z]:)?[\\/].+\.(?:tsx?|jsx?|mjs|cjs)$")
PRETTIER_WARN = re.compile(r"^\[warn\]\s+(\S.*)$")
TS_ERROR = re.compile(r"^(.+?\(\d+,\d+\): error TS\d+: .*)$")


def _short_path(path: str) -> str:
    normalized = path.replace("\\", "/")
    marker = "/web/"
    index = normalized.rfind(marker)
    return normalized[index + len(marker) :] if index != -1 else normalized


def extract_highlights(name: str, lines: list[str]) -> list[str]:
    """Pull the lines that say what is wrong out of a step log."""
    found: list[str] = []
    if name == "frontend-lint":
        current = ""
        for line in lines:
            if ESLINT_FILE.match(line.strip()):
                current = _short_path(line.strip())
                continue
            match = ESLINT_PROBLEM.match(line)
            if match:
                found.append(f"{current}:{match.group(1)}:{match.group(2)} {match.group(3)[:160]}")
    elif name == "frontend-format":
        files = [m.group(1) for line in lines if (m := PRETTIER_WARN.match(line))]
        files = [f for f in files if "Code style issues" not in f and "Run Prettier" not in f]
        if files:
            found.append(f"{len(files)} file cần prettier --write: " + ", ".join(files[:3]))
            if len(files) > 3:
                found[-1] += f" (và {len(files) - 3} file khác, xem Console Output)"
    elif name == "frontend-typecheck":
        found = [m.group(1) for line in lines if (m := TS_ERROR.match(line))]
    elif name in ("git-policy", "secret-scan"):
        found = [line for line in lines if "Git Policy:" in line or "VIOLATION" in line]
    elif name == "migration-drift":
        found = [line for line in lines if "FAILED" in line or "drift" in line.lower()]
    return [line[:240] for line in found[:MAX_HIGHLIGHTS]]


def failed_steps(root: Path, tail: int = 25) -> list[FailedStep]:
    logs = root / "logs"
    steps: list[FailedStep] = []
    if not logs.is_dir():
        return steps
    for status_file in sorted(logs.glob("*.status")):
        raw = status_file.read_text(encoding="utf-8", errors="replace").strip()
        if not raw.lstrip("-").isdigit() or int(raw) == 0:
            continue
        name = status_file.stem
        log_file = logs / f"{name}.log"
        if log_file.exists():
            lines = clean_lines(log_file.read_text(encoding="utf-8", errors="replace"))
            text = "\n".join(lines[-tail:])
            highlights = extract_highlights(name, lines)
        else:
            text, highlights = "(không có log)", []
        steps.append(FailedStep(name, label_for(name), int(raw), text, highlights))
    return steps


def failed_cases(root: Path, limit: int = 30) -> list[FailedCase]:
    cases: list[FailedCase] = []
    for report in sorted(root.rglob("*.xml")):
        try:
            tree = ET.parse(report)
        except ET.ParseError:
            continue
        for case in tree.iter("testcase"):
            problem = case.find("failure")
            if problem is None:
                problem = case.find("error")
            if problem is None:
                continue
            message = (problem.get("message") or problem.text or "").strip()
            message = " ".join(ANSI.sub("", message).split())[:300]
            cases.append(
                FailedCase(case.get("classname") or "", case.get("name") or "", message)
            )
            if len(cases) >= limit:
                return cases
    return cases


RETRY = re.compile(r"\(retry x(\d+)\)")
PASS_MARK = re.compile(r"^\s*[✓✔√]\s*")
TIMING = re.compile(r"\s+\d+(?:\.\d+)?\s*m?s(?:\s.*)?$")


def flaky_tests(root: Path, limit: int = 20) -> list[str]:
    """Tests that failed once and passed on retry, read from the verbose vitest log."""
    log = root / "logs" / "frontend-vitest.log"
    if not log.exists():
        return []
    found: list[str] = []
    for raw in log.read_text(encoding="utf-8", errors="replace").splitlines():
        line = ANSI.sub("", raw)
        if not RETRY.search(line):
            continue
        name = TIMING.sub("", PASS_MARK.sub("", line)).strip()
        if name and name not in found:
            found.append(name)
        if len(found) >= limit:
            break
    return found


def build_markdown(
    steps: list[FailedStep], cases: list[FailedCase], flaky: list[str] | None = None
) -> str:
    flaky = flaky or []
    out = ["# ParkWise PR CI: báo cáo", ""]
    if flaky:
        out += ["## Test chập chờn (đã tự thử lại và đạt)", ""]
        out += [f"- `{name}`" for name in flaky]
        out.append("")
    if not steps and not cases:
        if flaky:
            return "\n".join(out)
        out += [
            "Không tìm thấy log lỗi trong ci-results.",
            "Lỗi nhiều khả năng xảy ra trước các bước kiểm tra (Verify Pull Request, "
            "Checkout PR) hoặc ở bước chưa ghi log. Xem Console Output.",
            "",
        ]
        return "\n".join(out)

    out += ["## Bước thất bại", ""]
    for step in steps:
        out += [f"### {step.label} (exit {step.exit_code})", ""]
        if step.highlights:
            out += [f"- {line}" for line in step.highlights] + [""]
        out += ["```", step.tail, "```", ""]
    if cases:
        out += ["## Test thất bại", ""]
        for case in cases:
            out.append(f"- `{case.suite}` / `{case.name}`: {case.message}")
        out.append("")
    return "\n".join(out)


def build_text(
    steps: list[FailedStep], cases: list[FailedCase], flaky: list[str] | None = None
) -> str:
    """Plain text for the Jenkins build description (newlines become line breaks there)."""
    flaky = flaky or []
    if not steps and not cases:
        if flaky:
            return "Test chập chờn (đã thử lại và đạt):\n" + "\n".join(flaky[:5])
        return "FAILED trước các bước kiểm tra. Xem Console Output."
    lines = ["FAILED"]
    for step in steps:
        lines.append(f"Bước lỗi: {step.label}")
        lines += [f"  {item}" for item in step.highlights[:5]]
    for case in cases[:5]:
        lines.append(f"Test lỗi: {case.suite} / {case.name} ({case.message[:120]})")
    if len(cases) > 5:
        lines.append(f"... và {len(cases) - 5} test lỗi khác (xem failure-report.md)")
    if flaky:
        lines.append("Test chập chờn: " + ", ".join(flaky[:3]))
    lines.append("Chi tiết đầy đủ: Console Output và artifact failure-report.md")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path, help="ci-results directory")
    parser.add_argument("--output", type=Path, required=True, help="Markdown report path")
    parser.add_argument("--text", type=Path, help="plain-text summary path for the build description")
    parser.add_argument(
        "--only-if-findings",
        action="store_true",
        help="write nothing when there are no failed steps, failed tests or flaky tests",
    )
    args = parser.parse_args()

    steps = failed_steps(args.root)
    cases = failed_cases(args.root)
    flaky = flaky_tests(args.root)
    if args.only_if_findings and not (steps or cases or flaky):
        return 0
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(build_markdown(steps, cases, flaky), encoding="utf-8")
    if args.text:
        args.text.write_text(build_text(steps, cases, flaky), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
