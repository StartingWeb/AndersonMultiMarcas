#!/usr/bin/env python3
"""Manage only this repository's disposable SQL Server; credentials stay in ignored .cache."""
import argparse
import json
import os
from pathlib import Path
import secrets
import subprocess
import time

ROOT = Path(__file__).resolve().parents[2]
STATE = ROOT / '.cache' / 'local-sql'
NAME = 'anderson-local-sql'
IMAGE = 'mcr.microsoft.com/mssql/server:2022-latest@sha256:cf8f0ab2ebe61feec2eed2629061a07d247f791c2a8ddda74a4ac9f06a291718'


def run(*args, **kwargs):
    return subprocess.run(args, check=True, text=True, **kwargs)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['start', 'stop', 'run', 'test', 'seed', 'browser'])
    args = parser.parse_args()
    config_path = STATE / 'config.json'
    if args.action == 'start':
        if config_path.exists():
            raise SystemExit('Local state already exists. Use stop before creating another disposable database.')
        STATE.mkdir(parents=True, exist_ok=True)
        os.chmod(STATE, 0o700)
        password = 'Aa1!' + secrets.token_urlsafe(30)
        envfile = STATE / 'sql.env'
        envfile.write_text(f'ACCEPT_EULA=Y\nMSSQL_PID=Developer\nMSSQL_SA_PASSWORD={password}\nSQLCMDPASSWORD={password}\n')
        envfile.chmod(0o600)
        run('docker', 'run', '-d', '--platform', 'linux/amd64', '--name', NAME,
            '--label', 'anderson.purpose=disposable-local', '--env-file', str(envfile),
            '-p', '127.0.0.1::1433', IMAGE, stdout=subprocess.DEVNULL)
        data = json.loads(subprocess.check_output(['docker', 'inspect', NAME]))[0]
        port = data['NetworkSettings']['Ports']['1433/tcp'][0]['HostPort']
        connection = f'Server=127.0.0.1,{port};Database=AndersonLocal;User Id=sa;Password={password};TrustServerCertificate=True;Encrypt=True'
        config_path.write_text(json.dumps({'connection': connection, 'containerId': data['Id'],
            'bootstrapPassword': 'Aa1!' + secrets.token_urlsafe(24)}))
        config_path.chmod(0o600)
        for _ in range(120):
            ready = subprocess.run(['docker','exec', NAME, '/opt/mssql-tools18/bin/sqlcmd', '-S','localhost','-U','sa','-C','-b','-Q','SELECT 1'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            if ready.returncode == 0:
                print('SQL Server ready on loopback. Run: python3 scripts/dev/local_sql.py run')
                return
            time.sleep(1)
        raise SystemExit('SQL Server readiness timeout; inspect docker logs anderson-local-sql.')
    if not config_path.exists():
        raise SystemExit('Run start first.')
    config = json.loads(config_path.read_text())
    data = json.loads(subprocess.check_output(['docker', 'inspect', NAME]))[0]
    if ((data['Config'].get('Labels') or {}).get('anderson.purpose') != 'disposable-local'
            or data['Id'] != config.get('containerId')):
        raise SystemExit('Refusing to use or remove a container that differs from this local state.')
    if args.action == 'stop':
        run('docker', 'rm', '-f', NAME, stdout=subprocess.DEVNULL)
        for filename in ('config.json', 'sql.env'):
            (STATE / filename).unlink(missing_ok=True)
        if not any(STATE.iterdir()): STATE.rmdir()
        print('Disposable SQL Server removed.')
        return
    env = os.environ.copy()
    env.update({'ANDERSON_TEST_SQL': config['connection'],
                'ConnectionStrings__DefaultConnectionDev': config['connection'],
                'ASPNETCORE_ENVIRONMENT': 'Development', 'DOTNET_ENVIRONMENT': 'Development',
                'ASPNETCORE_URLS': 'http://127.0.0.1:5185', 'StorageMigration__Execute': 'false',
                'Database__ApplyMigrationsOnStartup': 'true', 'Storage__Provider': 'Local',
                'DevelopmentBootstrap__Enabled': 'true', 'DevelopmentBootstrap__UserName': 'local.developer',
                'DevelopmentBootstrap__Email': 'developer@example.invalid',
                'DevelopmentBootstrap__Password': config['bootstrapPassword'],
                'LegacyImageImport__RecoverPendingJobsOnStartup': 'false',
                'Seo__BaseUrl': 'http://127.0.0.1:5185'})
    if args.action == 'seed':
        sql = (ROOT / 'scripts/functional/synthetic_catalog.sql').read_text()
        run('docker', 'exec', '-i', NAME, '/opt/mssql-tools18/bin/sqlcmd', '-S', 'localhost',
            '-U', 'sa', '-C', '-I', '-b', '-d', 'AndersonLocal', input=sql)
    elif args.action == 'browser':
        env['ANDERSON_E2E_PASSWORD'] = config['bootstrapPassword']
        run(str(ROOT / '.cache/browser-venv/bin/python'), 'scripts/functional/browser_smoke.py', env=env, cwd=ROOT)
    elif args.action == 'test':
        run('dotnet','test','Project.Tests/Project.Tests.csproj','-c','Release',env=env,cwd=ROOT)
    else:
        print('Local login: local.developer. Generated password is in .cache/local-sql/config.json (never commit).')
        run('dotnet','run','--project','Project/Project.csproj','-c','Release','--no-launch-profile',env=env,cwd=ROOT)


if __name__ == '__main__':
    main()
