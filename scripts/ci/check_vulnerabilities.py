#!/usr/bin/env python3
"""Fail closed for invalid audit output and High/Critical NuGet advisories."""
import argparse
import json
from pathlib import Path


def findings(document):
    if not isinstance(document, dict) or not isinstance(document.get("projects"), list) or not document["projects"] or document.get("problems"):
        raise ValueError("Missing project audit results")
    found = set()
    for project in document["projects"]:
        # NuGet omits frameworks entirely when a project has no findings.
        if not project.get("path"):
            raise ValueError("Missing project identity")
        for framework in project.get("frameworks", []):
            for scope in ("topLevelPackages", "transitivePackages"):
                for package in framework.get(scope, []):
                    for advisory in package.get("vulnerabilities", []):
                        severity = advisory["severity"].lower()
                        if severity not in {"low", "moderate", "high", "critical"}:
                            raise ValueError("Unknown vulnerability severity")
                        found.add((package["id"], package["resolvedVersion"], severity, advisory["advisoryurl"]))
    return sorted(found)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reports", nargs="+", type=Path)
    args = parser.parse_args()
    try:
        result = sorted(set(f for path in args.reports for f in findings(json.loads(path.read_text()))))
    except (OSError, ValueError, KeyError, TypeError) as exc:
        parser.exit(2, f"NuGet audit unavailable or invalid: {type(exc).__name__}\n")
    for item in result:
        print(json.dumps(dict(zip(("package", "version", "severity", "advisory"), item))))
    blocked = sum(f[2] in {"high", "critical"} for f in result)
    print(f"Unique advisories: {len(result)}; High/Critical: {blocked}")
    return 1 if blocked else 0


if __name__ == "__main__":
    raise SystemExit(main())
