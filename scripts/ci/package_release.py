#!/usr/bin/env python3
"""Export an already-built Docker image as a versioned, checksummed release."""
import argparse
import gzip
import json
import re
import subprocess
from pathlib import Path
from application_manifest import digest


def verify_runtime_report(path, image, commit):
    """Bind the offline runtime checks to the exact image being exported."""
    report = json.loads(path.read_text(encoding="utf-8"))
    required = {
        "production_startup_and_health_without_database_or_network",
        "release_health_matches_source", "published_static_image",
        "native_magick_webp_conversion", "production_refuses_automatic_migrations",
    }
    if (not isinstance(report, dict) or report.get("status") != "passed"
            or report.get("network") != "none" or report.get("commit") != commit
            or report.get("imageId") != image["Id"]
            or report.get("remainingContainers") != []):
        raise ValueError("Final image runtime evidence is missing, failed or mismatched")
    scenarios = report.get("scenarios", [])
    if (not isinstance(scenarios, list) or not scenarios
            or any(not isinstance(item, dict) or item.get("status") != "passed" for item in scenarios)
            or not required.issubset({item.get("scenario") for item in scenarios})):
        raise ValueError("Final image runtime evidence is incomplete")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--runtime-report", type=Path, required=True)
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
    if image["Config"].get("User") != "app":
        parser.error("Production requires the non-root application user")
    verify_runtime_report(args.runtime_report, image, args.commit)
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
