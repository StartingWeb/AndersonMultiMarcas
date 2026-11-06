#!/usr/bin/env python3
"""Controlled Docker replacement. No shell, database commands, registry or volume removal.

Every server mutation is behind local activation gates; installing this script does
not activate deployment. Configuration and image archives stay with each release,
so a Docker image prune cannot destroy the application's rollback material.
"""

import argparse
import contextlib
import datetime
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import stat
import subprocess
import sys
import tempfile
import time
import uuid


NAME = "andersonmultimarcas-web"
NETWORK = "startingweb_network"
DP_VOLUME = "andersonmultimarcas-dataprotection-keys"
DP_PATH = "/home/app/.aspnet/DataProtection-Keys"
MANAGED_LABEL = "com.startingweb.managed-by"
MANAGER = "andersonmultimarcas-unified"
REVISION_LABEL = "org.opencontainers.image.revision"
COMMIT = re.compile(r"[0-9a-f]{40}")
IMAGE = re.compile(r"sha256:[0-9a-f]{64}")
HEX = re.compile(r"[0-9a-f]{64}")
FILES = ("image.tar.gz", "release.json", "SHA256SUMS")


class DeploymentError(Exception):
    pass


def require(condition, message):
    if not condition:
        raise DeploymentError(message)


def regular(path, secret=False):
    require(path.is_file() and not path.is_symlink(), f"Required regular file: {path.name}")
    mode = stat.S_IMODE(path.stat().st_mode)
    require(not mode & 0o022, f"Group/world writable file refused: {path.name}")
    if secret:
        require(mode == 0o600, f"Secret/snapshot must have mode 0600: {path.name}")


def read_json(path, secret=False):
    regular(path, secret)
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (ValueError, UnicodeError):
        raise DeploymentError(f"Invalid JSON: {path.name}") from None
    require(isinstance(value, dict), f"JSON object required: {path.name}")
    return value


def atomic_json(path, value):
    fd, temporary = tempfile.mkstemp(prefix=".write-", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as stream:
            os.fchmod(stream.fileno(), 0o600)
            json.dump(value, stream, sort_keys=True)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
        directory_fd = os.open(path.parent, os.O_RDONLY)
        try:
            os.fsync(directory_fd)
        finally:
            os.close(directory_fd)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def digest(path):
    hasher = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(block)
    return hasher.hexdigest()


def validate_manifest(value):
    expected = {"schemaVersion", "commit", "imageId", "imageTag", "ciRunId", "repository"}
    require(set(value) == expected, "Unexpected release manifest fields")
    require(value["schemaVersion"] == 1, "Unsupported release manifest schema")
    require(isinstance(value["commit"], str) and COMMIT.fullmatch(value["commit"]), "Invalid commit")
    require(isinstance(value["imageId"], str) and IMAGE.fullmatch(value["imageId"]), "Invalid image ID")
    require(value["imageTag"] == f"andersonmultimarcas-unified:{value['commit']}", "Invalid image tag")
    require(str(value["ciRunId"]).isdigit() and int(value["ciRunId"]) > 0, "Invalid CI run ID")
    require(value["repository"] == "StartingWeb/AndersonMultiMarcas", "Unexpected source repository")
    return value


def verify_bundle(bundle, expected_manifest_sha=None):
    require(bundle.is_dir() and not bundle.is_symlink(), "Bundle must be a real directory")
    for filename in FILES:
        regular(bundle / filename)
    if expected_manifest_sha is not None:
        require(HEX.fullmatch(expected_manifest_sha), "Invalid expected manifest SHA-256")
        require(digest(bundle / "release.json") == expected_manifest_sha, "Transferred manifest hash mismatch")
    checksums = {}
    for line in (bundle / "SHA256SUMS").read_text(encoding="ascii").splitlines():
        match = re.fullmatch(r"([0-9a-f]{64})  (image\.tar\.gz|release\.json)", line)
        require(match is not None, "Invalid SHA256SUMS entry")
        require(match[2] not in checksums, "Duplicate SHA256SUMS entry")
        checksums[match[2]] = match[1]
    require(set(checksums) == {"image.tar.gz", "release.json"}, "Incomplete SHA256SUMS")
    for filename, expected in checksums.items():
        require(digest(bundle / filename) == expected, f"Artifact checksum mismatch: {filename}")
    return validate_manifest(read_json(bundle / "release.json"))


def validate_server(path):
    value = read_json(path)
    required = {"schemaVersion", "containerName", "network", "dataProtectionVolume", "uploads", "legacyFallbackContainer"}
    require(set(value) == required, "Unexpected server configuration fields")
    require(value["schemaVersion"] == 1, "Unsupported server configuration schema")
    require(value["containerName"] == NAME and value["network"] == NETWORK, "Unexpected container/network")
    require(value["dataProtectionVolume"] == DP_VOLUME, "Unexpected Data Protection volume")
    uploads = value["uploads"]
    require(isinstance(uploads, dict), "Explicit uploads policy required")
    # The audited deployment uses R2. A future local-storage cutover requires a
    # reviewed extension; silently creating an empty volume would lose images.
    require(set(uploads) == {"type", "reason"} and uploads["type"] == "none" and
            isinstance(uploads["reason"], str) and uploads["reason"].strip(), "Only the audited R2/no-local-mount policy is supported")
    fallback = value["legacyFallbackContainer"]
    require(isinstance(fallback, str) and re.fullmatch(r"andersonmultimarcas-legacy-[a-z0-9-]{1,50}", fallback),
            "Explicit retained legacy fallback container required")
    return value


def validate_env(path):
    regular(path, secret=True)
    values = {}
    normalized_keys = set()
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue
        key, separator, value = line.partition("=")
        require(separator and re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", key), "Invalid Docker env-file entry")
        # .NET configuration keys are case-insensitive, even though Linux env
        # keys are not. A second spelling must not override a required guard.
        require(key.casefold() not in normalized_keys, "Duplicate env-file entry")
        require("\x00" not in value, "Invalid env-file value")
        values[key] = value
        normalized_keys.add(key.casefold())
    required = {
        "ASPNETCORE_ENVIRONMENT": "Production",
        "Database__ApplyMigrationsOnStartup": "false",
        "StorageMigration__Execute": "false",
        "LegacyImageImport__RecoverPendingJobsOnStartup": "false",
        "Storage__Provider": "R2",
        "DataProtection__ApplicationName": "AndersonMultiMarcas",
        "DataProtection__KeysPath": DP_PATH,
    }
    for key, expected in required.items():
        require(values.get(key) == expected, f"Required safe runtime setting missing/invalid: {key}")
    normalized_values = {key.casefold(): value for key, value in values.items()}
    require(normalized_values.get("dotnet_environment", "Production") == "Production", "DOTNET_ENVIRONMENT must be Production")
    require("deployment__commit" not in normalized_keys, "Deployment__Commit is supplied by the release manifest")
    return values


class Docker:
    def run(self, *args, missing=False):
        result = subprocess.run(["docker", *args], capture_output=True, text=True, check=False)
        if result.returncode:
            if missing and ("No such object" in result.stderr or "No such container" in result.stderr):
                return None
            # Docker inspect, env files and startup logs can contain credentials.
            # Never echo command output or application logs to Actions.
            raise DeploymentError(f"Docker operation failed: {args[0]}")
        return result.stdout

    def inspect(self, name):
        output = self.run("container", "inspect", name, missing=True)
        return json.loads(output)[0] if output is not None else None

    def check_image(self, manifest, archive):
        self.run("load", "--input", str(archive))
        image = json.loads(self.run("image", "inspect", manifest["imageId"]))[0]
        require(image["Id"] == manifest["imageId"], "Loaded image ID mismatch")
        require(image.get("Os") == "linux" and image.get("Architecture") == "amd64", "Image must target linux/amd64")
        require((image.get("Config", {}).get("Labels") or {}).get(REVISION_LABEL) == manifest["commit"], "Image commit label mismatch")

    def health(self, name, commit, timeout):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            try:
                container = self.inspect(name)
                if not container or not container["State"]["Running"]:
                    raise ValueError("Container is not running")
                curl = ("exec", name, "curl", "--fail", "--silent", "--show-error", "--max-time", "3",
                        "--header", "X-Forwarded-Proto: https")
                self.run(*curl, "http://127.0.0.1:8080/health")
                if commit is not None:
                    payload = json.loads(self.run(*curl, "http://127.0.0.1:8080/health/release"))
                    if not isinstance(payload, dict) or payload.get("commit") != commit:
                        raise ValueError("Unexpected deployed commit")
                return
            except (OSError, ValueError, KeyError, DeploymentError):
                time.sleep(1)
        raise DeploymentError("Application health/commit validation timed out")


class ReleaseManager:
    def __init__(self, root, docker=None, health_timeout=90):
        self.root = Path(root)
        self.docker = docker or Docker()
        self.timeout = health_timeout
        self.state_file = self.root / "current.json"
        self.transaction_file = self.root / "transaction.json"

    @contextlib.contextmanager
    def locked(self):
        require(self.root.is_dir() and not self.root.is_symlink(), "Deployment root must already exist")
        require(not stat.S_IMODE(self.root.stat().st_mode) & 0o022, "Deployment root is group/world writable")
        for name in ("shared", "releases"):
            path = self.root / name
            require(path.is_dir() and not path.is_symlink(), f"Required real directory: {name}")
            require(not stat.S_IMODE(path.stat().st_mode) & 0o022, f"Directory is group/world writable: {name}")
        lock = self.root / "deploy.lock"
        require(not lock.is_symlink(), "Symlink lock refused")
        with lock.open("a", encoding="utf-8") as stream:
            os.fchmod(stream.fileno(), 0o600)
            try:
                fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise DeploymentError("Another deployment holds the application lock") from None
            yield

    def gates(self):
        shared = self.root / "shared"
        enabled = shared / "deploy.enabled"
        regular(enabled, secret=True)
        require(enabled.read_text(encoding="ascii").strip() == "enabled", "Production deployment is disabled")
        parity = read_json(shared / "source-parity.approved.json", secret=True)
        require(set(parity) == {"reviewedUnifiedCommit", "legacyImageId", "reviewedAt"}, "Invalid source-parity approval")
        require(isinstance(parity["reviewedUnifiedCommit"], str) and COMMIT.fullmatch(parity["reviewedUnifiedCommit"]), "Invalid source-parity reviewed commit")
        require(isinstance(parity["legacyImageId"], str) and IMAGE.fullmatch(parity["legacyImageId"]), "Invalid source-parity legacy image")
        require(isinstance(parity["reviewedAt"], str) and parity["reviewedAt"].strip(), "Missing source-parity review date")
        self.parity = parity
        return validate_server(shared / "server.json")

    def history(self, event, **fields):
        path = self.root / "history.jsonl"
        require(not path.is_symlink(), "Symlink release history refused")
        with path.open("a", encoding="utf-8") as stream:
            os.fchmod(stream.fileno(), 0o600)
            record = {"at": datetime.datetime.now(datetime.timezone.utc).isoformat(), "event": event, **fields}
            stream.write(json.dumps(record, sort_keys=True) + "\n")
            stream.flush()
            os.fsync(stream.fileno())

    def state(self):
        if not self.state_file.exists():
            return None
        state = read_json(self.state_file, secret=True)
        require(set(state) == {"current", "previous"}, "Invalid current state")
        for reference in (state["current"], state["previous"]):
            if reference is not None:
                require(set(reference) == {"commit", "imageId", "ciRunId"}, "Invalid release reference")
                require(isinstance(reference["commit"], str) and COMMIT.fullmatch(reference["commit"]), "Invalid state commit")
                require(isinstance(reference["imageId"], str) and IMAGE.fullmatch(reference["imageId"]), "Invalid state image")
        require(state["current"] is not None, "Current state must reference a release")
        return state

    def snapshot(self, bundle, expected_sha):
        manifest = verify_bundle(bundle, expected_sha)
        release = self.root / "releases" / manifest["commit"]
        if release.exists():
            require(verify_bundle(release) == manifest, "Existing immutable release differs")
            validate_env(release / "runtime.env")
            validate_server(release / "server.json")
            return release, manifest
        validate_env(self.root / "shared" / "runtime.env")
        temporary = Path(tempfile.mkdtemp(prefix=".incoming-", dir=self.root / "releases"))
        try:
            os.chmod(temporary, 0o700)
            for name in FILES:
                shutil.copyfile(bundle / name, temporary / name)
                os.chmod(temporary / name, 0o600)
            for name in ("runtime.env", "server.json", "source-parity.approved.json"):
                shutil.copyfile(self.root / "shared" / name, temporary / name)
                os.chmod(temporary / name, 0o600)
            # Re-read the copied material to reject changes during transfer/copy.
            require(verify_bundle(temporary, expected_sha) == manifest, "Snapshot changed while copying")
            validate_env(temporary / "runtime.env")
            validate_server(temporary / "server.json")
            os.rename(temporary, release)
        finally:
            if temporary.exists():
                shutil.rmtree(temporary)
        return release, manifest

    def existing(self, commit):
        require(isinstance(commit, str) and COMMIT.fullmatch(commit), "Invalid rollback commit")
        release = self.root / "releases" / commit
        manifest = verify_bundle(release)
        require(manifest["commit"] == commit, "Snapshot commit differs from directory")
        validate_env(release / "runtime.env")
        validate_server(release / "server.json")
        return release, manifest

    @staticmethod
    def managed(container, commit=None):
        labels = container.get("Config", {}).get("Labels") or {}
        return labels.get(MANAGED_LABEL) == MANAGER and (commit is None or labels.get(REVISION_LABEL) == commit)

    def preflight(self, server, old_state):
        self.docker.run("network", "inspect", NETWORK)
        self.docker.run("volume", "inspect", DP_VOLUME)
        current = self.docker.inspect(NAME)
        if current:
            require(self.managed(current), "Legacy/unmanaged live container refused; cutover requires separate authorization")
            require(old_state is not None and self.managed(current, old_state["current"]["commit"]) and
                    current["Image"] == old_state["current"]["imageId"], "Managed container differs from recorded current release")
            self.existing(old_state["current"]["commit"])
            return current, False
        require(old_state is None, "Recorded current container is missing; recover before deployment")
        fallback = self.docker.inspect(server["legacyFallbackContainer"])
        require(fallback is not None and not fallback["State"]["Running"], "First release requires an explicitly retained, stopped legacy baseline")
        require(fallback["Image"] == self.parity["legacyImageId"], "Legacy baseline image differs from reviewed source-parity baseline")
        return fallback, True

    def start(self, release, manifest):
        args = ["run", "--detach", "--name", NAME, "--network", NETWORK, "--network-alias", NAME,
                "--restart", "unless-stopped", "--user", "1654:1654", "--cap-drop", "ALL",
                "--security-opt", "no-new-privileges:true", "--log-opt", "max-size=10m", "--log-opt", "max-file=3",
                "--env-file", str(release / "runtime.env"), "--env", "ASPNETCORE_HTTP_PORTS=8080",
                "--env", f"Deployment__Commit={manifest['commit']}",
                "--label", f"{MANAGED_LABEL}={MANAGER}", "--label", f"{REVISION_LABEL}={manifest['commit']}",
                "--mount", f"type=volume,src={DP_VOLUME},dst={DP_PATH}", manifest["imageId"]]
        self.docker.run(*args)

    def restore_transaction(self):
        transaction = read_json(self.transaction_file, secret=True)
        required = {"oldState", "oldContainerId", "backupName", "legacy", "newCommit"}
        require(set(transaction) in (required, required | {"reversingLegacyContainerId", "legacyFallbackName"}), "Invalid recovery journal")
        require(isinstance(transaction["newCommit"], str) and COMMIT.fullmatch(transaction["newCommit"]), "Invalid recovery commit")
        require(re.fullmatch(r"[0-9a-f]{64}", transaction["oldContainerId"] or ""), "Invalid recovery baseline ID")
        require(re.fullmatch(r"andersonmultimarcas-(?:legacy|backup)-[a-z0-9-]{1,80}", transaction["backupName"] or ""), "Invalid recovery backup name")
        require(type(transaction["legacy"]) is bool, "Invalid recovery baseline type")
        if transaction["legacy"]:
            require(transaction["oldState"] is None, "Legacy recovery must not declare managed release state")
        else:
            require(isinstance(transaction["oldState"], dict) and
                    set(transaction["oldState"]) == {"current", "previous"}, "Invalid recovery prior state")
            reference = transaction["oldState"]["current"]
            require(isinstance(reference, dict) and isinstance(reference.get("commit"), str) and
                    COMMIT.fullmatch(reference["commit"]), "Invalid recovery prior commit")
            self.existing(reference["commit"])
        current = self.docker.inspect(NAME)
        if current and current["Id"] != transaction["oldContainerId"]:
            if current["Id"] == transaction.get("reversingLegacyContainerId"):
                fallback = transaction.get("legacyFallbackName", "")
                require(isinstance(fallback, str) and re.fullmatch(r"andersonmultimarcas-legacy-[a-z0-9-]{1,50}", fallback), "Invalid legacy recovery name")
                self.docker.run("stop", "--time", "30", NAME)
                self.docker.run("rename", NAME, fallback)
            else:
                require(self.managed(current, transaction["newCommit"]), "Unknown live container; manual recovery required")
                self.docker.run("rm", "--force", NAME)
            current = None
        if current is None:
            baseline = self.docker.inspect(transaction["backupName"])
            require(baseline is not None and baseline["Id"] == transaction["oldContainerId"], "Retained recovery baseline is missing")
            self.docker.run("rename", transaction["backupName"], NAME)
        self.docker.run("start", NAME)
        expected_commit = None if transaction["legacy"] else transaction["oldState"]["current"]["commit"]
        self.docker.health(NAME, expected_commit, self.timeout)
        if transaction["oldState"] is None:
            if self.state_file.exists():
                self.state_file.unlink()
        else:
            atomic_json(self.state_file, transaction["oldState"])
        self.history("recovery_succeeded", failedCommit=transaction["newCommit"], restoredCommit=expected_commit,
                     restoredLegacy=transaction["legacy"])
        self.transaction_file.unlink()

    def activate(self, release, manifest, server, action):
        require(not self.transaction_file.exists(), "Pending transaction exists; invoke rollback without --release to recover")
        old_state = self.state()
        old_container, legacy = self.preflight(server, old_state)
        if old_state and manifest["commit"] == old_state["current"]["commit"]:
            self.docker.health(NAME, manifest["commit"], self.timeout)
            self.history("already_current", commit=manifest["commit"])
            return
        self.docker.check_image(manifest, release / "image.tar.gz")
        backup_name = server["legacyFallbackContainer"] if legacy else f"andersonmultimarcas-backup-{uuid.uuid4().hex}"
        journal = {"oldState": old_state, "oldContainerId": old_container["Id"], "backupName": backup_name,
                   "legacy": legacy, "newCommit": manifest["commit"]}
        self.history("release_started", action=action, commit=manifest["commit"], ciRunId=manifest["ciRunId"],
                     sourceParityBaseline=self.parity["reviewedUnifiedCommit"])
        atomic_json(self.transaction_file, journal)
        try:
            if not legacy:
                self.docker.run("stop", "--time", "30", NAME)
                self.docker.run("rename", NAME, backup_name)
            self.start(release, manifest)
            self.docker.health(NAME, manifest["commit"], self.timeout)
            reference = {key: manifest[key] for key in ("commit", "imageId", "ciRunId")}
            atomic_json(self.state_file, {"current": reference, "previous": old_state["current"] if old_state else None})
            self.history("release_succeeded", action=action, **reference)
            self.transaction_file.unlink()
        except BaseException:
            # A full/unwritable history filesystem must not prevent restoring
            # the running application after a failed replacement.
            with contextlib.suppress(OSError, DeploymentError):
                self.history("release_failed", action=action, commit=manifest["commit"])
            try:
                self.restore_transaction()
            except BaseException:
                raise DeploymentError("Release failed and automatic recovery failed; retained transaction requires operator recovery") from None
            raise DeploymentError("Release failed; prior application was restored and checked") from None

    def rollback_legacy(self, server, state):
        # A public-proxy check can fail after the first managed release passed
        # its internal check. Keep that first cutover reversible as well.
        current, was_legacy = self.preflight(server, state)
        require(not was_legacy, "Expected a current managed release")
        fallback_name = server["legacyFallbackContainer"]
        baseline = self.docker.inspect(fallback_name)
        require(baseline is not None and not baseline["State"]["Running"], "Retained stopped legacy container is required")
        require(baseline["Image"] == self.parity["legacyImageId"], "Legacy fallback image differs from reviewed baseline")
        backup_name = f"andersonmultimarcas-backup-{uuid.uuid4().hex}"
        transaction = {"oldState": state, "oldContainerId": current["Id"], "backupName": backup_name,
                       "legacy": False, "newCommit": state["current"]["commit"],
                       "reversingLegacyContainerId": baseline["Id"], "legacyFallbackName": fallback_name}
        self.history("legacy_rollback_started", commit=state["current"]["commit"])
        atomic_json(self.transaction_file, transaction)
        try:
            self.docker.run("stop", "--time", "30", NAME)
            self.docker.run("rename", NAME, backup_name)
            self.docker.run("rename", fallback_name, NAME)
            self.docker.run("start", NAME)
            self.docker.health(NAME, None, self.timeout)
            self.state_file.unlink()
            self.history("legacy_rollback_succeeded", fromCommit=state["current"]["commit"], legacyImageId=baseline["Image"])
            self.transaction_file.unlink()
        except BaseException:
            try:
                self.restore_transaction()
            except BaseException:
                raise DeploymentError("Legacy rollback failed and managed recovery failed; retained transaction requires operator recovery") from None
            raise DeploymentError("Legacy rollback failed; managed application was restored and checked") from None

    def deploy(self, bundle, expected_sha):
        with self.locked():
            server = self.gates()
            require(not self.transaction_file.exists(), "Pending transaction exists; invoke rollback without --release to recover")
            release, manifest = self.snapshot(Path(bundle), expected_sha)
            self.activate(release, manifest, server, "deploy")
            print(f"Release verified: {manifest['commit']}")

    def rollback(self, commit=None):
        with self.locked():
            server = self.gates()
            if self.transaction_file.exists():
                require(commit is None, "Recover the pending transaction before choosing a rollback release")
                self.restore_transaction()
                print("Interrupted deployment recovered and checked")
                return
            state = self.state()
            require(state is not None, "No managed release is recorded")
            target = commit or (state["previous"] or {}).get("commit")
            if target is None:
                self.rollback_legacy(server, state)
                print("Legacy baseline restored and checked")
                return
            release, manifest = self.existing(target)
            self.activate(release, manifest, server, "rollback")
            print(f"Rollback verified: {manifest['commit']}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="operation", required=True)
    for operation in ("deploy", "rollback"):
        command = subparsers.add_parser(operation)
        command.add_argument("--root", type=Path, required=True)
        command.add_argument("--health-timeout", type=int, default=90)
        if operation == "deploy":
            command.add_argument("--bundle", type=Path, required=True)
            command.add_argument("--manifest-sha256", required=True)
        else:
            command.add_argument("--release")
    args = parser.parse_args()
    require(1 <= args.health_timeout <= 600, "Health timeout must be between 1 and 600 seconds")
    signal.signal(signal.SIGTERM, lambda *_: (_ for _ in ()).throw(DeploymentError("Deployment interrupted")))
    manager = ReleaseManager(args.root, health_timeout=args.health_timeout)
    if args.operation == "deploy":
        manager.deploy(args.bundle, args.manifest_sha256)
    else:
        manager.rollback(args.release)


if __name__ == "__main__":
    try:
        main()
    except (DeploymentError, OSError, ValueError, KeyError) as error:
        # Do not print OS errors containing arbitrary paths or subprocess output.
        message = str(error) if isinstance(error, DeploymentError) else f"Local operation failed ({type(error).__name__})"
        print(f"ERROR: {message}", file=sys.stderr)
        sys.exit(1)
