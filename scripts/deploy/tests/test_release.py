"""Transactional tests use only temporary files and an in-memory Docker double.

No network, Docker daemon, registry, production directory or database is touched.
The double models container identity/name/stop/start and load-after-prune semantics;
production health checks are separately tested through Docker's command boundary.
"""

import contextlib
import copy
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock


MODULE = Path(__file__).resolve().parents[1] / "release.py"
SPEC = importlib.util.spec_from_file_location("deployment_release", MODULE)
release = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(release)

FIRST = "a" * 40
SECOND = "b" * 40
THIRD = "c" * 40
LEGACY_IMAGE = "sha256:" + "e" * 64
SECRET = "do-not-emit-this-test-secret"


def write_json(path, value):
    path.write_text(json.dumps(value), encoding="utf-8")
    path.chmod(0o600)


class FakeDocker(release.Docker):
    def __init__(self):
        self.containers = {}
        self.images = {}
        self.calls = []
        self.fail_commits = set()
        self.loaded = []
        self.sequence = 0

    def container(self, image_id, commit=None, managed=False, running=False):
        self.sequence += 1
        labels = {release.REVISION_LABEL: commit} if commit else {}
        if managed:
            labels[release.MANAGED_LABEL] = release.MANAGER
        return {"Id": hashlib.sha256(str(self.sequence).encode()).hexdigest(), "Image": image_id,
                "State": {"Running": running}, "Config": {"Labels": labels, "Env": ["SECRET=" + SECRET]}}

    def run(self, *args, missing=False):
        self.calls.append(args)
        if args[:2] == ("container", "inspect"):
            value = self.containers.get(args[2])
            if value is None and missing:
                return None
            if value is None:
                raise release.DeploymentError("Missing test container")
            return json.dumps([value])
        if args[:2] in (("network", "inspect"), ("volume", "inspect")):
            return "[{}]"
        if args[0] == "load":
            archive = Path(args[2])
            manifest = json.loads((archive.parent / "release.json").read_text())
            self.images[manifest["imageId"]] = {
                "Id": manifest["imageId"], "Os": "linux", "Architecture": "amd64",
                "Config": {"Labels": {release.REVISION_LABEL: manifest["commit"]}},
            }
            self.loaded.append(manifest["commit"])
            return "Loaded test image"
        if args[:2] == ("image", "inspect"):
            return json.dumps([self.images[args[2]]])
        if args[0] == "stop":
            self.containers[args[-1]]["State"]["Running"] = False
        elif args[0] == "rename":
            if args[2] in self.containers:
                raise release.DeploymentError("Container name conflict")
            self.containers[args[2]] = self.containers.pop(args[1])
        elif args[0] == "run":
            name = args[args.index("--name") + 1]
            commit = args[args.index("--env", args.index("--env") + 1) + 1].split("=", 1)[1]
            if name in self.containers:
                raise release.DeploymentError("Container name conflict")
            self.containers[name] = self.container(args[-1], commit, managed=True, running=True)
            return self.containers[name]["Id"]
        elif args[0] == "start":
            self.containers[args[1]]["State"]["Running"] = True
        elif args[0] == "rm":
            self.containers.pop(args[-1], None)
        else:
            raise AssertionError(f"Unexpected Docker operation: {args[0]}")
        return ""

    def health(self, name, commit, timeout):
        self.calls.append(("TEST_HEALTH", name, commit))
        if commit in self.fail_commits:
            raise release.DeploymentError("Simulated unhealthy release")
        current = self.containers[name]
        if not current["State"]["Running"]:
            raise release.DeploymentError("Simulated stopped release")
        if commit is not None and current["Config"]["Labels"].get(release.REVISION_LABEL) != commit:
            raise release.DeploymentError("Simulated wrong release identity")

    def check_image(self, manifest, archive):
        # Transactional fixtures model Docker identity; archive parsing has its
        # own real tar/OCI tests, and smoke_docker exercises the real engine.
        self.run("load", "--input", str(archive))
        image = json.loads(self.run("image", "inspect", manifest["imageId"]))[0]
        release.require(image["Config"]["Labels"][release.REVISION_LABEL] == manifest["commit"],
                        "Image commit label mismatch")
        return image["Id"]


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.base = Path(self.temporary.name)
        self.root = self.base / "app"
        self.root.mkdir(mode=0o700)
        (self.root / "shared").mkdir(mode=0o700)
        (self.root / "releases").mkdir(mode=0o700)
        self.server = {
            "schemaVersion": 1, "containerName": release.NAME, "network": release.NETWORK,
            "dataProtectionVolume": release.DP_VOLUME, "uploads": {"type": "none", "reason": "Audited R2"},
            "legacyFallbackContainer": "andersonmultimarcas-legacy-reviewed",
        }
        write_json(self.root / "shared" / "server.json", self.server)
        write_json(self.root / "shared" / "source-parity.approved.json", {
            "reviewedUnifiedCommit": FIRST, "legacyImageId": LEGACY_IMAGE, "reviewedAt": "2026-11-05T12:00:00Z",
        })
        self.env = self.root / "shared" / "runtime.env"
        self.env.write_text("\n".join([
            "ASPNETCORE_ENVIRONMENT=Production", "Database__ApplyMigrationsOnStartup=false",
            "StorageMigration__Execute=false", "LegacyImageImport__RecoverPendingJobsOnStartup=false",
            "Storage__Provider=R2", "DataProtection__ApplicationName=AndersonMultiMarcas",
            "DataProtection__KeysPath=" + release.DP_PATH,
            "ConnectionStrings__DefaultConnectionProd=Server=isolated.invalid;Password=" + SECRET,
        ]) + "\n", encoding="utf-8")
        self.env.chmod(0o600)
        self.enabled = self.root / "shared" / "deploy.enabled"
        self.enabled.write_text("enabled\n")
        self.enabled.chmod(0o600)
        self.docker = FakeDocker()
        self.legacy = self.docker.container(LEGACY_IMAGE)
        self.docker.containers[self.server["legacyFallbackContainer"]] = self.legacy
        self.manager = release.ReleaseManager(self.root, docker=self.docker, health_timeout=1)

    def bundle(self, commit=FIRST):
        path = self.base / ("bundle-" + commit)
        path.mkdir(exist_ok=True)
        (path / "image.tar.gz").write_bytes(b"isolated simulated image " + commit.encode())
        manifest = {"schemaVersion": 1, "commit": commit, "imageId": "sha256:" + hashlib.sha256(commit.encode()).hexdigest(),
                    "imageTag": "andersonmultimarcas-unified:" + commit, "ciRunId": "12345",
                    "repository": "StartingWeb/AndersonMultiMarcas"}
        write_json(path / "release.json", manifest)
        self.checksums(path)
        return path, release.digest(path / "release.json")

    @staticmethod
    def checksums(path):
        (path / "SHA256SUMS").write_text("".join(release.digest(path / name) + "  " + name + "\n"
                                                for name in ("image.tar.gz", "release.json")))

    def deploy(self, commit=FIRST):
        bundle, expected_sha = self.bundle(commit)
        with contextlib.redirect_stdout(io.StringIO()):
            self.manager.deploy(bundle, expected_sha)

    def test_oci_runtime_identity_is_used_for_start_state_and_rollback(self):
        original = self.docker.check_image
        def local_identity(manifest, archive):
            original(manifest, archive)
            return 'sha256:' + hashlib.sha256(('local-oci-' + manifest['commit']).encode()).hexdigest()
        self.docker.check_image = local_identity
        self.deploy(FIRST)
        expected = 'sha256:' + hashlib.sha256(('local-oci-' + FIRST).encode()).hexdigest()
        self.assertEqual(expected, self.docker.containers[release.NAME]['Image'])
        self.assertEqual(expected, self.manager.state()['current']['imageId'])
        self.deploy(SECOND)
        with contextlib.redirect_stdout(io.StringIO()):
            self.manager.rollback()
        self.assertEqual(expected, self.docker.containers[release.NAME]['Image'])
        self.assertEqual(expected, self.manager.state()['current']['imageId'])

    def test_activation_gate_prevents_all_docker_operations(self):
        self.enabled.write_text("disabled\n")
        with self.assertRaisesRegex(release.DeploymentError, "disabled"):
            self.deploy()
        self.assertEqual([], self.docker.calls)

    def test_missing_source_parity_prevents_all_docker_operations(self):
        (self.root / "shared" / "source-parity.approved.json").unlink()
        with self.assertRaises(release.DeploymentError):
            self.deploy()
        self.assertEqual([], self.docker.calls)

    def test_live_legacy_is_never_stopped_or_renamed(self):
        self.docker.containers[release.NAME] = self.docker.container(LEGACY_IMAGE, running=True)
        with self.assertRaisesRegex(release.DeploymentError, "unmanaged"):
            self.deploy()
        self.assertFalse(any(args[0] in ("stop", "rename", "run", "load") for args in self.docker.calls))
        self.assertTrue(self.docker.containers[release.NAME]["State"]["Running"])

    def test_first_deploy_requires_stopped_reviewed_legacy(self):
        self.legacy["State"]["Running"] = True
        with self.assertRaisesRegex(release.DeploymentError, "stopped legacy"):
            self.deploy()
        self.assertFalse(any(args[0] in ("stop", "rename", "run", "load") for args in self.docker.calls))

    def test_first_deploy_rejects_wrong_legacy_image(self):
        self.legacy["Image"] = "sha256:" + "f" * 64
        with self.assertRaisesRegex(release.DeploymentError, "baseline image"):
            self.deploy()

    def test_archive_tamper_rejected_before_docker(self):
        path, expected_sha = self.bundle()
        (path / "image.tar.gz").write_bytes(b"tampered")
        with self.assertRaisesRegex(release.DeploymentError, "checksum mismatch"):
            self.manager.deploy(path, expected_sha)
        self.assertEqual([], self.docker.calls)

    def test_expected_manifest_hash_rejects_joint_tampering(self):
        path, expected_sha = self.bundle()
        manifest = json.loads((path / "release.json").read_text())
        manifest["ciRunId"] = "99999"
        write_json(path / "release.json", manifest)
        self.checksums(path)
        with self.assertRaisesRegex(release.DeploymentError, "manifest hash mismatch"):
            self.manager.deploy(path, expected_sha)

    def test_checksum_path_traversal_rejected(self):
        path, expected_sha = self.bundle()
        with (path / "SHA256SUMS").open("a") as stream:
            stream.write("0" * 64 + "  ../../outside\n")
        with self.assertRaisesRegex(release.DeploymentError, "SHA256SUMS"):
            self.manager.deploy(path, expected_sha)

    def test_runtime_migrations_are_forbidden(self):
        self.env.write_text(self.env.read_text().replace("ApplyMigrationsOnStartup=false", "ApplyMigrationsOnStartup=true"))
        with self.assertRaisesRegex(release.DeploymentError, "ApplyMigrationsOnStartup"):
            self.deploy()
        self.assertEqual([], self.docker.calls)

    def test_runtime_env_permissions_and_duplicate_entries_rejected(self):
        self.env.chmod(0o644)
        with self.assertRaisesRegex(release.DeploymentError, "0600"):
            self.deploy()
        self.env.chmod(0o600)
        with self.env.open("a") as stream:
            stream.write("Storage__Provider=Local\n")
        with self.assertRaisesRegex(release.DeploymentError, "Duplicate"):
            self.deploy()

    def test_dotnet_case_insensitive_env_override_rejected(self):
        with self.env.open("a") as stream:
            stream.write("DATABASE__APPLYMIGRATIONSONSTARTUP=true\n")
        with self.assertRaisesRegex(release.DeploymentError, "Duplicate"):
            self.deploy()
        self.assertEqual([], self.docker.calls)

    def test_deployment_commit_cannot_be_overridden_with_different_case(self):
        with self.env.open("a") as stream:
            stream.write("DEPLOYMENT__COMMIT=" + SECOND + "\n")
        with self.assertRaisesRegex(release.DeploymentError, "Deployment__Commit"):
            self.deploy()
        self.assertEqual([], self.docker.calls)

    def test_dotnet_environment_cannot_bypass_production_with_different_case(self):
        with self.env.open("a") as stream:
            stream.write("Dotnet_Environment=Development\n")
        with self.assertRaisesRegex(release.DeploymentError, "DOTNET_ENVIRONMENT"):
            self.deploy()
        self.assertEqual([], self.docker.calls)

    def test_first_success_retains_legacy_and_immutable_configuration(self):
        self.deploy()
        state = self.manager.state()
        self.assertEqual(FIRST, state["current"]["commit"])
        self.assertIsNone(state["previous"])
        self.assertFalse(self.legacy["State"]["Running"])
        self.assertIs(self.docker.containers[self.server["legacyFallbackContainer"]], self.legacy)
        snapshot = self.root / "releases" / FIRST
        self.assertIn(SECRET, (snapshot / "runtime.env").read_text())
        self.assertEqual(0o600, (snapshot / "runtime.env").stat().st_mode & 0o777)
        self.assertFalse(self.manager.transaction_file.exists())
        args = next(args for args in self.docker.calls if args[0] == "run")
        self.assertIn("1654:1654", args)
        self.assertIn("type=volume,src=" + release.DP_VOLUME + ",dst=" + release.DP_PATH, args)
        self.assertNotIn("--publish", args)
        self.assertNotIn(SECRET, " ".join(str(args) for args in self.docker.calls))

    def test_first_failure_restores_same_legacy_identity_and_checks_health(self):
        self.docker.fail_commits.add(FIRST)
        with self.assertRaisesRegex(release.DeploymentError, "prior application was restored"):
            self.deploy()
        self.assertIs(self.docker.containers[release.NAME], self.legacy)
        self.assertTrue(self.legacy["State"]["Running"])
        self.assertIsNone(self.manager.state())
        self.assertFalse(self.manager.transaction_file.exists())
        self.assertIn(("TEST_HEALTH", release.NAME, None), self.docker.calls)

    def test_failed_second_release_restores_previous_container_and_state(self):
        self.deploy()
        previous_state = self.manager.state()
        previous_container = self.docker.containers[release.NAME]
        self.docker.fail_commits.add(SECOND)
        with self.assertRaisesRegex(release.DeploymentError, "prior application was restored"):
            self.deploy(SECOND)
        self.assertEqual(previous_state, self.manager.state())
        self.assertIs(previous_container, self.docker.containers[release.NAME])
        self.assertTrue(previous_container["State"]["Running"])

    def test_image_revision_mismatch_is_rejected_before_stopping_current(self):
        original_run = self.docker.run

        def wrong_revision(*args, **kwargs):
            output = original_run(*args, **kwargs)
            if args[:2] == ("image", "inspect"):
                images = json.loads(output)
                images[0]["Config"]["Labels"][release.REVISION_LABEL] = SECOND
                return json.dumps(images)
            return output

        with mock.patch.object(self.docker, "run", side_effect=wrong_revision):
            with self.assertRaisesRegex(release.DeploymentError, "commit label mismatch"):
                self.deploy()
        self.assertFalse(any(args[0] in ("stop", "rename", "run") for args in self.docker.calls))

    def test_failed_history_append_does_not_block_automatic_application_restore(self):
        self.deploy()
        previous = self.docker.containers[release.NAME]
        original_history = self.manager.history

        def disk_error(event, **fields):
            if event == "release_failed":
                raise OSError("Simulated full history filesystem")
            return original_history(event, **fields)

        self.docker.fail_commits.add(SECOND)
        with mock.patch.object(self.manager, "history", side_effect=disk_error):
            with self.assertRaisesRegex(release.DeploymentError, "prior application was restored"):
                self.deploy(SECOND)
        self.assertIs(previous, self.docker.containers[release.NAME])
        self.assertEqual(FIRST, self.manager.state()["current"]["commit"])

    def test_rollback_reloads_archived_image_after_prune(self):
        self.deploy()
        self.deploy(SECOND)
        self.docker.images.clear()  # Models the audited daily docker image prune.
        with contextlib.redirect_stdout(io.StringIO()):
            self.manager.rollback()
        self.assertEqual([FIRST, SECOND, FIRST], self.docker.loaded)
        self.assertEqual(FIRST, self.manager.state()["current"]["commit"])
        self.assertEqual(SECOND, self.manager.state()["previous"]["commit"])

    def test_public_check_failure_after_first_success_can_restore_legacy(self):
        self.deploy()
        managed = self.docker.containers[release.NAME]
        with contextlib.redirect_stdout(io.StringIO()):
            self.manager.rollback()
        self.assertIs(self.legacy, self.docker.containers[release.NAME])
        self.assertTrue(self.legacy["State"]["Running"])
        self.assertFalse(managed["State"]["Running"])
        self.assertIsNone(self.manager.state())
        self.assertFalse(self.manager.transaction_file.exists())
        self.assertIn("legacy_rollback_succeeded", (self.root / "history.jsonl").read_text())

    def test_unhealthy_legacy_reversal_restores_managed_application(self):
        self.deploy()
        managed = self.docker.containers[release.NAME]
        self.docker.fail_commits.add(None)
        with self.assertRaisesRegex(release.DeploymentError, "managed application was restored"):
            self.manager.rollback()
        self.assertIs(managed, self.docker.containers[release.NAME])
        self.assertTrue(managed["State"]["Running"])
        self.assertEqual(FIRST, self.manager.state()["current"]["commit"])
        self.assertIs(self.legacy, self.docker.containers[self.server["legacyFallbackContainer"]])
        self.assertFalse(self.manager.transaction_file.exists())

    def test_failed_rollback_restores_the_release_that_was_current(self):
        self.deploy()
        self.deploy(SECOND)
        self.docker.fail_commits.add(FIRST)
        with self.assertRaisesRegex(release.DeploymentError, "prior application was restored"):
            self.manager.rollback()
        self.assertEqual(SECOND, self.manager.state()["current"]["commit"])
        self.assertTrue(self.docker.containers[release.NAME]["State"]["Running"])

    def test_snapshot_env_is_immutable_on_retry_and_not_taken_from_shared(self):
        self.deploy()
        saved = (self.root / "releases" / FIRST / "runtime.env").read_text()
        self.env.write_text("unsafe new configuration\n")
        self.deploy()
        self.assertEqual(saved, (self.root / "releases" / FIRST / "runtime.env").read_text())
        self.assertEqual(1, sum(args[0] == "run" for args in self.docker.calls))

    def test_release_history_and_stdout_never_contain_secrets(self):
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            path, expected_sha = self.bundle()
            self.manager.deploy(path, expected_sha)
        self.assertNotIn(SECRET, output.getvalue())
        history = (self.root / "history.jsonl").read_text()
        self.assertNotIn(SECRET, history)
        self.assertIn("release_succeeded", history)

    def test_pending_transaction_recovered_after_interruption(self):
        self.deploy()
        old_state = self.manager.state()
        old_container = self.docker.containers.pop(release.NAME)
        backup = "andersonmultimarcas-backup-interrupted"
        self.docker.containers[backup] = old_container
        old_container["State"]["Running"] = False
        self.docker.containers[release.NAME] = self.docker.container("sha256:" + "b" * 64, SECOND, True, True)
        write_json(self.manager.transaction_file, {"oldState": old_state, "oldContainerId": old_container["Id"],
                                                   "backupName": backup, "legacy": False, "newCommit": SECOND})
        with contextlib.redirect_stdout(io.StringIO()):
            self.manager.rollback()
        self.assertIs(old_container, self.docker.containers[release.NAME])
        self.assertEqual(old_state, self.manager.state())
        self.assertFalse(self.manager.transaction_file.exists())

    def test_pending_transaction_blocks_new_deploy(self):
        write_json(self.manager.transaction_file, {})
        with self.assertRaisesRegex(release.DeploymentError, "Pending transaction"):
            self.deploy()
        self.assertEqual([], self.docker.calls)

    def test_lock_blocks_parallel_deploy(self):
        with self.manager.locked():
            with self.assertRaisesRegex(release.DeploymentError, "holds the application lock"):
                self.deploy()

    def test_commands_never_prune_or_remove_volumes(self):
        self.deploy()
        self.deploy(SECOND)
        with contextlib.redirect_stdout(io.StringIO()):
            self.manager.rollback()
        for args in self.docker.calls:
            self.assertNotIn("prune", args)
            self.assertNotIn("--volumes", args)
            self.assertNotEqual(("volume", "rm"), args[:2])


class HealthAndCommandTests(unittest.TestCase):
    def test_health_executes_inside_exact_target_with_forwarded_https_and_commit(self):
        docker = release.Docker()
        calls = []

        def run(*args, **kwargs):
            calls.append(args)
            if args[:2] == ("container", "inspect"):
                return '[{"State":{"Running":true}}]'
            return json.dumps({"commit": FIRST}) if args[-1].endswith("/release") else '"OK"'

        with mock.patch.object(docker, "run", side_effect=run):
            docker.health(release.NAME, FIRST, 1)
        curls = [args for args in calls if args[0] == "exec"]
        self.assertEqual(2, len(curls))
        for args in curls:
            self.assertEqual(release.NAME, args[1])
            self.assertIn("X-Forwarded-Proto: https", args)
            self.assertTrue(args[-1].startswith("http://127.0.0.1:8080/health"))

    def test_wrong_health_commit_never_passes(self):
        docker = release.Docker()
        with mock.patch.object(docker, "inspect", return_value={"State": {"Running": True}}), \
                mock.patch.object(docker, "run", return_value=json.dumps({"commit": SECOND})), \
                mock.patch.object(release.time, "monotonic", side_effect=[0, 0, 2]), \
                mock.patch.object(release.time, "sleep"):
            with self.assertRaisesRegex(release.DeploymentError, "timed out"):
                docker.health(release.NAME, FIRST, 1)

    def test_nonobject_health_payload_is_retried_and_fails_closed(self):
        for payload in (None, [], "unexpected", 42):
            with self.subTest(payload=payload):
                docker = release.Docker()
                with mock.patch.object(docker, "inspect", return_value={"State": {"Running": True}}), \
                        mock.patch.object(docker, "run", return_value=json.dumps(payload)), \
                        mock.patch.object(release.time, "monotonic", side_effect=[0, 0, 2]), \
                        mock.patch.object(release.time, "sleep"):
                    with self.assertRaisesRegex(release.DeploymentError, "timed out"):
                        docker.health(release.NAME, FIRST, 1)

    def test_failed_docker_output_is_not_exposed(self):
        failure = subprocess.CompletedProcess(["docker"], 1, stdout=SECRET, stderr=SECRET)
        with mock.patch.object(release.subprocess, "run", return_value=failure):
            with self.assertRaises(release.DeploymentError) as error:
                release.Docker().run("load", "--input", "isolated.tar.gz")
        self.assertNotIn(SECRET, str(error.exception))


if __name__ == "__main__":
    unittest.main()
