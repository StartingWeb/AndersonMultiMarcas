#!/usr/bin/env python3
"""Read-only, fail-closed verification of GitHub production approval rules."""
import json
import os
import urllib.request


def validate(environment, policies):
    rules = environment.get("protection_rules", [])
    reviewers = [r for r in rules if r.get("type") == "required_reviewers"]
    if environment.get("name") != "production" or not reviewers:
        raise ValueError("production must exist and require reviewers")
    if not any(r.get("reviewers") and r.get("prevent_self_review") is True for r in reviewers):
        raise ValueError("Require at least one reviewer and prevent self approval")
    if environment.get("deployment_branch_policy") != {"protected_branches": False, "custom_branch_policies": True}:
        raise ValueError("Use a custom production branch policy restricted to main")
    branches = policies.get("branch_policies", [])
    if policies.get("total_count") != 1 or len(branches) != 1:
        raise ValueError("Allow exactly one deployment branch rule")
    if branches[0].get("name") != "main" or branches[0].get("type") != "branch":
        raise ValueError("Only the main branch may deploy")


def main():
    # The REST environment schema does not expose the administrator-bypass UI
    # setting. Require its separately reviewed attestation; do not invent an API field.
    if os.environ.get("PRODUCTION_ADMIN_BYPASS_REVIEWED") != "true":
        raise ValueError("Administrator bypass setting must be manually reviewed before activation")
    repository = os.environ["GITHUB_REPOSITORY"]
    if repository != "StartingWeb/AndersonMultiMarcas":
        raise ValueError("Unexpected deployment repository")
    def read(suffix):
        request = urllib.request.Request(
            f"https://api.github.com/repos/{repository}/environments/production{suffix}",
            headers={"Authorization": "Bearer " + os.environ["GH_TOKEN"], "Accept": "application/vnd.github+json",
                     "X-GitHub-Api-Version": "2022-11-28", "User-Agent": "Anderson-deployment-preflight"})
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)
    validate(read(""), read("/deployment-branch-policies?per_page=100"))
    print("production approval rules verified (read-only).")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        raise SystemExit(f"Environment preflight refused: {type(exc).__name__}; verify required reviewers, bypass and main policy.") from None
