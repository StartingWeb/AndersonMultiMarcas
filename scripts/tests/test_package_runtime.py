import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "ci"))
import package_release
import test_runtime_image


class FinalImageEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.commit = "a" * 40
        self.image = {"Id": "sha256:" + "b" * 64, "Os": "linux", "Architecture": "amd64",
                      "Config": {"User": "app", "Labels": {"org.opencontainers.image.revision": self.commit}}}
        self.report = {"status": "passed", "network": "none", "commit": self.commit,
                       "imageId": self.image["Id"], "remainingContainers": [],
                       "scenarios": [{"scenario": name, "status": "passed"} for name in (
                           "production_startup_and_health_without_database_or_network",
                           "release_health_matches_source", "published_static_image",
                           "native_magick_webp_conversion", "production_refuses_automatic_migrations")]}

    def verify(self, report):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "runtime.json"
            path.write_text(json.dumps(report))
            package_release.verify_runtime_report(path, self.image, self.commit)

    def test_success_is_bound_to_the_exact_tested_image(self):
        self.verify(self.report)
        for field, value in (("imageId", "sha256:" + "c" * 64), ("commit", "c" * 40),
                             ("status", "failed"), ("network", "bridge"),
                             ("remainingContainers", ["leftover"])):
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.verify({**self.report, field: value})

    def test_missing_or_failed_scenarios_cannot_be_packaged(self):
        failed = copy.deepcopy(self.report)
        failed["scenarios"][0]["status"] = "failed"
        for report in ({}, [], {**self.report, "scenarios": []},
                       {**self.report, "scenarios": self.report["scenarios"][:-1]}, failed):
            with self.subTest(report=report), self.assertRaises(ValueError):
                self.verify(report)

    @mock.patch.object(package_release.subprocess, "Popen")
    @mock.patch.object(package_release.subprocess, "check_output")
    def test_mismatched_report_fails_before_export_or_bundle_creation(self, inspect, export):
        inspect.return_value = json.dumps([self.image])
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "runtime.json"
            path.write_text(json.dumps({**self.report, "imageId": "sha256:" + "c" * 64}))
            bundle = Path(directory) / "bundle"
            args = ["package_release.py", "--commit", self.commit, "--run-id", "1",
                    "--runtime-report", str(path), "--output", str(bundle)]
            with mock.patch.object(sys, "argv", args), self.assertRaises(ValueError):
                package_release.main()
            export.assert_not_called()
            self.assertFalse(bundle.exists())


class RuntimeFailureEvidenceTests(unittest.TestCase):
    @mock.patch.object(test_runtime_image, "docker")
    def test_early_inspection_failure_preserves_evidence_without_starting_container(self, docker):
        docker.side_effect = RuntimeError("Image is unavailable")
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "runtime.json"
            args = ["test_runtime_image.py", "--image", "synthetic-missing-image",
                    "--commit", "a" * 40, "--report", str(path)]
            with mock.patch.object(sys, "argv", args), self.assertRaises(RuntimeError):
                test_runtime_image.main()
            report = json.loads(path.read_text())
            self.assertEqual("failed", report["status"])
            self.assertEqual("RuntimeError", report["errorType"])
            self.assertEqual([], report["remainingContainers"])
            docker.assert_called_once_with("image", "inspect", "synthetic-missing-image")


if __name__ == "__main__":
    unittest.main()
