#!/usr/bin/env python3
"""Transfer a release over pinned SSH; rollback if public commit health fails."""
import argparse
import json
import os
import re
import shlex
import subprocess
import tempfile
import time
import urllib.parse
import urllib.request
from pathlib import Path
from application_manifest import digest


def configuration(environ):
    required = ("PRODUCTION_SSH_HOST", "PRODUCTION_SSH_USER", "PRODUCTION_SSH_PRIVATE_KEY",
                "PRODUCTION_SSH_KNOWN_HOSTS", "PRODUCTION_PUBLIC_HEALTH_URL", "DEPLOY_ROOT")
    if any(not environ.get(key) for key in required):
        raise ValueError("Missing deployment configuration")
    host, user = environ[required[0]], environ[required[1]]
    port = environ.get("PRODUCTION_SSH_PORT", "22") or "22"
    root = environ["DEPLOY_ROOT"]
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9.-]*", host) or not re.fullmatch(r"[a-z_][a-z0-9_-]*", user):
        raise ValueError("Invalid SSH host/user")
    if not port.isdigit() or not 1 <= int(port) <= 65535:
        raise ValueError("Invalid SSH port")
    if root != "/opt/startingweb/apps/anderson-multimarcas-unified":
        raise ValueError("Deployment root must be the dedicated unified directory")
    url = urllib.parse.urlsplit(environ["PRODUCTION_PUBLIC_HEALTH_URL"])
    if (url.scheme != "https" or url.hostname not in {"andersonmultimarcas.com.br", "www.andersonmultimarcas.com.br", "anderson.startingweb.com.br"}
            or url.username or url.password or url.port not in {None, 443} or url.path != "/health/release" or url.query or url.fragment):
        raise ValueError("Public health must be an audited HTTPS release endpoint")
    return host, user, port, root, url.geturl()


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def public_health(url, commit):
    opener = urllib.request.build_opener(NoRedirect())
    for _ in range(12):
        try:
            request = urllib.request.Request(url, headers={"Cache-Control": "no-cache"})
            with opener.open(request, timeout=5) as response:
                payload = response.read(4097)
                if len(payload) > 4096:
                    continue
                result = json.loads(payload)
                if response.status == 200 and isinstance(result, dict) and result.get("commit") == commit:
                    return True
        except (OSError, ValueError):
            pass
        time.sleep(5)
    return False


def verify_local_bundle(bundle):
    """Reject incomplete or corrupted transfers before opening an SSH session.

    The server independently repeats these checks using the separately supplied
    manifest hash. Checking here also prevents a failed CI download from touching
    a remote deployment directory.
    """
    if bundle.is_symlink() or not bundle.is_dir():
        raise ValueError("Bundle must be a real directory")
    for name in ("release.json", "image.tar.gz", "SHA256SUMS"):
        path = bundle / name
        if path.is_symlink() or not path.is_file():
            raise ValueError("Missing regular release file")
    checksums = {}
    for line in (bundle / "SHA256SUMS").read_text(encoding="ascii").splitlines():
        match = re.fullmatch(r"([0-9a-f]{64})  (image\.tar\.gz|release\.json)", line)
        if not match or match[2] in checksums:
            raise ValueError("Invalid release checksums")
        checksums[match[2]] = match[1]
    if set(checksums) != {"image.tar.gz", "release.json"} or any(
            digest(bundle / name) != checksum for name, checksum in checksums.items()):
        raise ValueError("Release artifact checksum mismatch")
    value = json.loads((bundle / "release.json").read_text())
    if not isinstance(value, dict) or set(value) != {"schemaVersion", "commit", "imageId", "imageTag", "ciRunId", "repository"}:
        raise ValueError("Invalid release manifest")
    if (value["schemaVersion"] != 1 or value["repository"] != "StartingWeb/AndersonMultiMarcas"
            or not isinstance(value["imageId"], str) or not re.fullmatch(r"sha256:[0-9a-f]{64}", value["imageId"])
            or not isinstance(value["commit"], str) or not re.fullmatch(r"[0-9a-f]{40}", value["commit"])
            or value["imageTag"] != "andersonmultimarcas-unified:" + value["commit"]):
        raise ValueError("Invalid release image/source")
    return value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, required=True)
    parser.add_argument("--deployer", type=Path, default=Path("scripts/deploy/release.py"))
    args = parser.parse_args()
    host, user, port, root, url = configuration(os.environ)
    release = verify_local_bundle(args.bundle)
    commit = release["commit"]
    run_id = str(release["ciRunId"])
    attempt = os.environ.get("GITHUB_RUN_ATTEMPT", "1")
    if not re.fullmatch(r"[0-9a-f]{40}", commit) or not re.fullmatch(r"[1-9][0-9]*", run_id) or not re.fullmatch(r"[1-9][0-9]*", attempt):
        raise ValueError("Invalid release identity")
    if commit != os.environ.get("SOURCE_COMMIT") or run_id != os.environ.get("CI_RUN_ID"):
        raise ValueError("Bundle differs from the trusted CI source/run")
    destination = f"{root}/incoming/{commit}-{run_id}-{attempt}"
    remote_script = destination + "/release.py"
    verify_and_run = (
        "import hashlib,runpy,sys; p=sys.argv[1]; expected=sys.argv[2]; "
        "actual=hashlib.sha256(open(p,'rb').read()).hexdigest(); "
        "assert actual==expected, 'Deployment script checksum mismatch'; "
        "sys.argv=[p]+sys.argv[3:]; runpy.run_path(p,run_name='__main__')")
    with tempfile.TemporaryDirectory(prefix="anderson-ssh-") as directory:
        private, known = Path(directory) / "identity", Path(directory) / "known_hosts"
        private.write_text(os.environ["PRODUCTION_SSH_PRIVATE_KEY"] + "\n")
        known.write_text(os.environ["PRODUCTION_SSH_KNOWN_HOSTS"] + "\n")
        private.chmod(0o600)
        known.chmod(0o600)
        options = ["-i", str(private), "-o", "IdentitiesOnly=yes", "-o", "BatchMode=yes",
                   "-o", "StrictHostKeyChecking=yes", "-o", "UserKnownHostsFile=" + str(known),
                   "-o", "ConnectTimeout=15", "-o", "LogLevel=ERROR"]
        peer = user + "@" + host
        def ssh(command):
            subprocess.run(["ssh", *options, "-p", port, peer, command], check=True)
        ssh(f"umask 077; test -d {shlex.quote(root + '/incoming')} && mkdir {shlex.quote(destination)}")
        subprocess.run(["scp", *options, "-P", port, str(args.bundle / "image.tar.gz"),
                        str(args.bundle / "release.json"), str(args.bundle / "SHA256SUMS"), str(args.deployer),
                        peer + ":" + destination + "/"], check=True)
        def operation(name, extra=()):
            return shlex.join(["python3", "-c", verify_and_run, remote_script, digest(args.deployer), name, "--root", root, *extra])
        ssh(operation("deploy", ["--bundle", destination, "--manifest-sha256", digest(args.bundle / "release.json")]))
        if not public_health(url, commit):
            print("Public release health failed; requesting application rollback (no database changes).")
            ssh(operation("rollback"))
            raise RuntimeError("Public health failed; previous application restored; investigate before retry")
    print(f"Deployment and public release health confirmed: {commit}")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        raise SystemExit(f"Delivery failed: {type(exc).__name__}. Check sanitized server release history; no secrets are printed.") from None
