#!/usr/bin/env python3
"""Check the real runtime image offline, including native WebP conversion.

Only random-prefixed containers are created. Network is disabled and the database
connection is synthetic; no SQL/R2 service is used or reachable by this test.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile
import time
import uuid


def docker(*args, required=True):
    result = subprocess.run(["docker", *args], capture_output=True, check=False)
    if required and result.returncode:
        raise RuntimeError("Disposable runtime check failed: docker " + args[0])
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    if not re.fullmatch(r"[0-9a-f]{40}", args.commit):
        parser.error("Expected a full source commit")
    prefix = "amm-runtime-" + uuid.uuid4().hex[:12]
    names = []
    report = {"run": prefix, "network": "none", "scenarios": []}
    image = json.loads(docker("image", "inspect", args.image).stdout)[0]
    if image["Architecture"] != "amd64" or image["Os"] != "linux" or image["Config"]["User"] != "app":
        raise RuntimeError("Expected the linux/amd64 non-root production image")
    if image["Config"].get("Labels", {}).get("org.opencontainers.image.revision") != args.commit:
        raise RuntimeError("Runtime image source label mismatch")
    report["imageId"] = image["Id"]
    report["commit"] = args.commit
    environment = ["--env", "ConnectionStrings__DefaultConnectionProd=Server=synthetic.invalid;Database=Synthetic;User Id=unused;Password=SyntheticOnly1!;Encrypt=False",
                   "--env", "Storage__Provider=Local", "--env", "Deployment__Commit=" + args.commit]

    def start(suffix, extra=()):
        name = prefix + "-" + suffix
        names.append(name)
        docker("run", "--detach", "--name", name, "--network", "none", "--user", "1654:1654",
               "--cap-drop", "ALL", "--security-opt", "no-new-privileges:true", *environment, *extra, args.image)
        return name

    def curl(name, path, *extra):
        return docker("exec", name, "curl", "--fail", "--silent", "--show-error", "--max-time", "4",
                      "--header", "X-Forwarded-Proto: https", *extra,
                      "http://127.0.0.1:8080" + path, required=False)

    def passed(scenario):
        report["scenarios"].append({"scenario": scenario, "status": "passed"})
        print("PASS: " + scenario, flush=True)

    try:
        name = start("application")
        for _ in range(60):
            health = curl(name, "/health")
            if health.returncode == 0:
                break
            state = json.loads(docker("container", "inspect", name).stdout)[0]["State"]
            if not state["Running"]:
                raise RuntimeError("Application exited before health readiness")
            time.sleep(1)
        else:
            raise RuntimeError("Application health readiness timed out")
        passed("production_startup_and_health_without_database_or_network")
        identity = curl(name, "/health/release")
        if identity.returncode or json.loads(identity.stdout).get("commit") != args.commit:
            raise RuntimeError("Application release health returned the wrong commit")
        passed("release_health_matches_source")
        static = curl(name, "/img/carroDefault.png")
        if static.returncode or not static.stdout.startswith(b"\x89PNG\r\n\x1a\n"):
            raise RuntimeError("Published static fallback image is missing")
        report["staticImageSha256"] = hashlib.sha256(static.stdout).hexdigest()
        passed("published_static_image")
        media = curl(name, "/media/img?src=/img/carroDefault.png&w=400&q=70", "--header", "Accept: image/webp",
                     "--dump-header", "/tmp/amm-runtime-headers", "--output", "/tmp/amm-runtime-image.webp")
        if media.returncode:
            raise RuntimeError("Native image optimization endpoint failed")
        headers = docker("exec", name, "cat", "/tmp/amm-runtime-headers").stdout.lower()
        if b"content-type: image/webp" not in headers:
            raise RuntimeError("Native image endpoint returned no WebP payload")
        with tempfile.TemporaryDirectory(prefix=prefix) as directory:
            output = Path(directory) / "optimized.webp"
            docker("cp", name + ":/tmp/amm-runtime-image.webp", str(output))
            payload = output.read_bytes()
        if len(payload) < 12 or payload[:4] != b"RIFF" or payload[8:12] != b"WEBP":
            raise RuntimeError("Native image output has invalid WebP signature")
        report["webpBytes"] = len(payload)
        report["webpSha256"] = hashlib.sha256(payload).hexdigest()
        passed("native_magick_webp_conversion")
        unsafe = start("migration-guard", ["--env", "Database__ApplyMigrationsOnStartup=true"])
        for _ in range(30):
            state = json.loads(docker("container", "inspect", unsafe).stdout)[0]["State"]
            if not state["Running"]:
                if state["ExitCode"] == 0:
                    raise RuntimeError("Unsafe migration opt-in exited successfully")
                logs = docker("logs", unsafe)
                if b"Migrations no startup sao permitidas somente em Development" not in logs.stdout + logs.stderr:
                    raise RuntimeError("Startup failed for a reason other than the migration guard")
                break
            time.sleep(1)
        else:
            raise RuntimeError("Production accepted automatic migrations")
        passed("production_refuses_automatic_migrations")
        report["status"] = "passed"
    except BaseException as error:
        report["status"] = "failed"
        report["errorType"] = type(error).__name__
        raise
    finally:
        for name in names:
            docker("rm", "--force", name, required=False)
        remaining = [name for name in names if docker("container", "inspect", name, required=False).returncode == 0]
        report["remainingContainers"] = remaining
        if args.report:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
        if remaining:
            raise RuntimeError("Disposable runtime containers remain after cleanup")
        print("PASS: disposable runtime containers removed and absence verified", flush=True)


if __name__ == "__main__":
    main()
