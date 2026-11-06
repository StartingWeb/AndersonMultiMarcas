#!/usr/bin/env python3
"""Validate GitHub Actions YAML and embedded shell with pinned actionlint."""
import hashlib
import io
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tarfile
import urllib.request


VERSION = "1.7.12"
LINUX_SHA256 = "8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8"


def main():
    binary = shutil.which("actionlint")
    if not binary:
        if platform.system() != "Linux" or platform.machine() not in {"x86_64", "AMD64"}:
            raise SystemExit("Install a checksum-verified actionlint in PATH for this platform; no global installation is performed.")
        cache = Path(".cache/cicd-tools")
        cache.mkdir(parents=True, exist_ok=True)
        target = cache / f"actionlint-{VERSION}"
        url = f"https://github.com/rhysd/actionlint/releases/download/v{VERSION}/actionlint_{VERSION}_linux_amd64.tar.gz"
        with urllib.request.urlopen(url, timeout=60) as response:
            payload = response.read()
        if hashlib.sha256(payload).hexdigest() != LINUX_SHA256:
            raise SystemExit("actionlint download checksum mismatch")
        with tarfile.open(fileobj=io.BytesIO(payload), mode="r:gz") as archive:
            member = archive.getmember("actionlint")
            if not member.isfile():
                raise SystemExit("Unexpected actionlint archive entry")
            target.write_bytes(archive.extractfile(member).read())
        target.chmod(0o755)
        binary = str(target.resolve())
    if not shutil.which("shellcheck"):
        raise SystemExit("shellcheck is required in PATH to validate embedded scripts")
    return subprocess.call([binary, "-shellcheck=shellcheck"])


if __name__ == "__main__":
    raise SystemExit(main())
