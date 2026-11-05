#!/usr/bin/env python3
"""Create/verify a checksummed publish tree tied to a trusted CI run."""
import argparse
import hashlib
import json
import re
from pathlib import Path


def digest(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()


def tree(root):
    files = {}
    for path in root.rglob("*"):
        if path.is_symlink():
            raise ValueError("Symlinks are not accepted in a publish artifact")
        if path.is_file():
            files[path.relative_to(root).as_posix()] = digest(path)
    if "Project.dll" not in files:
        raise ValueError("Missing application assembly")
    return files


def manifest(application, commit, run_id):
    if not re.fullmatch(r"[0-9a-f]{40}", commit) or not re.fullmatch(r"[1-9][0-9]*", run_id):
        raise ValueError("Invalid source identity")
    return {"schemaVersion": 1, "repository": "StartingWeb/AndersonMultiMarcas", "commit": commit,
            "ciRunId": run_id, "files": tree(application / "publish")}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=["create", "verify"])
    parser.add_argument("--application", type=Path, required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--run-id", required=True)
    args = parser.parse_args()
    expected = manifest(args.application, args.commit, args.run_id)
    path = args.application / "publish-manifest.json"
    if args.operation == "create":
        with path.open("x") as stream:
            json.dump(expected, stream, sort_keys=True, indent=2)
    elif json.loads(path.read_text()) != expected:
        raise SystemExit("Publish manifest/source/checksum mismatch")
    print(f"Application {args.operation}: {len(expected['files'])} files verified")


if __name__ == "__main__":
    main()
