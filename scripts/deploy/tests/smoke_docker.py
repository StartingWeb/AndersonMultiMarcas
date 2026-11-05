#!/usr/bin/env python3
"""Opt-in Docker lifecycle smoke test; no application, SQL, R2 or production resources.

Usage: python3 scripts/deploy/tests/smoke_docker.py --base-image nginx@sha256:...
The base must provide Alpine apk/nginx and support linux/amd64. It is explicitly
selected by the operator; the smoke installs curl only inside disposable images.
Every container, image tag, network and volume is prefixed with a random run ID.
Only resources created by this run are removed in finally.
"""

import argparse
import contextlib
import gzip
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import uuid

sys.path.insert(0, str(Path(__file__).parent))
from test_release import FIRST, SECOND, THIRD, ReleaseTests, release, write_json


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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-image", required=True)
    args = parser.parse_args()
    if "@sha256:" not in args.base_image:
        parser.error("Select a digest-pinned Alpine nginx base")
    prefix = "amm-smoke-" + uuid.uuid4().hex[:12]
    fixture = ReleaseTests(methodName="test_first_success_retains_legacy_and_immutable_configuration")
    fixture.setUp()
    network = prefix + "-network"
    keys = prefix + "-keys"
    tags = []
    created_network = False
    created_volume = False
    try:
        docker("network", "create", "--label", "com.startingweb.smoke-run=" + prefix, network)
        created_network = True
        docker("volume", "create", "--label", "com.startingweb.smoke-run=" + prefix, keys)
        created_volume = True
        engine = IsolatedDocker(prefix)
        fixture.manager.docker = engine
        fixture.manager.timeout = 8
        manifests = {}
        for commit in (FIRST, SECOND, THIRD):
            context = fixture.base / ("context-" + commit)
            context.mkdir()
            (context / "nginx.conf").write_text(
                "pid /tmp/nginx.pid; error_log /dev/stderr; events {} http { access_log off; "
                "client_body_temp_path /tmp/body; proxy_temp_path /tmp/proxy; fastcgi_temp_path /tmp/fastcgi; "
                "uwsgi_temp_path /tmp/uwsgi; scgi_temp_path /tmp/scgi; server { listen 8080; "
                + ("location /health { return 503; }" if commit == THIRD else
                   'location = /health { return 200 "OK"; } location = /health/release { default_type application/json; return 200 \'{"commit":"' + commit + '"}\'; }')
                + " } }\n")
            (context / "Dockerfile").write_text(
                f"FROM {args.base_image}\nRUN apk add --no-cache curl\nCOPY nginx.conf /smoke-nginx.conf\n"
                f"LABEL {release.REVISION_LABEL}={commit}\nENTRYPOINT [\"nginx\",\"-c\",\"/smoke-nginx.conf\",\"-g\",\"daemon off;\"]\n")
            tag = prefix + ":" + commit
            tags.append(tag)
            docker("build", "--platform", "linux/amd64", "--quiet", "--tag", tag, str(context))
            image_id = docker("image", "inspect", "--format", "{{.Id}}", tag).strip()
            bundle, _ = fixture.bundle(commit)
            with (bundle / "image.tar.gz").open("wb") as output:
                with gzip.GzipFile(fileobj=output, mode="wb", mtime=0) as compressed:
                    # Stream through a temporary file: subprocess requires a file
                    # descriptor and must not bypass the gzip wrapper.
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
        fixture.manager.deploy(manifests[FIRST][0], manifests[FIRST][1])
        fixture.manager.deploy(manifests[SECOND][0], manifests[SECOND][1])
        try:
            fixture.manager.deploy(manifests[THIRD][0], manifests[THIRD][1])
        except release.DeploymentError as error:
            if "prior application was restored" not in str(error):
                raise
        else:
            raise AssertionError("Unhealthy image unexpectedly succeeded")
        if fixture.manager.state()["current"]["commit"] != SECOND:
            raise AssertionError("Automatic rollback did not restore the second image")
        fixture.manager.rollback()
        if fixture.manager.state()["current"]["commit"] != FIRST or engine.loaded[-1] != FIRST:
            raise AssertionError("Explicit rollback did not reload first archive")
        # The first release can be selected and subsequently reversed to the
        # retained legacy baseline after a public health failure.
        fixture.manager.rollback(SECOND)
        fixture.manager.rollback(FIRST)
        state = fixture.manager.state()
        state["previous"] = None
        release.atomic_json(fixture.manager.state_file, state)
        fixture.manager.rollback()
        if fixture.manager.state() is not None:
            raise AssertionError("First-cutover reversal retained managed state")
        print("PASS: real isolated Docker deploy, failure recovery, archive rollback, legacy reversal")
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
        fixture.doCleanups()


if __name__ == "__main__":
    main()
