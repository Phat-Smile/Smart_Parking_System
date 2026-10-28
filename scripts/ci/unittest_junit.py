"""Run the AI service's unittest suite and write a Jenkins JUnit report.

This keeps unittest discovery and its verbose console output without adding a
third-party dependency solely for CI reporting.
"""

from __future__ import annotations

import argparse
import sys
import time
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


class JUnitResult(unittest.TextTestResult):
    def __init__(self, *args: object, **kwargs: object) -> None:
        super().__init__(*args, **kwargs)
        self.cases: list[dict[str, str | float | None]] = []
        self.started: dict[unittest.case.TestCase, float] = {}

    def startTest(self, test: unittest.case.TestCase) -> None:  # noqa: N802
        self.started[test] = time.monotonic()
        super().startTest(test)

    def _record(
        self,
        test: unittest.case.TestCase,
        status: str,
        message: str | None = None,
        detail: str | None = None,
    ) -> None:
        test_id = test.id()
        classname, _, name = test_id.rpartition(".")
        self.cases.append(
            {
                "classname": classname,
                "name": name,
                "time": max(0.0, time.monotonic() - self.started.get(test, time.monotonic())),
                "status": status,
                "message": message,
                "detail": detail,
            }
        )

    def addSuccess(self, test: unittest.case.TestCase) -> None:  # noqa: N802
        super().addSuccess(test)
        self._record(test, "passed")

    def addFailure(self, test: unittest.case.TestCase, err: tuple) -> None:  # noqa: N802
        super().addFailure(test, err)
        self._record(test, "failure", str(err[1]), self._exc_info_to_string(err, test))

    def addError(self, test: unittest.case.TestCase, err: tuple) -> None:  # noqa: N802
        super().addError(test, err)
        self._record(test, "error", str(err[1]), self._exc_info_to_string(err, test))

    def addSkip(self, test: unittest.case.TestCase, reason: str) -> None:  # noqa: N802
        super().addSkip(test, reason)
        self._record(test, "skipped", reason)

    def addExpectedFailure(self, test: unittest.case.TestCase, err: tuple) -> None:  # noqa: N802
        super().addExpectedFailure(test, err)
        self._record(test, "skipped", "Expected failure", self._exc_info_to_string(err, test))

    def addUnexpectedSuccess(self, test: unittest.case.TestCase) -> None:  # noqa: N802
        super().addUnexpectedSuccess(test)
        self._record(test, "failure", "Unexpected success of a test marked expectedFailure")

    def addSubTest(self, test: unittest.case.TestCase, subtest: unittest.case._SubTest, err: tuple | None) -> None:  # noqa: N802, SLF001
        super().addSubTest(test, subtest, err)
        if err is not None:
            status = "failure" if issubclass(err[0], test.failureException) else "error"
            self._record(subtest, status, str(err[1]), self._exc_info_to_string(err, subtest))


def write_report(result: JUnitResult, output: Path) -> None:
    failures = sum(case["status"] == "failure" for case in result.cases)
    errors = sum(case["status"] == "error" for case in result.cases)
    skipped = sum(case["status"] == "skipped" for case in result.cases)
    suite = ET.Element(
        "testsuite",
        {
            "name": "AI / unittest",
            "tests": str(len(result.cases)),
            "failures": str(failures),
            "errors": str(errors),
            "skipped": str(skipped),
            "time": f"{sum(float(case['time']) for case in result.cases):.3f}",
        },
    )
    for case in result.cases:
        testcase = ET.SubElement(
            suite,
            "testcase",
            {
                "classname": str(case["classname"]),
                "name": str(case["name"]),
                "time": f"{case['time']:.3f}",
            },
        )
        if case["status"] != "passed":
            issue = ET.SubElement(testcase, str(case["status"]))
            issue.set("message", str(case["message"] or ""))
            if case["detail"]:
                issue.text = str(case["detail"])

    output.parent.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(suite).write(output, encoding="utf-8", xml_declaration=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--start-directory", default="tests")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    suite = unittest.defaultTestLoader.discover(args.start_directory)
    result = unittest.TextTestRunner(verbosity=2, resultclass=JUnitResult).run(suite)
    write_report(result, args.output)
    if result.testsRun == 0:
        print("No AI unittest cases were discovered.", file=sys.stderr)
        return 1
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    sys.exit(main())
