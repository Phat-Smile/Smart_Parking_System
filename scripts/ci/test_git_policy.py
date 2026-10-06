"""Focused examples for the Git policy check."""

import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from check_git_policy import new_commit_errors, valid_branch, valid_message


class GitPolicyTests(unittest.TestCase):
    def test_trusted_base_policy_validates_legacy_head_without_policy_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repo = root / "repo"
            repo.mkdir()

            def git(*args, environment=None):
                return subprocess.check_output(
                    ["git", *args], cwd=repo, env=environment, text=True
                ).strip()

            git("init", "-b", "develop")
            git("config", "user.name", "Policy Test")
            git("config", "user.email", "policy@example.test")
            (repo / "README.md").write_text("foundation\n", encoding="utf-8")
            git("add", "README.md")
            git("commit", "-m", "initial foundation")

            git("switch", "-c", "features/Implementation_UserLogin")
            (repo / "login.txt").write_text("legacy login\n", encoding="utf-8")
            git("add", "login.txt")
            legacy_date = "2026-10-06T01:20:00+00:00"
            legacy_environment = dict(
                os.environ,
                GIT_AUTHOR_DATE=legacy_date,
                GIT_COMMITTER_DATE=legacy_date,
            )
            git("commit", "-m", "Implement User Login", environment=legacy_environment)
            head = git("rev-parse", "HEAD")

            git("switch", "develop")
            policy_source = Path(__file__).with_name("check_git_policy.py")
            policy_destination = repo / "scripts" / "ci" / "check_git_policy.py"
            policy_destination.parent.mkdir(parents=True)
            shutil.copyfile(policy_source, policy_destination)
            git("add", "scripts/ci/check_git_policy.py")
            git("commit", "-m", "ci(git): add trusted policy")
            base = git("rev-parse", "HEAD")

            trusted = root / "policy"
            candidate = root / "candidate"
            git("worktree", "add", "--detach", str(trusted), base)
            git("worktree", "add", "--detach", str(candidate), head)
            self.assertTrue((trusted / "scripts" / "ci" / "check_git_policy.py").is_file())
            self.assertFalse((candidate / "scripts" / "ci").exists())

            environment = dict(
                os.environ,
                POLICY_HEAD_BRANCH="features/Implementation_UserLogin",
                POLICY_PR_TITLE="feat(auth): implement user login",
                POLICY_BASE_SHA=base,
                POLICY_HEAD_SHA=head,
                POLICY_CUTOVER_UTC="2026-10-06T01:41:50Z",
            )

            def validate(**changes):
                return subprocess.run(
                    [sys.executable, str(trusted / "scripts" / "ci" / "check_git_policy.py")],
                    cwd=candidate,
                    env={**environment, **changes},
                    capture_output=True,
                    text=True,
                )

            self.assertEqual(validate().returncode, 0)
            self.assertIn(
                "invalid source branch",
                validate(POLICY_HEAD_BRANCH="feature/login").stderr,
            )
            self.assertIn(
                "invalid PR title",
                validate(POLICY_PR_TITLE="Login done").stderr,
            )

            git("switch", "-c", "features/Implementation_Untrusted", head)
            policy_destination.parent.mkdir(parents=True, exist_ok=True)
            policy_destination.write_text(
                'print("untrusted policy bypass")\n', encoding="utf-8"
            )
            git("add", "scripts/ci/check_git_policy.py")
            git("commit", "-m", "add untrusted policy", environment=legacy_environment)
            untrusted_head = git("rev-parse", "HEAD")
            untrusted = root / "untrusted"
            git("worktree", "add", "--detach", str(untrusted), untrusted_head)
            result = subprocess.run(
                [sys.executable, str(trusted / "scripts" / "ci" / "check_git_policy.py")],
                cwd=untrusted,
                env={
                    **environment,
                    "POLICY_HEAD_SHA": untrusted_head,
                    "POLICY_PR_TITLE": "Login done",
                },
                capture_output=True,
                text=True,
            )
            self.assertEqual(result.returncode, 1)
            self.assertIn("invalid PR title", result.stderr)
            self.assertNotIn("untrusted policy bypass", result.stdout)

    def test_allowed_branch_names(self):
        for branch in (
            "features/Implementation_UserLogin",
            "features/Design_UserLogin",
            "hotfix/Bug_UserLogin",
            "release/sprint_1",
        ):
            with self.subTest(branch=branch):
                self.assertTrue(valid_branch(branch))

    def test_rejected_branch_names(self):
        for branch in (
            "feature/UserLogin",
            "feat/login",
            "features/login",
            "features/implementation-login",
            "features/Implementation_userLogin",
            "features/Implementation_",
            "features/Implementation_User_Login",
            "release/sprint_0",
            "fix-login",
        ):
            with self.subTest(branch=branch):
                self.assertFalse(valid_branch(branch))

    def test_conventional_pr_titles(self):
        self.assertTrue(valid_message("feat(auth): implement user login", require_scope=True))
        self.assertTrue(valid_message("fix(auth): prevent credentials in login URL", require_scope=True))
        for title in ("Implement User Login from Stitch", "User Login", "fix(auth): fix"):
            with self.subTest(title=title):
                self.assertFalse(valid_message(title, require_scope=True))

    def test_conventional_commits_can_omit_scope(self):
        self.assertTrue(valid_message("ci: add frontend validation job", require_scope=False))
        self.assertFalse(valid_message("update", require_scope=False))
        self.assertFalse(valid_message("Merge develop into feature", require_scope=False))

    def test_old_commits_are_preserved_and_new_merge_commits_fail(self):
        outputs = {
            ("rev-list", "--reverse", "--parents", "base..head"): "old parent\nnew parent\nmerge first second",
            ("show", "-s", "--format=%ct", "old"): "10",
            ("show", "-s", "--format=%ct", "new"): "30",
            ("show", "-s", "--format=%ct", "merge"): "31",
            ("show", "-s", "--format=%s", "new"): "fix(auth): prevent native GET submission",
            ("show", "-s", "--format=%s", "merge"): "Merge develop into feature",
        }
        with patch("check_git_policy.git", side_effect=lambda *args: outputs[args]):
            errors = new_commit_errors("base", "head", cutover_epoch=20)
        self.assertEqual(len(errors), 1)
        self.assertIn("merge commits are not allowed", errors[0])

    def test_new_vague_commit_fails(self):
        outputs = {
            ("rev-list", "--reverse", "--parents", "base..head"): "new parent",
            ("show", "-s", "--format=%ct", "new"): "30",
            ("show", "-s", "--format=%s", "new"): "update",
        }
        with patch("check_git_policy.git", side_effect=lambda *args: outputs[args]):
            errors = new_commit_errors("base", "head", cutover_epoch=20)
        self.assertEqual(len(errors), 1)
        self.assertIn("invalid commit message", errors[0])


if __name__ == "__main__":
    unittest.main()
