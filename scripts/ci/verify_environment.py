#!/usr/bin/env python3
"""Read-only, fail-closed verification of GitHub production automatic deployment rules."""
import json
import os
import urllib.request


def validate(environment, policies):
    if environment.get("can_admins_bypass") is not False:
        raise ValueError("Administrators must not bypass production protection rules")
    rules = environment.get("protection_rules", [])
    if environment.get("name") != "production":
        raise ValueError("Unexpected Environment")
    if any(r.get("type") == "required_reviewers" for r in rules):
        raise ValueError("Automatic deployment must not require per-release reviewers")
    if environment.get("deployment_branch_policy") != {"protected_branches": False, "custom_branch_policies": True}:
        raise ValueError("Use a custom production branch policy restricted to main")
    branches = policies.get("branch_policies", [])
    if policies.get("total_count") != 1 or len(branches) != 1:
        raise ValueError("Allow exactly one deployment branch rule")
    if branches[0].get("name") != "main" or branches[0].get("type") != "branch":
        raise ValueError("Only the main branch may deploy")


def main():
    # Keep explicit authorization in addition to checking the actual API state.
    # The audited GitHub response exposes can_admins_bypass; fail closed if absent.
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
    request = urllib.request.Request(
        f"https://api.github.com/repos/{repository}/branches/main",
        headers={"Authorization": "Bearer " + os.environ["GH_TOKEN"],
                 "Accept": "application/vnd.github+json", "User-Agent": "Anderson-deployment-preflight"})
    with urllib.request.urlopen(request, timeout=30) as response:
        if json.load(response).get("protected") is not True:
            raise ValueError("main must be protected before automatic deployment")
    print("production automatic deployment rules verified (read-only).")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        raise SystemExit(f"Environment preflight refused: {type(exc).__name__}; verify automatic policy, bypass and main protection.") from None
