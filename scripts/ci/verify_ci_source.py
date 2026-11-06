#!/usr/bin/env python3
"""Refuse packaging without a successful push/main CI from this repository."""
import json
import os
from pathlib import Path
import re
import subprocess

REPOSITORY = 'StartingWeb/AndersonMultiMarcas'


def validate(event, source, checked_out, main_head):
    run = event.get('workflow_run', {})
    repository = event.get('repository', {})
    if (repository.get('full_name') != REPOSITORY or not repository.get('id')
            or run.get('head_repository', {}).get('id') != repository['id']
            or run.get('conclusion') != 'success' or run.get('event') != 'push'
            or run.get('head_branch') != 'main'):
        raise ValueError('Only successful push/main CI from the audited repository may be packaged')
    if (not re.fullmatch('[a-f0-9]{40}', source or '')
            or source != run.get('head_sha') or source != checked_out or source != main_head):
        raise ValueError('CI source, checkout and current main must match')


def main():
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text())
    def revision(ref):
        return subprocess.check_output(['git', 'rev-parse', ref], text=True).strip()
    validate(event, os.environ['SOURCE_COMMIT'], revision('HEAD'), revision('origin/main'))
    print('Successful CI and current main source verified; no deployment performed.')


if __name__ == '__main__':
    main()
