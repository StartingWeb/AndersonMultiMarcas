#!/usr/bin/env python3
"""Render a private future runtime connection file; never connect to SQL or install it."""
import argparse
import os
from pathlib import Path
import re
import stat


def connection(password, host, database):
    if (len(password)<24 or any(ord(c)<32 for c in password)
            or not re.fullmatch(r'[A-Za-z0-9_.-]+(?:,[0-9]{1,5})?',host)
            or database!='AndersonMultiMarcas'):
        raise ValueError('Invalid private credential or audited SQL destination')
    # ADO.NET quoted connection values, not shell quoting. Docker env-file is literal.
    quoted='"'+password.replace('"','""')+'"'
    return ('ConnectionStrings__DefaultConnectionProd=Server='+host+';Database='+database+
            ';User Id=anderson_runtime;Password='+quoted+';Encrypt=True;TrustServerCertificate=True;\n')


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--password-file',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--host',required=True)
    args=parser.parse_args();os.umask(0o077)
    secret=args.password_file
    info=secret.lstat()
    if not stat.S_ISREG(info.st_mode) or info.st_mode & 0o077 or info.st_uid!=os.geteuid():
        raise ValueError('Password file must be a private owned regular file')
    if any(p.is_symlink() for p in secret.parents):raise ValueError('Symlink credential parent refused')
    output=args.output
    repository=Path(__file__).resolve().parents[2]
    if output.resolve().is_relative_to(repository):raise ValueError('Credential output must remain outside the checkout')
    parent=output.parent.lstat()
    if not stat.S_ISDIR(parent.st_mode) or parent.st_mode & 0o077 or parent.st_uid!=os.geteuid():
        raise ValueError('Output directory must be private and owned')
    if any(p.is_symlink() for p in output.parents):raise ValueError('Symlink output parent refused')
    value=connection(secret.read_text().rstrip('\n'),args.host,'AndersonMultiMarcas')
    # O_EXCL prevents overwriting any credential or a symlink. Nothing is installed.
    fd=os.open(output,os.O_CREAT|os.O_EXCL|os.O_WRONLY,0o600)
    with os.fdopen(fd,'w') as stream:stream.write(value);stream.flush();os.fsync(stream.fileno())
    print('Private connection proposal created; no SQL connection or production installation performed.')


if __name__=='__main__':
    try:main()
    except Exception as error:raise SystemExit('Runtime credential preparation refused: '+type(error).__name__) from None
