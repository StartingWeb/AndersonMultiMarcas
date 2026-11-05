#!/usr/bin/env python3
"""Export an already-built Docker image as a versioned, checksummed release."""
import argparse
import gzip
import json
import re
import subprocess
from pathlib import Path
from application_manifest import digest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if not re.fullmatch(r"[0-9a-f]{40}", args.commit) or not re.fullmatch(r"[1-9][0-9]*", args.run_id):
        parser.error("Invalid source identity")
    tag = "andersonmultimarcas-unified:" + args.commit
    image = json.loads(subprocess.check_output(["docker", "image", "inspect", tag], text=True))[0]
    if image["Config"].get("Labels", {}).get("org.opencontainers.image.revision") != args.commit:
        parser.error("Image revision mismatch")
    if image["Os"] != "linux" or image["Architecture"] != "amd64":
        parser.error("Production requires linux/amd64")
    args.output.mkdir(parents=True, exist_ok=False)
    target = args.output / "image.tar.gz"
    with target.open("xb") as raw, gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0) as archive:
        process = subprocess.Popen(["docker", "image", "save", tag], stdout=subprocess.PIPE)
        try:
            while chunk := process.stdout.read(1024 * 1024):
                archive.write(chunk)
            if process.wait() != 0:
                raise RuntimeError("docker image save failed")
        finally:
            process.stdout.close()
            if process.poll() is None:
                process.kill()
                process.wait()
    metadata = {"schemaVersion": 1, "commit": args.commit, "imageId": image["Id"], "imageTag": tag,
                "ciRunId": args.run_id, "repository": "StartingWeb/AndersonMultiMarcas"}
    (args.output / "release.json").write_text(json.dumps(metadata, sort_keys=True, indent=2) + "\n")
    (args.output / "SHA256SUMS").write_text("".join(f"{digest(args.output / name)}  {name}\n" for name in ("image.tar.gz", "release.json")))
    print(f"Release bundle prepared for {args.commit}")


if __name__ == "__main__":
    main()
