import sys
import tempfile
import unittest
from pathlib import Path

import failure_report as fr

JUNIT = """<testsuite name="s" tests="2">
  <testcase classname="Admin.Page" name="renders list"><failure message="Unable to find text">stack</failure></testcase>
  <testcase classname="Admin.Page" name="renders header"/>
</testsuite>"""

ESC = chr(27)
TICK = chr(0x2713)

ESLINT_LOG = """
> web@0.1.0 lint
> eslint

/home/jenkins/agent/workspace/SmartParking-PR_PR-9/web/src/app/(owner)/owner/pricing/page.tsx
  137:13  error    Error: Calling setState synchronously within an effect can trigger cascading renders  react-hooks/set-state-in-effect
  140:5   warning  'x' is defined but never used  @typescript-eslint/no-unused-vars

/home/jenkins/agent/workspace/SmartParking-PR_PR-9/web/src/features/a/B.tsx
  12:3  error  'y' is not defined  no-undef

2 problems (2 errors, 1 warning)
"""

PRETTIER_LOG = """Checking formatting...
[warn] src/features/customer/CustomerMap.tsx
[warn] src/styles/owner-v4.css
[warn] Code style issues found in 2 files. Run Prettier with --write to fix.
"""


def write_step(root: Path, name: str, status: int, text: str) -> None:
    logs = root / "logs"
    logs.mkdir(parents=True, exist_ok=True)
    (logs / f"{name}.log").write_text(text, encoding="utf-8")
    (logs / f"{name}.status").write_text(f"{status}\n", encoding="utf-8")


class FailureReportTests(unittest.TestCase):
    def test_lists_only_steps_with_nonzero_exit(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_step(root, "frontend-lint", 0, "ok")
            write_step(root, "frontend-typecheck", 2, "error TS2304: Cannot find name 'x'")
            steps = fr.failed_steps(root)
        self.assertEqual([s.label for s in steps], ["Frontend Typecheck"])
        self.assertEqual(steps[0].exit_code, 2)
        self.assertIn("TS2304", steps[0].tail)

    def test_strips_ansi_and_keeps_only_the_tail(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            lines = [f"line {i}" for i in range(100)]
            lines[-1] = f"{ESC}[31mred final line{ESC}[0m"
            write_step(root, "frontend-build", 1, "\n".join(lines))
            step = fr.failed_steps(root, tail=5)[0]
        self.assertEqual(len(step.tail.splitlines()), 5)
        self.assertTrue(step.tail.endswith("red final line"))
        self.assertNotIn(ESC, step.tail)

    def test_dotnet_log_names_become_readable_labels(self) -> None:
        self.assertEqual(fr.label_for("dotnet-unit-payment"), "Payment Unit Tests")
        self.assertEqual(fr.label_for("something-new"), "something-new")
        self.assertEqual(fr.label_for("migration-drift"), "Migration Drift")

    def test_lint_highlights_name_the_file_line_and_message(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_step(root, "frontend-lint", 1, ESLINT_LOG)
            step = fr.failed_steps(root)[0]
        self.assertEqual(len(step.highlights), 2)
        self.assertTrue(
            step.highlights[0].startswith("src/app/(owner)/owner/pricing/page.tsx:137:13 ")
        )
        self.assertIn("setState synchronously", step.highlights[0])
        self.assertTrue(step.highlights[1].startswith("src/features/a/B.tsx:12:3 "))

    def test_format_highlight_lists_the_files_prettier_would_change(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_step(root, "frontend-format", 1, PRETTIER_LOG)
            step = fr.failed_steps(root)[0]
        self.assertEqual(len(step.highlights), 1)
        self.assertIn("2 file", step.highlights[0])
        self.assertIn("src/features/customer/CustomerMap.tsx", step.highlights[0])
        self.assertNotIn("Code style issues", step.highlights[0])

    def test_typecheck_and_policy_highlights(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_step(
                root,
                "frontend-typecheck",
                2,
                "src/a.ts(3,5): error TS2339: Property 'x' does not exist\nnoise\n",
            )
            write_step(
                root,
                "git-policy",
                1,
                "Git Policy: invalid PR title: fix(web): fix things\nother\n",
            )
            steps = {s.name: s for s in fr.failed_steps(root)}
        self.assertEqual(steps["frontend-typecheck"].highlights, ["src/a.ts(3,5): error TS2339: Property 'x' does not exist"])
        self.assertEqual(steps["git-policy"].highlights, ["Git Policy: invalid PR title: fix(web): fix things"])

    def test_collects_failed_junit_cases(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "frontend").mkdir()
            (root / "frontend" / "junit.xml").write_text(JUNIT, encoding="utf-8")
            (root / "broken.xml").write_text("<not xml", encoding="utf-8")
            cases = fr.failed_cases(root)
        self.assertEqual(len(cases), 1)
        self.assertEqual(cases[0].name, "renders list")
        self.assertIn("Unable to find text", cases[0].message)

    def test_report_without_findings_points_to_console(self) -> None:
        self.assertIn("Console Output", fr.build_markdown([], []))
        self.assertIn("Console Output", fr.build_text([], []))

    def test_text_summary_is_plain_text_with_the_details(self) -> None:
        step = fr.FailedStep(
            "frontend-lint",
            "Frontend Lint",
            1,
            "tail",
            ["src/a.tsx:1:2 Error: bad", "src/b.tsx:3:4 Error: worse"],
        )
        text = fr.build_text([step], [fr.FailedCase("S", "case", "boom")])
        self.assertNotIn("<", text)
        self.assertIn("Bước lỗi: Frontend Lint", text)
        self.assertIn("  src/a.tsx:1:2 Error: bad", text)
        self.assertIn("Test lỗi: S / case (boom)", text)
        self.assertIn("\n", text)

    def test_markdown_lists_the_highlights_before_the_log_tail(self) -> None:
        step = fr.FailedStep("frontend-lint", "Frontend Lint", 1, "tail", ["src/a.tsx:1:2 bad"])
        markdown = fr.build_markdown([step], [])
        self.assertLess(markdown.index("src/a.tsx:1:2 bad"), markdown.index("tail"))

    def test_flaky_tests_are_read_from_the_vitest_log(self) -> None:
        log = (
            f" {ESC}[32m{TICK}{ESC}[39m src/a.test.ts > list > shows rows 12ms (retry x1)\n"
            f" {TICK} src/a.test.ts > list > shows header 3ms\n"
        )
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_step(root, "frontend-vitest", 0, log)
            flaky = fr.flaky_tests(root)
        self.assertEqual(flaky, ["src/a.test.ts > list > shows rows"])
        self.assertIn("shows rows", fr.build_markdown([], [], flaky))
        self.assertIn("shows rows", fr.build_text([], [], flaky))

    def test_only_if_findings_writes_nothing_for_a_clean_run(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            write_step(root, "frontend-lint", 0, "ok")
            out = root / "report.md"
            old = sys.argv
            sys.argv = ["x", str(root), "--output", str(out), "--only-if-findings"]
            try:
                self.assertEqual(fr.main(), 0)
            finally:
                sys.argv = old
            self.assertFalse(out.exists())

    def test_missing_logs_directory_returns_no_steps(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            self.assertEqual(fr.failed_steps(Path(tmp)), [])


if __name__ == "__main__":
    unittest.main()
