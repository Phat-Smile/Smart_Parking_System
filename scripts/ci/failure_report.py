"""Summarize a failed ParkWise PR CI run from the ci-results directory.

Reads the step logs written by the pipeline (ci-results/logs/<name>.log plus
<name>.status with the exit code) and every JUnit XML report, then writes a short
Markdown report and an HTML summary for the Jenkins build description.
"""

from __future__ import annotations

import argparse
import html
import re
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path

ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")

LABELS = {
    "git-policy": "Git Policy",
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
}


@dataclass(frozen=True)
class FailedStep:
    name: str
    label: str
    exit_code: int
    tail: str


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


def tail_lines(path: Path, count: int) -> str:
    text = path.read_text(encoding="utf-8", errors="replace")
    lines = [ANSI.sub("", line).rstrip() for line in text.splitlines()]
    lines = [line for line in lines if line.strip()]
    return "\n".join(lines[-count:])


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
        text = tail_lines(log_file, tail) if log_file.exists() else "(không có log)"
        steps.append(FailedStep(name, label_for(name), int(raw), text))
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


def build_markdown(steps: list[FailedStep], cases: list[FailedCase]) -> str:
    out = ["# ParkWise PR CI: báo cáo lỗi", ""]
    if not steps and not cases:
        out += [
            "Không tìm thấy log lỗi trong ci-results.",
            "Lỗi nhiều khả năng xảy ra trước các bước kiểm tra (Verify Pull Request, "
            "Checkout PR) hoặc ở bước chưa ghi log. Xem Console Output.",
            "",
        ]
        return "\n".join(out)

    out += ["## Bước thất bại", ""]
    for step in steps:
        out += [f"### {step.label} (exit {step.exit_code})", "", "```", step.tail, "```", ""]
    if cases:
        out += ["## Test thất bại", ""]
        for case in cases:
            out.append(f"- `{case.suite}` / `{case.name}`: {case.message}")
        out.append("")
    return "\n".join(out)


def build_html(steps: list[FailedStep], cases: list[FailedCase]) -> str:
    if not steps and not cases:
        return "FAILED trước các bước kiểm tra. Xem Console Output."
    parts = ["<b>FAILED</b>"]
    if steps:
        parts.append(
            "Bước lỗi: " + ", ".join(html.escape(step.label) for step in steps)
        )
    for case in cases[:5]:
        parts.append(
            f"Test lỗi: {html.escape(case.suite)} / {html.escape(case.name)}"
            f" ({html.escape(case.message[:120])})"
        )
    if len(cases) > 5:
        parts.append(f"... và {len(cases) - 5} test lỗi khác (xem failure-report.md)")
    return "<br>".join(parts)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path, help="ci-results directory")
    parser.add_argument("--output", type=Path, required=True, help="Markdown report path")
    parser.add_argument("--html", type=Path, help="HTML summary path for the build description")
    args = parser.parse_args()

    steps = failed_steps(args.root)
    cases = failed_cases(args.root)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(build_markdown(steps, cases), encoding="utf-8")
    if args.html:
        args.html.write_text(build_html(steps, cases), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
