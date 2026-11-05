import copy
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

CI = Path(__file__).resolve().parents[1] / "ci"
sys.path.insert(0, str(CI))
import application_manifest
import check_vulnerabilities
import deliver
import verify_environment


class VulnerabilityGateTests(unittest.TestCase):
    def test_clean_projects_omit_frameworks_in_dotnet_output(self):
        self.assertEqual([], check_vulnerabilities.findings({"projects": [{"path": "Data.csproj"}]}))

    def test_advisories_are_deduplicated(self):
        advisory = {"severity": "High", "advisoryurl": "https://github.com/advisories/test"}
        package = {"id": "Example", "resolvedVersion": "1", "vulnerabilities": [advisory, advisory]}
        doc = {"projects": [{"path": "Project.csproj", "frameworks": [{"topLevelPackages": [package]}]}]}
        self.assertEqual(1, len(check_vulnerabilities.findings(doc)))

    def test_missing_or_failed_audit_is_not_success(self):
        for document in ({}, {"projects": []}, {"projects": [{"path": "x"}], "problems": ["feed failed"]}):
            with self.subTest(document=document), self.assertRaises(ValueError):
                check_vulnerabilities.findings(document)


class EnvironmentApprovalTests(unittest.TestCase):
    def setUp(self):
        self.environment = {"name": "production",
            "protection_rules": [{"type": "required_reviewers", "prevent_self_review": True, "reviewers": [{"id": 1}]}],
            "deployment_branch_policy": {"protected_branches": False, "custom_branch_policies": True}}
        self.policy = {"total_count": 1, "branch_policies": [{"name": "main", "type": "branch"}]}

    def test_reviewed_main_only_environment_passes(self):
        verify_environment.validate(self.environment, self.policy)

    def test_missing_reviewers_and_self_review_are_rejected(self):
        for field, value in (("protection_rules", []),):
            invalid = copy.deepcopy(self.environment)
            invalid[field] = value
            with self.assertRaises(ValueError):
                verify_environment.validate(invalid, self.policy)
        self.environment["protection_rules"][0]["prevent_self_review"] = False
        with self.assertRaises(ValueError):
            verify_environment.validate(self.environment, self.policy)

    def test_tags_and_wildcards_are_rejected(self):
        for name, kind in (("*", "branch"), ("main", "tag")):
            self.policy["branch_policies"] = [{"name": name, "type": kind}]
            with self.assertRaises(ValueError):
                verify_environment.validate(self.environment, self.policy)

    @mock.patch.dict(verify_environment.os.environ, {}, clear=True)
    def test_manual_bypass_review_is_required_without_claiming_an_api_field(self):
        with self.assertRaises(ValueError):
            verify_environment.main()


class ProvenanceTests(unittest.TestCase):
    def test_publish_files_are_bound_to_source_and_detect_tampering(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "publish").mkdir()
            assembly = root / "publish" / "Project.dll"
            assembly.write_bytes(b"test fixture, not a production assembly")
            before = application_manifest.manifest(root, "a" * 40, "1")
            assembly.write_bytes(b"modified")
            self.assertNotEqual(before, application_manifest.manifest(root, "a" * 40, "1"))
            self.assertNotEqual(before["commit"], application_manifest.manifest(root, "b" * 40, "1")["commit"])

    def test_symlinks_cannot_escape_publish_tree(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "publish").mkdir()
            (root / "publish" / "Project.dll").symlink_to("/does-not-exist")
            with self.assertRaises(ValueError):
                application_manifest.manifest(root, "a" * 40, "1")


class DeliveryConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.env = {"PRODUCTION_SSH_HOST": "host.example", "PRODUCTION_SSH_USER": "deploy",
            "PRODUCTION_SSH_PRIVATE_KEY": "test-only-placeholder", "PRODUCTION_SSH_KNOWN_HOSTS": "test-only-placeholder",
            "PRODUCTION_PUBLIC_HEALTH_URL": "https://www.andersonmultimarcas.com.br/health/release",
            "DEPLOY_ROOT": "/opt/startingweb/apps/anderson-multimarcas-unified"}

    def test_audited_destination_only(self):
        self.assertEqual("22", deliver.configuration(self.env)[2])

    def test_legacy_root_injection_and_insecure_health_are_rejected(self):
        for key, value in (("DEPLOY_ROOT", "/opt/startingweb/apps/anderson-multimarcas"),
                           ("PRODUCTION_SSH_HOST", "host;whoami"),
                           ("PRODUCTION_PUBLIC_HEALTH_URL", "http://www.andersonmultimarcas.com.br/health/release")):
            invalid = dict(self.env, **{key: value})
            with self.subTest(key=key), self.assertRaises(ValueError):
                deliver.configuration(invalid)

    @mock.patch.object(deliver.time, "sleep")
    @mock.patch.object(deliver.urllib.request, "build_opener")
    def test_public_health_rejects_wrong_commit(self, build_opener, sleep):
        response = mock.MagicMock()
        response.status = 200
        response.read.return_value = b'{"commit":"wrong"}'
        build_opener.return_value.open.return_value.__enter__.return_value = response
        self.assertFalse(deliver.public_health(self.env["PRODUCTION_PUBLIC_HEALTH_URL"], "a" * 40))

    @mock.patch.object(deliver.time, "sleep")
    @mock.patch.object(deliver.urllib.request, "build_opener")
    def test_public_health_rejects_json_arrays_without_escaping_rollback_path(self, build_opener, sleep):
        response = mock.MagicMock()
        response.status = 200
        response.read.return_value = b'[]'
        build_opener.return_value.open.return_value.__enter__.return_value = response
        self.assertFalse(deliver.public_health(self.env["PRODUCTION_PUBLIC_HEALTH_URL"], "a" * 40))

    def delivery_fixture(self, directory):
        root = Path(directory)
        bundle = root / "bundle"
        bundle.mkdir()
        (bundle / "release.json").write_text(json.dumps({"commit": "a" * 40, "ciRunId": "1"}))
        (bundle / "image.tar.gz").write_bytes(b"isolated transfer fixture")
        (bundle / "SHA256SUMS").write_text("fixture, remote executor is mocked")
        deployer = root / "release.py"
        deployer.write_text("# isolated fixture")
        return bundle, deployer

    @mock.patch.object(deliver, "public_health", return_value=False)
    @mock.patch.object(deliver.subprocess, "run")
    def test_public_failure_requests_remote_rollback_and_erases_temporary_keys(self, run, health):
        with tempfile.TemporaryDirectory() as directory:
            bundle, deployer = self.delivery_fixture(directory)
            env = dict(self.env, SOURCE_COMMIT="a" * 40, CI_RUN_ID="1", GITHUB_RUN_ATTEMPT="1")
            with mock.patch.dict(deliver.os.environ, env, clear=True), mock.patch.object(sys, "argv", ["deliver.py", "--bundle", str(bundle), "--deployer", str(deployer)]):
                with self.assertRaises(RuntimeError):
                    deliver.main()
            ssh_calls = [call.args[0] for call in run.call_args_list if call.args[0][0] == "ssh"]
            self.assertEqual(3, len(ssh_calls))
            self.assertIn("rollback", ssh_calls[-1][-1])
            key_path = Path(ssh_calls[0][ssh_calls[0].index("-i") + 1])
            self.assertFalse(key_path.exists())
            self.assertNotIn("test-only-placeholder", str(run.call_args_list))

    @mock.patch.object(deliver.subprocess, "run")
    def test_wrong_ci_identity_is_rejected_before_any_ssh(self, run):
        with tempfile.TemporaryDirectory() as directory:
            bundle, deployer = self.delivery_fixture(directory)
            env = dict(self.env, SOURCE_COMMIT="b" * 40, CI_RUN_ID="1")
            with mock.patch.dict(deliver.os.environ, env, clear=True), mock.patch.object(sys, "argv", ["deliver.py", "--bundle", str(bundle), "--deployer", str(deployer)]):
                with self.assertRaises(ValueError):
                    deliver.main()
            run.assert_not_called()


if __name__ == "__main__":
    unittest.main()
