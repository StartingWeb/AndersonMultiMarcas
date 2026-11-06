#!/usr/bin/env python3
"""Isolated Anderson SQL backups, encrypted Restic retention and bounded SMTP.

Adapted from the audited TS Compro Ouro architecture. No production restore,
schema changes, offsite destination or SMTP recipient is inferred.
"""
import argparse
from contextlib import contextmanager
from datetime import datetime, timedelta, timezone
from email.message import EmailMessage
from email.utils import parseaddr
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import signal
import shutil
import smtplib
import ssl
import stat
import subprocess
import tempfile
import time
from zoneinfo import ZoneInfo

ROOT = Path('/opt/startingweb/backups/anderson-multimarcas')
DATABASE = 'AndersonMultiMarcas'
SQL_CONTAINER = 'sqlserver'
TAG = 'andersonmultimarcas'
HOST = 'startingweb-anderson'
ZONE = ZoneInfo('America/Sao_Paulo')
SQL_DIR = '/var/opt/mssql/backup/anderson-unified'
CONFIG_SOURCE = Path('/etc/tscomproouro-backup/config.json')


class BackupError(RuntimeError):
    """Only constant, non-sensitive messages may leave the process."""


def now():
    return datetime.now(timezone.utc)


def next_execution(current=None):
    current = (current or now()).astimezone(ZONE)
    target = current.replace(hour=3, minute=0, second=0, microsecond=0)
    return (target if target > current else target + timedelta(days=1)).isoformat()


def private(path, directory=False):
    info = path.lstat()
    kind = stat.S_ISDIR if directory else stat.S_ISREG
    if (not kind(info.st_mode) or info.st_uid != os.geteuid() or info.st_mode & 0o077
            or any(p.is_symlink() for p in path.parents)):
        raise BackupError('Private path has unexpected type, owner or permissions.')
    return path


def atomic_json(path, value):
    descriptor, name = tempfile.mkstemp(prefix='.write-', dir=path.parent)
    try:
        with os.fdopen(descriptor, 'w') as stream:
            os.fchmod(stream.fileno(), 0o600)
            json.dump(value, stream, ensure_ascii=False, indent=2, sort_keys=True)
            stream.write('\n')
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(name, path)
        descriptor = os.open(path.parent, os.O_RDONLY)
        try:
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
    finally:
        if os.path.exists(name):
            os.unlink(name)


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(block)
    return result.hexdigest()


def execute(args, *, data=None, env=None, timeout=3600, cwd=None):
    # Never echo SQL, process output, env-files or provider responses.
    result = subprocess.run(args, input=data, capture_output=True, text=True,
                            env=env, timeout=timeout, cwd=cwd)
    if result.returncode:
        raise BackupError('External operation failed; sensitive output suppressed.')
    return result.stdout


@contextmanager
def lock(path):
    descriptor = os.open(path, os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
    try:
        private(path)
        try:
            fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise BackupError('Another Anderson backup operation is active.') from None
        yield
    finally:
        os.close(descriptor)


def load_config():
    private(ROOT, directory=True)
    value = json.loads(private(ROOT / 'config.json').read_text())
    if (value['database'] != DATABASE or value['sql_container'] != SQL_CONTAINER
            or value['retention'] != {'daily': 7, 'weekly': 4, 'monthly': 3}
            or value.get('storage_policy') != 'local_only'
            or value.get('smtp_source') != str(CONFIG_SOURCE)):
        raise BackupError('Configuration differs from the audited project scope.')
    for name in ('state', 'work', 'native', 'repository', 'logs'):
        private(ROOT / name, directory=True)
    return value


def sql(query):
    return execute(['docker', 'exec', '-i', SQL_CONTAINER, 'sh', '-c',
                    'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec '
                    '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b -h -1 -W -w 65535'],
                   data='SET NOCOUNT ON;\n' + query, timeout=1200)


class Repository:
    def __init__(self, config):
        self.config = config
        self.env = {k: os.environ[k] for k in ('PATH', 'LANG') if k in os.environ}
        self.env.update(RESTIC_PASSWORD_FILE=str(private(ROOT / 'repository.password')),
                        RESTIC_CACHE_DIR=str(ROOT / 'state' / 'cache'),
                        GOMAXPROCS='1', TZ='America/Sao_Paulo')

    def command(self, *args, cwd=None):
        return execute(['/usr/bin/restic', '--repo', str(ROOT / 'repository'),
                        '--compression', 'auto', *args], env=self.env, cwd=cwd)

    def identity(self):
        identifier = json.loads(self.command('cat', 'config'))['id']
        if identifier != self.config['repository_id']:
            raise BackupError('Encrypted repository identity differs from configuration.')

    def snapshots(self):
        return json.loads(self.command('snapshots', '--json', '--host', HOST, '--tag', TAG)) or []

    def retain(self, newest):
        verified = {s['id'] for s in self.snapshots() if 'verified' in s.get('tags', [])}
        plan = json.loads(self.command('forget', '--dry-run', '--json', '--host', HOST,
                                      '--tag', TAG + ',verified', '--group-by', 'host',
                                      '--keep-daily', '7', '--keep-weekly', '4', '--keep-monthly', '3'))
        keep, remove = set(), set()
        for group in plan or []:
            keep.update(s['id'] for s in group.get('keep') or [])
            remove.update(s['id'] for s in group.get('remove') or [])
        if not keep or newest not in keep or keep & remove or keep | remove != verified:
            raise BackupError('Unsafe retention plan; no snapshot removed.')
        # Authenticate every retained byte before deleting any snapshot.
        self.command('check', '--read-data')
        if remove:
            self.command('forget', *sorted(remove))
            self.command('prune', '--max-repack-size', '256M')
            self.command('check', '--read-data')
        remaining = {s['id'] for s in self.snapshots() if 'verified' in s.get('tags', [])}
        if remaining != keep:
            raise BackupError('Post-retention inventory differs from the reviewed plan.')
        return len(remaining)


def read_smtp_source(path):
    # Reuse the existing root-owned SMTP configuration without copying credentials.
    # The helper has no network or Docker logging and mounts only that config tree.
    if path.parent != CONFIG_SOURCE.parent or path.name in ('', '.', '..'):
        raise BackupError('SMTP credential must remain in the audited configuration tree.')
    return execute(['docker', 'run', '--rm', '--network', 'none', '--read-only',
                    '--log-driver', 'none', '--cap-drop', 'ALL', '--security-opt',
                    'no-new-privileges:true', '--mount',
                    f'type=bind,src={CONFIG_SOURCE.parent},dst=/source,readonly',
                    '--entrypoint', 'cat', 'nginx:alpine', '/source/' + path.name], timeout=30)


def mailbox(value):
    if not isinstance(value, str) or '\r' in value or '\n' in value:
        raise ValueError('Invalid mailbox')
    if parseaddr(value)[1] != value or '@' not in value or len(value) > 254:
        raise ValueError('Invalid mailbox')
    return value


def message_for(report, sender, recipient):
    message = EmailMessage()
    message['From'], message['To'] = mailbox(sender), mailbox(recipient)
    success = report['backup_status'] == 'success'
    message['Subject'] = ('[AndersonMultiMarcas] Backup diário concluído com sucesso' if success
                          else '[AndersonMultiMarcas] ALERTA — Falha no backup diário')
    seconds = round(report.get('duration_seconds', 0))
    last = report.get('last_valid', {})
    restored = report.get('last_restore', {})
    fields = {
        'Sistema': DATABASE, 'Banco': DATABASE, 'Servidor': 'StartingWeb',
        'Data e horário (Brasília)': report.get('completed_at'),
        'Início do backup': report['started_at'], 'Conclusão': report.get('completed_at'),
        'Duração': f'{seconds // 60} min {seconds % 60} s',
        'Tamanho': f"{report.get('size_bytes', 0) / 1000000:.2f} MB",
        'Verificação SQL': report.get('sql_verify', 'não concluída'),
        'SHA-256': 'registrado no manifesto protegido',
        'Armazenamento': 'VPS StartingWeb; repositório Restic criptografado',
        'Último backup válido': last.get('verified_at', 'não confirmado'),
        'Restauração de teste': restored.get('completed_at', 'pendente'),
        'Resultado da restauração': restored.get('status', 'pendente'),
        'Status': 'SUCESSO' if success else 'FALHA',
        'Etapa': report.get('stage'), 'Próximo backup (Brasília)': report.get('next_execution'),
        'Observação': 'Armazenamento local: perda completa da VPS pode perder também os backups.'}
    message.set_content('\n'.join(f'{key}: {value}' for key, value in fields.items()) + '\n')
    return message


def send_email(config, report, *, sleep=time.sleep):
    if not config.get('recipient'):
        return {'status': 'pending_authorized_recipient', 'attempts': 0}
    try:
        settings = json.loads(read_smtp_source(CONFIG_SOURCE))['email']
        password = ''.join(read_smtp_source(Path(settings['password_file'])).split())
        if (settings['provider'] != 'gmail' or settings['host'] != 'smtp.gmail.com'
                or settings['port'] != 587 or settings['tls'] != 'starttls'
                or settings['credential_kind'] != 'google_app_password'
                or not re.fullmatch(r'[A-Za-z0-9]{16}', password)):
            raise ValueError('Unexpected SMTP profile')
        message = message_for(report, settings['from'], config['recipient'])
    except Exception:
        return {'status': 'configuration_failed', 'attempts': 0}
    for attempt in range(1, 4):
        connection = None
        try:
            context = ssl.create_default_context()
            context.minimum_version = ssl.TLSVersion.TLSv1_2
            connection = smtplib.SMTP(settings['host'], settings['port'], timeout=15)
            connection.ehlo()
            connection.starttls(context=context)
            connection.ehlo()
            connection.login(settings['username'], password)
            if connection.send_message(message):
                raise ValueError('Recipient rejected')
            result = {'status': 'accepted_by_smtp', 'attempts': attempt,
                      'data_response': 250, 'certificate_verified': True}
            # DATA acceptance is final even if subsequent socket inspection or
            # QUIT fails. Never duplicate an already accepted notification.
            try:
                result['tls_version'] = connection.sock.version()
            except Exception:
                pass
            return result
        except smtplib.SMTPAuthenticationError as error:
            if 500 <= error.smtp_code < 600:
                return {'status': 'authentication_failed', 'attempts': attempt}
        except Exception:
            pass
        finally:
            if connection:
                # An error in QUIT must never duplicate an accepted message.
                try:
                    connection.quit()
                except Exception:
                    pass
                try:
                    connection.close()
                except Exception:
                    pass
        if attempt < 3:
            sleep(2 ** attempt)
    return {'status': 'failed', 'attempts': 3}


def saved(name):
    path = ROOT / 'state' / name
    return json.loads(private(path).read_text()) if path.exists() else {}


def run_backup(config, *, initial=False):
    start = now()
    run_id = start.strftime('%Y%m%dT%H%M%SZ') + '-' + secrets.token_hex(4)
    report = {'run_id': run_id, 'started_at': start.astimezone(ZONE).isoformat(),
              'backup_status': 'failed', 'next_execution': next_execution(start)}
    work = ROOT / 'work' / run_id
    work.mkdir(mode=0o700)
    native_sql = SQL_DIR + '/' + run_id + '.partial'
    try:
        report['stage'] = 'capacity'
        size_mb = int(sql("SELECT SUM(size)*8/1024 FROM sys.master_files "
                          "WHERE database_id=DB_ID(N'AndersonMultiMarcas');").strip())
        reserve = max(config['reserve_free_bytes'], size_mb * 1048576 * 4)
        if shutil.disk_usage(ROOT).free < reserve:
            raise BackupError('Insufficient disk reserve; previous backups preserved.')
        if sum(p.stat().st_size for p in (ROOT / 'repository').rglob('*') if p.is_file()) > config['max_repository_bytes']:
            raise BackupError('Repository capacity threshold exceeded; review storage.')
        repository = Repository(config)
        repository.identity()
        report['stage'] = 'native_backup'
        execute(['docker', 'exec', SQL_CONTAINER, 'sh', '-c',
                 'umask 077; mkdir -p ' + SQL_DIR + '; chmod 700 ' + SQL_DIR])
        options = 'COPY_ONLY, CHECKSUM, INIT' if initial else 'CHECKSUM, INIT'
        sql(f"BACKUP DATABASE [{DATABASE}] TO DISK=N'{native_sql}' WITH {options};\n"
            f"RESTORE VERIFYONLY FROM DISK=N'{native_sql}' WITH CHECKSUM, STOP_ON_ERROR;")
        report['sql_verify'] = 'aprovado (VERIFYONLY WITH CHECKSUM)'
        partial = work / 'database.partial'
        execute(['docker', 'cp', f'{SQL_CONTAINER}:{native_sql}', str(partial)])
        partial.chmod(0o600)
        with partial.open('rb') as stream:
            os.fsync(stream.fileno())
        backup_sha = digest(partial)
        partial.rename(work / 'database.bak')
        report.update(size_bytes=(work / 'database.bak').stat().st_size, sha256=backup_sha,
                      copy_only=initial)
        atomic_json(work / 'manifest.json', {
            'run_id': run_id, 'database': DATABASE, 'started_at': report['started_at'],
            'native_completed_at': now().astimezone(ZONE).isoformat(),
            'size_bytes': report['size_bytes'], 'sha256': backup_sha,
            'copy_only': initial, 'sql_verify': report['sql_verify'],
            'status': 'native_verified'})
        report['stage'] = 'encrypted_storage'
        repository.command('backup', '--json', '--host', HOST, '--tag', TAG,
                           '--tag', 'candidate', '--tag', 'run:' + run_id, '.', cwd=work)
        snapshots = [s for s in repository.snapshots() if 'run:' + run_id in s.get('tags', [])]
        if len(snapshots) != 1:
            raise BackupError('Expected exactly one encrypted snapshot for this run.')
        snapshot = snapshots[0]['id']
        report['stage'] = 'authenticated_restore'
        target = ROOT / 'work' / (run_id + '-verify')
        target.mkdir(mode=0o700)
        try:
            repository.command('restore', snapshot, '--target', str(target), '--verify')
            manifests = list(target.rglob('manifest.json'))
            if len(manifests) != 1:
                raise BackupError('Restored repository payload is ambiguous.')
            restored = manifests[0].parent
            if (digest(restored / 'database.bak') != backup_sha
                    or json.loads(manifests[0].read_text())['run_id'] != run_id):
                raise BackupError('Authenticated restore checksum mismatch.')
        finally:
            shutil.rmtree(target)
        repository.command('check', '--read-data')
        repository.command('tag', '--remove', 'candidate', '--add', 'verified', snapshot)
        snapshots = [s for s in repository.snapshots() if 'run:' + run_id in s.get('tags', [])]
        if len(snapshots) != 1 or 'verified' not in snapshots[0].get('tags', []):
            raise BackupError('Verified snapshot was not confirmed.')
        snapshot = snapshots[0]['id']  # Restic tagging creates a new snapshot ID.
        destination = ROOT / 'native' / run_id
        work.rename(destination)
        receipt = {'snapshot': snapshot, 'run_id': run_id, 'sha256': backup_sha,
                   'size_bytes': report['size_bytes'], 'verified_at': now().astimezone(ZONE).isoformat()}
        atomic_json(ROOT / 'state' / 'last-valid.json', receipt)
        report.update(snapshot=snapshot, last_valid=receipt)
        report['stage'] = 'retention'
        report['retained'] = repository.retain(snapshot)
        # Raw native material is a convenience; encrypted snapshots carry 7/4/3.
        # Keep the newest two confirmed native copies, never arbitrary directories.
        copies = [destination] + sorted((p for p in (ROOT / 'native').iterdir()
                                         if p.is_dir() and p != destination), reverse=True)
        for old in copies[2:]:
            private(old, directory=True)
            manifest = json.loads(private(old / 'manifest.json').read_text())
            if (set(p.name for p in old.iterdir()) != {'database.bak', 'manifest.json'}
                    or manifest['run_id'] != old.name or digest(old / 'database.bak') != manifest['sha256']):
                raise BackupError('Native retention inventory diverged; nothing further removed.')
            shutil.rmtree(old)
        report.update(backup_status='success', stage='completed')
    except Exception as error:
        report['error_code'] = type(error).__name__
    finally:
        # Only this unique staging filename can be removed from the shared SQL host.
        try:
            execute(['docker', 'exec', SQL_CONTAINER, 'rm', '-f', native_sql], timeout=30)
        except Exception:
            report['sql_staging_cleanup'] = 'failed'
        if work.exists():
            # Failed partials are explicitly isolated, never advertised as valid backups.
            atomic_json(work / 'failure.json', {'run_id': run_id, 'stage': report.get('stage'),
                                              'error_code': report.get('error_code')})
        report.update(completed_at=now().astimezone(ZONE).isoformat(),
                      duration_seconds=round((now() - start).total_seconds(), 2),
                      last_valid=saved('last-valid.json'), last_restore=saved('last-restore.json'))
        report['notification'] = send_email(config, report)
        atomic_json(ROOT / 'state' / 'status.json', report)
        atomic_json(ROOT / 'logs' / (run_id + '.json'), report)
    return report


def monitor(config):
    current = now()
    last = saved('last-valid.json')
    status = saved('status.json')
    age = (current - datetime.fromisoformat(last['verified_at'])).total_seconds() if last else None
    problem = age is None or age < -300 or age > 27 * 3600
    problem = problem or status.get('backup_status') != 'success'
    problem = problem or status.get('notification', {}).get('status') != 'accepted_by_smtp'
    repository_present = False
    try:
        repository = Repository(config)
        repository.identity()
        repository_present = last.get('snapshot') in {
            item['id'] for item in repository.snapshots() if 'verified' in item.get('tags', [])}
    except Exception:
        pass
    problem = problem or not repository_present or shutil.disk_usage(ROOT).free < config['reserve_free_bytes']
    report = {'checked_at': current.isoformat(), 'status': 'alert' if problem else 'healthy',
              'last_valid': last, 'last_valid_snapshot_present': repository_present,
              'notification': status.get('notification', {})}
    alert = saved('monitor-alert.json')
    if problem and (not alert or (current - datetime.fromisoformat(alert['sent_at'])).total_seconds() > 24 * 3600):
        notice = {**status, 'backup_status': 'failed', 'stage': 'freshness_or_notification_monitor',
                  'started_at': current.astimezone(ZONE).isoformat(),
                  'completed_at': current.astimezone(ZONE).isoformat(), 'last_valid': last}
        report['alert_notification'] = send_email(config, notice)
        if report['alert_notification']['status'] == 'accepted_by_smtp':
            atomic_json(ROOT / 'state' / 'monitor-alert.json', {'sent_at': current.isoformat()})
    if not problem and alert:
        (ROOT / 'state' / 'monitor-alert.json').unlink()
    atomic_json(ROOT / 'state' / 'monitor-status.json', report)
    return report


def initialize(config):
    if (ROOT / 'config.json').exists() or (ROOT / 'repository' / 'config').exists():
        raise BackupError('Existing backup installation must not be reinitialized.')
    for name in ('state', 'work', 'native', 'repository', 'logs'):
        (ROOT / name).mkdir(mode=0o700)
    descriptor = os.open(ROOT / 'repository.password', os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    with os.fdopen(descriptor, 'w') as stream:
        stream.write(secrets.token_hex(32) + '\n')
    repository = Repository(config)
    repository.command('init', '--repository-version', '2')
    config['repository_id'] = json.loads(repository.command('cat', 'config'))['id']
    atomic_json(ROOT / 'config.json', config)


def main():
    os.umask(0o077)
    def interrupted(_number, _frame):
        raise BackupError('Backup operation interrupted.')
    signal.signal(signal.SIGTERM, interrupted)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=('backup', 'monitor', 'status'))
    parser.add_argument('--initial', action='store_true')
    args = parser.parse_args()
    config = load_config()
    if args.operation == 'status':
        print(json.dumps(saved('status.json'), ensure_ascii=False))
        return 0
    with lock(ROOT / 'state' / 'backup.lock'):
        if args.operation == 'monitor':
            report = monitor(config)
        else:
            report = run_backup(config, initial=args.initial)
    print(json.dumps({k: report.get(k) for k in ('run_id', 'backup_status', 'stage', 'notification', 'status')}))
    notification_failed = (args.operation == 'backup' and config.get('recipient') and
                           report.get('notification', {}).get('status') != 'accepted_by_smtp')
    return 0 if report.get('backup_status', 'success') == 'success' and not notification_failed else 1


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except Exception as error:
        print('Backup operation refused: ' + type(error).__name__ + '; details suppressed.')
        raise SystemExit(1) from None
