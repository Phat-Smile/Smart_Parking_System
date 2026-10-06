"""Focused examples for the Git policy check."""

import unittest
from unittest.mock import patch

from check_git_policy import new_commit_errors, valid_branch, valid_message


class GitPolicyTests(unittest.TestCase):
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
