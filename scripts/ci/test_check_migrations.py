"""Unit tests for check_migrations.py."""

import unittest
from unittest.mock import MagicMock, patch
from check_migrations import SERVICES, check_service, main


class CheckMigrationsTests(unittest.TestCase):
    def test_services_list_has_five_microservices(self):
        self.assertEqual(len(SERVICES), 5)
        service_names = [s[0] for s in SERVICES]
        self.assertIn("Identity", service_names)
        self.assertIn("Parking", service_names)
        self.assertIn("Booking", service_names)
        self.assertIn("Payment", service_names)
        self.assertIn("Notification", service_names)

    @patch("subprocess.run")
    def test_check_service_passes_when_returncode_is_zero(self, mock_run):
        mock_run.return_value = MagicMock(returncode=0, stdout="No changes", stderr="")
        ok, msg = check_service("Identity", "src/proj", "src/startup")
        self.assertTrue(ok)
        self.assertIn("OK", msg)

    @patch("subprocess.run")
    def test_check_service_fails_when_returncode_is_nonzero(self, mock_run):
        mock_run.return_value = MagicMock(returncode=1, stdout="Pending changes", stderr="")
        ok, msg = check_service("Identity", "src/proj", "src/startup")
        self.assertFalse(ok)
        self.assertIn("FAILED", msg)

    @patch("check_migrations.check_service")
    def test_main_returns_zero_when_all_pass(self, mock_check):
        mock_check.return_value = (True, "OK")
        code = main()
        self.assertEqual(code, 0)

    @patch("check_migrations.check_service")
    def test_main_returns_one_when_any_fails(self, mock_check):
        mock_check.side_effect = [
            (True, "OK"),
            (False, "FAILED"),
            (True, "OK"),
            (True, "OK"),
            (True, "OK"),
        ]
        code = main()
        self.assertEqual(code, 1)


if __name__ == "__main__":
    unittest.main()
