#!/usr/bin/env python3
"""Forced command for the dedicated CI SSH key; never execute caller shell code.

Uploads use legacy SCP's receive-only protocol inside fresh incoming directories.
Only the locally installed, reviewed executor can run. SFTP, reads, arbitrary
Python, forwarding and shell access are unavailable to this key.
"""
import hashlib
import json
import os
from pathlib import Path
import re
import resource
import shlex
import stat
import subprocess
import sys

ROOT = Path('/opt/startingweb/apps/anderson-multimarcas-unified')
IDENTITY = r'[a-f0-9]{40}-[1-9][0-9]{0,19}-[1-9][0-9]{0,5}'
VERIFY_CODE = (
    "import hashlib,runpy,sys; p=sys.argv[1]; expected=sys.argv[2]; "
    "actual=hashlib.sha256(open(p,'rb').read()).hexdigest(); "
    "assert actual==expected, 'Deployment script checksum mismatch'; "
    "sys.argv=[p]+sys.argv[3:]; runpy.run_path(p,run_name='__main__')")


def private_directory(path):
    value = path.lstat()
    if (not stat.S_ISDIR(value.st_mode) or value.st_mode & 0o077
            or value.st_uid != os.geteuid() or any(p.is_symlink() for p in path.parents)):
        raise ValueError('Unsafe deployment directory')


def destination(value):
    path = Path(value.rstrip('/'))
    if path.parent != ROOT / 'incoming' or not re.fullmatch(IDENTITY, path.name):
        raise ValueError('Destination outside approved incoming scope')
    private_directory(ROOT)
    private_directory(ROOT / 'incoming')
    return path


def classify(command):
    if command == 'anderson-preflight':
        return 'preflight', None
    prefix = f'umask 077; test -d {ROOT}/incoming && mkdir '
    if command.startswith(prefix):
        path = destination(command[len(prefix):])
        if command != prefix + str(path):
            raise ValueError('Unexpected mkdir syntax')
        return 'mkdir', path
    args = shlex.split(command)
    if len(args) == 3 and args[:2] == ['scp', '-t']:
        path = destination(args[2])
        private_directory(path)
        return 'upload', path
    if (len(args) not in (8, 12) or args[:3] != ['python3', '-c', VERIFY_CODE]
            or args[5:8] not in (['deploy', '--root', str(ROOT)], ['rollback', '--root', str(ROOT)])):
        raise ValueError('Command is not part of the reviewed delivery protocol')
    script = Path(args[3])
    path = destination(str(script.parent))
    private_directory(path)
    if script.name != 'release.py' or not re.fullmatch(r'[a-f0-9]{64}', args[4]):
        raise ValueError('Invalid executor identity')
    if args[5] == 'deploy':
        if (len(args) != 12 or args[8:10] != ['--bundle', str(path)]
                or args[10] != '--manifest-sha256' or not re.fullmatch(r'[a-f0-9]{64}', args[11])):
            raise ValueError('Invalid deployment arguments')
    elif len(args) != 8:
        raise ValueError('Invalid rollback arguments')
    return 'executor', args


def execute(command):
    operation, value = classify(command)
    if operation == 'preflight':
        print(json.dumps({'ssh_key_scope': 'anderson-unified-delivery-only',
                          'deploy_enabled': (ROOT / 'shared/deploy.enabled').exists(),
                          'source_parity_approved': (ROOT / 'shared/source-parity.approved.json').exists()}))
        return 0
    if operation == 'mkdir':
        value.mkdir(mode=0o700)
        return 0
    if operation == 'upload':
        # Bound each received file; archives larger than this require review.
        resource.setrlimit(resource.RLIMIT_FSIZE, (768 * 1048576, 768 * 1048576))
        return subprocess.run(['/usr/bin/scp', '-t', str(value)], timeout=900).returncode
    installed = ROOT / 'bin/release.py'
    expected = hashlib.sha256(installed.read_bytes()).hexdigest()
    received = Path(value[3])
    info = received.lstat()
    if (not stat.S_ISREG(info.st_mode) or info.st_mode & 0o022
            or value[4] != expected or hashlib.sha256(received.read_bytes()).hexdigest() != expected):
        raise ValueError('Transferred executor differs from the installed reviewed version')
    # Ignore the caller-supplied Python. Run only the installed executor.
    return subprocess.run(['/usr/bin/python3', '-I', '-B', str(installed), *value[5:]], timeout=1500).returncode


if __name__ == '__main__':
    os.umask(0o077)
    try:
        raise SystemExit(execute(os.environ.get('SSH_ORIGINAL_COMMAND', '')))
    except Exception:
        print('SSH command refused by the Anderson delivery policy.', file=sys.stderr)
        raise SystemExit(1) from None
