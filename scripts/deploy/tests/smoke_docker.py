#!/usr/bin/env python3
"""Exercise real, disposable Docker deployment and failure recovery.

The pinned Alpine nginx fixture is intentionally independent of SQL/R2. Resource
names and labels use a random run ID; only this run's resources are removed.
SSH/SCP transfer failure is injected at the subprocess boundary, never connected
to a real host, while the retained application is checked in the Docker daemon.
"""

import argparse
import contextlib
import datetime
import gzip
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import uuid
from unittest import mock

sys.path.insert(0, str(Path(__file__).parent))
from test_release import FIRST, SECOND, THIRD, ReleaseTests, release, write_json
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "ci"))
import deliver

FOURTH = "d" * 40
INVALID = "f" * 40
TAMPERED = "1" * 40
MISCONFIGURED = "2" * 40


def docker(*args, stdout=None):
    result = subprocess.run(["docker", *args], stdout=stdout or subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if result.returncode:
        raise RuntimeError(f"Isolated Docker smoke operation failed: {args[0]}")
    return result.stdout.decode() if stdout is None else None


class IsolatedDocker(release.Docker):
    def __init__(self, prefix):
        self.prefix = prefix
        self.loaded = []

    def map(self, value):
        if value == release.NETWORK:
            return self.prefix + "-network"
        if value == release.DP_VOLUME:
            return self.prefix + "-keys"
        if value.startswith("andersonmultimarcas-"):
            return self.prefix + "-" + value.removeprefix("andersonmultimarcas-")
        return value.replace("src=" + release.DP_VOLUME + ",", "src=" + self.prefix + "-keys,")

    def run(self, *args, missing=False):
        mapped = [self.map(value) for value in args]
        if args[0] == "run":
            mapped[-1:-1] = ["--label", "com.startingweb.smoke-run=" + self.prefix]
        if args[0] == "load":
            self.loaded.append(json.loads((Path(args[2]).parent / "release.json").read_text())["commit"])
        return super().run(*mapped, missing=missing)

    def check_image(self, manifest, archive):
        return super().check_image({**manifest, "imageTag": self.map(manifest["imageTag"])}, archive)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-image", required=True)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    if "@sha256:" not in args.base_image:
        parser.error("Select a digest-pinned Alpine nginx base")
    prefix = "amm-smoke-" + uuid.uuid4().hex[:12]
    fixture = ReleaseTests(methodName="test_first_success_retains_legacy_and_immutable_configuration")
    fixture.setUp()
    network, keys = prefix + "-network", prefix + "-keys"
    tags = []
    created_network = created_volume = False
    results = []
    report = {"run": prefix, "baseImage": args.base_image, "scenarios": results,
              "at": datetime.datetime.now(datetime.timezone.utc).isoformat()}

    def passed(name):
        results.append({"scenario": name, "status": "passed"})
        print("PASS: " + name, flush=True)

    try:
        docker("network", "create", "--label", "com.startingweb.smoke-run=" + prefix, network)
        created_network = True
        docker("volume", "create", "--label", "com.startingweb.smoke-run=" + prefix, keys)
        created_volume = True
        engine = IsolatedDocker(prefix)
        fixture.manager.docker = engine
        fixture.manager.timeout = 8
        manifests = {}
        for commit in (FIRST, SECOND, THIRD, FOURTH):
            context = fixture.base / ("context-" + commit)
            context.mkdir()
            (context / "nginx.conf").write_text(
                "pid /tmp/nginx.pid; error_log /dev/stderr; events {} http { access_log off; "
                "client_body_temp_path /tmp/body; proxy_temp_path /tmp/proxy; fastcgi_temp_path /tmp/fastcgi; "
                "uwsgi_temp_path /tmp/uwsgi; scgi_temp_path /tmp/scgi; server { listen 8080; "
                + ("location /health { return 503; }" if commit == THIRD else
                   'location = /health { return 200 "OK"; } location = /health/release { default_type application/json; return 200 \'{"commit":"' + commit + '"}\'; }')
                + " } }\n")
            entrypoint = ('ENTRYPOINT ["/smoke/missing-executable"]\n' if commit == FOURTH else
                          'ENTRYPOINT ["nginx","-c","/smoke-nginx.conf","-g","daemon off;"]\n')
            (context / "Dockerfile").write_text(
                f"FROM {args.base_image}\nRUN apk add --no-cache curl\nCOPY nginx.conf /smoke-nginx.conf\n"
                f"LABEL {release.REVISION_LABEL}={commit}\n" + entrypoint)
            tag = prefix + "-unified:" + commit
            tags.append(tag)
            docker("build", "--platform", "linux/amd64", "--quiet", "--tag", tag, str(context))
            image_id = docker("image", "inspect", "--format", "{{.Id}}", tag).strip()
            bundle, _ = fixture.bundle(commit)
            with (bundle / "image.tar.gz").open("wb") as output:
                with gzip.GzipFile(fileobj=output, mode="wb", mtime=0) as compressed:
                    with tempfile.TemporaryFile() as saved:
                        docker("save", tag, stdout=saved)
                        saved.seek(0)
                        for block in iter(lambda: saved.read(1024 * 1024), b""):
                            compressed.write(block)
            manifest = json.loads((bundle / "release.json").read_text())
            manifest["imageId"] = image_id
            write_json(bundle / "release.json", manifest)
            fixture.checksums(bundle)
            manifests[commit] = (bundle, release.digest(bundle / "release.json"), image_id)
        parity_path = fixture.root / "shared" / "source-parity.approved.json"
        parity = json.loads(parity_path.read_text())
        parity["legacyImageId"] = manifests[FIRST][2]
        write_json(parity_path, parity)
        baseline = engine.map(fixture.server["legacyFallbackContainer"])
        docker("create", "--name", baseline, "--network", network, "--network-alias", engine.map(release.NAME),
               "--label", "com.startingweb.smoke-run=" + prefix, "--user", "1654:1654", manifests[FIRST][2])
        legacy_id = engine.inspect(fixture.server["legacyFallbackContainer"])["Id"]
        fixture.manager.deploy(*manifests[FIRST][:2])
        fixture.manager.deploy(*manifests[SECOND][:2])
        passed("successful_deployment")

        def assert_retained(expected_id):
            current = engine.inspect(release.NAME)
            if not current or current["Id"] != expected_id or fixture.manager.state()["current"]["commit"] != SECOND:
                raise AssertionError("Previous container identity/state was not retained")
            engine.health(release.NAME, SECOND, 8)
            if fixture.manager.transaction_file.exists():
                raise AssertionError("Completed recovery retained an active transaction")

        retained_id = engine.inspect(release.NAME)["Id"]
        for commit, scenario in ((THIRD, "health_failure_automatic_rollback"),
                                 (FOURTH, "startup_failure_automatic_rollback")):
            try:
                fixture.manager.deploy(*manifests[commit][:2])
            except release.DeploymentError as error:
                if "prior application was restored" not in str(error):
                    raise
            else:
                raise AssertionError("Broken candidate unexpectedly succeeded")
            assert_retained(retained_id)
            passed(scenario)

        invalid, invalid_sha = fixture.bundle(INVALID)
        try:
            fixture.manager.deploy(invalid, invalid_sha)
        except release.DeploymentError as error:
            if "load" not in str(error) and "archive" not in str(error):
                raise
        else:
            raise AssertionError("Invalid image archive unexpectedly succeeded")
        assert_retained(retained_id)
        passed("invalid_image_rejected_before_replacement")

        tampered, expected_sha = fixture.bundle(TAMPERED)
        (tampered / "image.tar.gz").write_bytes(b"tampered after checksums")
        try:
            fixture.manager.deploy(tampered, expected_sha)
        except release.DeploymentError as error:
            if "checksum mismatch" not in str(error):
                raise
        else:
            raise AssertionError("Tampered artifact unexpectedly succeeded")
        assert_retained(retained_id)
        passed("tampered_artifact_rejected")

        original_env = fixture.env.read_text()
        fixture.env.write_text(original_env.replace("ApplyMigrationsOnStartup=false", "ApplyMigrationsOnStartup=true"))
        misconfigured, misconfigured_sha = fixture.bundle(MISCONFIGURED)
        try:
            fixture.manager.deploy(misconfigured, misconfigured_sha)
        except release.DeploymentError as error:
            if "ApplyMigrationsOnStartup" not in str(error):
                raise
        else:
            raise AssertionError("Unsafe configuration unexpectedly succeeded")
        finally:
            fixture.env.write_text(original_env)
        assert_retained(retained_id)
        passed("unsafe_configuration_rejected_without_migrations")

        environment = {"PRODUCTION_SSH_HOST": "isolated.invalid", "PRODUCTION_SSH_USER": "deploy",
            "PRODUCTION_SSH_PRIVATE_KEY": "synthetic-never-used", "PRODUCTION_SSH_KNOWN_HOSTS": "synthetic-never-used",
            "PRODUCTION_PUBLIC_HEALTH_URL": "https://www.andersonmultimarcas.com.br/health/release",
            "DEPLOY_ROOT": "/opt/startingweb/apps/anderson-multimarcas-unified",
            "SOURCE_COMMIT": FIRST, "CI_RUN_ID": "12345"}
        with mock.patch.dict(deliver.os.environ, environment, clear=True), \
                mock.patch.object(sys, "argv", ["deliver.py", "--bundle", str(manifests[FIRST][0])]), \
                mock.patch.object(deliver.subprocess, "run", side_effect=[subprocess.CompletedProcess(["ssh"], 0),
                    subprocess.CalledProcessError(1, ["scp"])]) as transfer:
            try:
                deliver.main()
            except subprocess.CalledProcessError:
                pass
            else:
                raise AssertionError("Failed transfer unexpectedly succeeded")
            commands = [call.args[0] for call in transfer.call_args_list]
            if [command[0] for command in commands] != ["ssh", "scp"]:
                raise AssertionError("Failed transfer invoked a remote activation")
            if Path(commands[0][commands[0].index("-i") + 1]).exists():
                raise AssertionError("Transfer failure leaked temporary key file")
        assert_retained(retained_id)
        passed("transfer_failure_prevents_activation")

        fixture.manager.rollback()
        if fixture.manager.state()["current"]["commit"] != FIRST or engine.loaded[-1] != FIRST:
            raise AssertionError("Explicit rollback did not reload first archive")
        engine.health(release.NAME, FIRST, 8)
        passed("manual_rollback_from_preserved_archive")
        # Return to the first-cutover state, then exercise its explicit legacy
        # fallback. Only synthetic local state is changed by this fixture.
        state = fixture.manager.state()
        state["previous"] = None
        release.atomic_json(fixture.manager.state_file, state)
        fixture.manager.rollback()
        if fixture.manager.state() is not None or engine.inspect(release.NAME)["Id"] != legacy_id:
            raise AssertionError("Legacy reversal did not restore the retained baseline")
        engine.health(release.NAME, None, 8)
        passed("legacy_container_restored")
        report["status"] = "passed"
    except BaseException as error:
        report["status"] = "failed"
        report["errorType"] = type(error).__name__
        raise
    finally:
        containers = docker("ps", "-aq", "--filter", "label=com.startingweb.smoke-run=" + prefix).split()
        if containers:
            docker("rm", "--force", *containers)
        if created_volume:
            docker("volume", "rm", keys)
        if created_network:
            docker("network", "rm", network)
        if tags:
            docker("image", "rm", *tags)
        residue = {"containers": docker("ps", "-aq", "--filter", "label=com.startingweb.smoke-run=" + prefix).split(),
                   "volumes": docker("volume", "ls", "-q", "--filter", "label=com.startingweb.smoke-run=" + prefix).split(),
                   "networks": docker("network", "ls", "-q", "--filter", "label=com.startingweb.smoke-run=" + prefix).split(),
                   "imageTags": docker("image", "ls", "-q", prefix).split()}
        report["cleanup"] = residue
        fixture.doCleanups()
        if args.report:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
        if any(residue.values()):
            raise AssertionError("Isolated Docker smoke resources remain after cleanup")
        print("PASS: isolated Docker resources removed and absence verified", flush=True)


if __name__ == "__main__":
    main()
