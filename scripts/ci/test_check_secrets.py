"""Unit tests for check_secrets.py."""

import unittest
from check_secrets import is_excluded_path, scan_diff


class CheckSecretsTests(unittest.TestCase):
    def test_detects_private_key(self):
        diff = """--- a/src/app.cs
+++ b/src/app.cs
@@ -1,3 +1,4 @@
+-----BEGIN RSA PRIVATE KEY-----
+MIIEowIBAAKCAQEA0Y...
"""
        errors = scan_diff(diff)
        self.assertEqual(len(errors), 1)
        self.assertIn("Private Key", errors[0])

    def test_detects_github_pat(self):
        diff = """--- a/src/Service.cs
+++ b/src/Service.cs
@@ -10,1 +10,2 @@
+string token = "ghp_123456789012345678901234567890123456";
"""
        errors = scan_diff(diff)
        self.assertEqual(len(errors), 1)
        self.assertIn("GitHub Token", errors[0])

    def test_detects_aws_key(self):
        diff = """--- a/src/Storage.cs
+++ b/src/Storage.cs
@@ -5,1 +5,2 @@
+var key = "AKIAIOSFODNN7EXAMPLE";
"""
        errors = scan_diff(diff)
        self.assertEqual(len(errors), 1)
        self.assertIn("AWS Access Key ID", errors[0])

    def test_detects_hardcoded_db_password(self):
        diff = """--- a/src/Config.cs
+++ b/src/Config.cs
@@ -1,1 +1,2 @@
+string conn = "Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=SuperSecretPassword123!;";
"""
        errors = scan_diff(diff)
        self.assertEqual(len(errors), 1)
        self.assertIn("Password in Connection String", errors[0])

    def test_allows_placeholder_db_password(self):
        diff = """--- a/src/Config.cs
+++ b/src/Config.cs
@@ -1,1 +1,3 @@
+string conn1 = "Server=localhost;Database=db;User Id=postgres;Password=postgres;";
+string conn2 = "Server=localhost;Database=db;User Id=app;Password=${DB_PASSWORD};";
"""
        errors = scan_diff(diff)
        self.assertEqual(len(errors), 0)

    def test_ignores_excluded_extensions(self):
        self.assertTrue(is_excluded_path("web/package-lock.json"))
        self.assertTrue(is_excluded_path("src/Migrations/20261020_Init.Designer.cs"))
        self.assertTrue(is_excluded_path("docs/api/Identity.md"))
        self.assertFalse(is_excluded_path("src/Services/Identity/Program.cs"))


if __name__ == "__main__":
    unittest.main()
