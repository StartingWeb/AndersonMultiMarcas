#!/usr/bin/env python3
"""Restore only to new, internally networked, labelled SQL test containers.

Real backup data remains on the authorized VPS. Only structural metadata,
aggregate counts and HTTP statuses enter the report. All test resources have
unique names; existing containers, networks and volumes cannot be selected.
"""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import signal
import subprocess
import sys
import time

spec = importlib.util.spec_from_file_location('amm_backup', Path(__file__).with_name('backup.py'))
backup = importlib.util.module_from_spec(spec)
spec.loader.exec_module(backup)
LABEL = 'com.startingweb.anderson-restore-test'


def read_sql_script(path):
    # EF's UTF-8 BOM is valid in a file, but must not become a T-SQL token when
    # passing the script through sqlcmd stdin. Keep the original artifact intact.
    return path.read_text(encoding='utf-8-sig')


def structural_diff(expected, actual):
    result = {}
    for section in ('columns', 'indexes', 'foreign_keys', 'checks'):
        left = {json.dumps(v, sort_keys=True) for v in expected.get(section, [])}
        right = {json.dumps(v, sort_keys=True) for v in actual.get(section, [])}
        result[section] = {'missing': [json.loads(v) for v in sorted(left-right)],
                           'additional': [json.loads(v) for v in sorted(right-left)]}
    return result


def main():
    os.umask(0o077)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', type=Path, required=True)
    parser.add_argument('--migration-sql', type=Path, required=True)
    parser.add_argument('--schema-correction', type=Path)
    parser.add_argument('--unique-index-proposal', type=Path)
    args = parser.parse_args()
    root = Path('/opt/startingweb/apps/anderson-multimarcas-unified')
    run_id = secrets.token_hex(6)
    prefix = 'amm-restore-' + run_id
    target = root / 'snapshots' / prefix
    target.mkdir(mode=0o700)
    report = {'run_id': run_id, 'started_at': backup.now().isoformat(), 'status': 'failed'}
    containers, volumes = [], []
    network_created = False
    original = backup.saved('last-valid.json')
    native = backup.ROOT / 'native' / original['run_id'] / 'database.bak'
    password = 'R9!' + secrets.token_hex(24)

    def docker(*args):
        return backup.execute(['docker', *args], timeout=900)

    def test_sql(query, database='master', allow_failure=False):
        result = subprocess.run(['docker', 'exec', '-i', prefix + '-sql', 'sh', '-c',
                                 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec '
                                 '/opt/mssql-tools18/bin/sqlcmd -C -I -S localhost -U sa -b -l 5 -y 0 -w 65535 -d ' + database],
                                input=('SET NOCOUNT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; '
                                       'SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; '
                                       'SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;\nGO\n' + query), text=True,
                                capture_output=True, timeout=900)
        if result.returncode:
            if allow_failure:
                return {'passed': False, 'sql_error_numbers': re.findall(r'Msg (\d+),', result.stdout + result.stderr)}
            raise backup.BackupError('Isolated SQL operation failed; output suppressed.')
        return {'passed': True} if allow_failure else result.stdout

    def snapshot(database, production=False):
        query = Path(__file__).with_name('schema.sql').read_text()
        if production:
            output = backup.execute(['docker', 'exec', '-i', 'sqlserver', 'sh', '-c',
                                     'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec '
                                     '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b -y 0 -w 65535 -d AndersonMultiMarcas'],
                                    data=query, timeout=120)
        else:
            output = test_sql(query, database)
        value = json.loads(''.join(output.splitlines()))
        # SQL FOR JSON omits a property when an empty subquery produces NULL.
        for section in ('columns', 'indexes', 'foreign_keys', 'checks', 'migrations', 'counts'):
            value.setdefault(section, [])
        return value

    def restore(database):
        # Inspect the backup layout and validate logical names of its audited source.
        raw = test_sql("RESTORE FILELISTONLY FROM DISK=N'/var/opt/mssql/backup/database.bak';")
        # The fixed-width output is intentionally not parsed. Query logical names
        # from the audited source database; validate before interpolating SQL.
        logical = backup.sql("SELECT name FROM sys.master_files WHERE database_id=DB_ID(N'AndersonMultiMarcas') ORDER BY type;")
        names = [v.strip() for v in logical.splitlines() if v.strip()]
        if len(names) != 2 or any(not re.fullmatch(r'[A-Za-z0-9_]+', v) for v in names):
            raise backup.BackupError('Unexpected logical file layout; review restore mapping.')
        if not raw.strip():
            raise backup.BackupError('Backup file list is empty.')
        test_sql(f"RESTORE DATABASE [{database}] FROM DISK=N'/var/opt/mssql/backup/database.bak' "
                 f"WITH MOVE N'{names[0]}' TO N'/var/opt/mssql/data/{database}.mdf', "
                 f"MOVE N'{names[1]}' TO N'/var/opt/mssql/data/{database}_log.ldf', CHECKSUM, RECOVERY;")

    def start_app(database, suffix):
        name = prefix + '-web-' + suffix
        containers.append(name)
        docker('run', '--detach', '--name', name, '--label', LABEL + '=' + run_id,
               '--network', prefix, '--memory', '384m', '--memory-swap', '384m', '--cpus', '.5',
               '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true',
               '--log-opt', 'max-size=2m', '--log-opt', 'max-file=1',
               '--env', 'ASPNETCORE_ENVIRONMENT=Production', '--env', 'DOTNET_ENVIRONMENT=Production',
               '--env', 'Database__ApplyMigrationsOnStartup=false', '--env', 'StorageMigration__Execute=false',
               '--env', 'LegacyImageImport__RecoverPendingJobsOnStartup=false',
               '--env', 'Storage__Provider=Local', '--env', 'Storage__DualReadEnabled=false',
               '--env', 'DataProtection__ApplicationName=AndersonRestoreTest',
               '--env', 'DataProtection__KeysPath=/home/app/.aspnet/DataProtection-Keys',
               '--mount', 'type=volume,src=' + prefix + '-keys,dst=/home/app/.aspnet/DataProtection-Keys',
               '--env', 'ConnectionStrings__DefaultConnectionProd=Server=' + prefix + '-sql;Database=' + database +
               ';User Id=amm_reader;Password=' + password + ';Encrypt=False;TrustServerCertificate=True',
               '--env', 'Deployment__Commit=' + release['commit'], runtime_image_id)
        for attempt in range(60):
            response = subprocess.run(['docker', 'exec', name, 'curl', '--silent', '--fail', '--max-time', '3',
                                       '--header', 'X-Forwarded-Proto: https', 'http://127.0.0.1:8080/health/release'],
                                      capture_output=True, text=True)
            if not response.returncode and json.loads(response.stdout).get('commit') == release['commit']:
                break
            time.sleep(1)
        else:
            raise backup.BackupError('Isolated application did not become ready.')
        statuses = {}
        for route in ['/', '/Veiculos', '/Empresa', '/Login', '/Admin', '/img/carroDefault.png']:
            statuses[route] = docker('exec', name, 'curl', '--silent', '--output', '/dev/null',
                                     '--write-out', '%{http_code}', '--max-time', '30',
                                     '--header', 'X-Forwarded-Proto: https', 'http://127.0.0.1:8080' + route).strip()
        logs = docker('logs', name)
        result = {'http_statuses': statuses,
                  'sql_errors': len(re.findall(r'SqlException|Invalid column name|Invalid object name', logs)),
                  'application_failures': len(re.findall(r'fail:', logs)),
                  'external_network_access': False, 'database_identity': 'read_only',
                  'production_credentials_present': False, 'release_health_matches': True}
        result['passed'] = (all(statuses[x] == '200' for x in ['/', '/Veiculos', '/Empresa', '/Login', '/img/carroDefault.png'])
                            and statuses['/Admin'] in ('302', '303') and not result['sql_errors'] and not result['application_failures'])
        key_hashes = docker('exec', name, 'sh', '-c', 'sha256sum /home/app/.aspnet/DataProtection-Keys/*.xml').splitlines()
        result['data_protection_key_hashes'] = sorted(line.split()[0] for line in key_hashes)
        docker('stop', '--time', '15', name)
        return result

    try:
        def interrupted(_number, _frame):
            raise backup.BackupError('Isolated validation interrupted; cleanup required.')
        signal.signal(signal.SIGTERM, interrupted)
        report['stage'] = 'preflight'
        available = int(re.search(r'MemAvailable:\s+(\d+)', Path('/proc/meminfo').read_text())[1]) * 1024
        if available < 2200 * 1048576:
            raise backup.BackupError('Insufficient memory reserve for isolated restore.')
        if backup.digest(native) != original['sha256']:
            raise backup.BackupError('Native backup checksum differs from valid receipt.')
        # Revalidate both transferred release files against the bundle checksums.
        expected = {}
        for line in (args.bundle / 'SHA256SUMS').read_text().splitlines():
            sha, name = line.split('  ')
            if name not in ('image.tar.gz', 'release.json') or name in expected or not re.fullmatch(r'[a-f0-9]{64}', sha):
                raise backup.BackupError('Invalid bundle checksums.')
            expected[name] = sha
        if set(expected) != {'image.tar.gz', 'release.json'} or any(backup.digest(args.bundle / p) != s for p,s in expected.items()):
            raise backup.BackupError('Transferred artifact checksum mismatch.')
        release = json.loads((args.bundle / 'release.json').read_text())
        executor_spec = importlib.util.spec_from_file_location('amm_release', root / 'bin/release.py')
        executor = importlib.util.module_from_spec(executor_spec)
        executor_spec.loader.exec_module(executor)
        runtime_image_id = executor.Docker().check_image(release, args.bundle / 'image.tar.gz')
        image = json.loads(docker('image', 'inspect', runtime_image_id))[0]
        if (image['Architecture'] != 'amd64' or image['Os'] != 'linux'
                or image['Config']['Labels']['org.opencontainers.image.revision'] != release['commit']):
            raise backup.BackupError('Runtime image architecture or source does not match.')
        report.update(backup=original, release=release, runtime_image_id=runtime_image_id, bundle_sha256=expected,
                      migration_sql_sha256=backup.digest(args.migration_sql))
        report['stage'] = 'source_schema_snapshot'
        source = snapshot('AndersonMultiMarcas', production=True)
        backup.atomic_json(target / 'source-schema.json', source)
        source_sql = json.loads(docker('inspect', 'sqlserver'))[0]
        sql_image = source_sql['Image']
        docker('network', 'create', '--internal', '--label', LABEL + '=' + run_id, prefix)
        network_created = True
        volume = prefix + '-sql-data'
        docker('volume', 'create', '--label', LABEL + '=' + run_id, volume)
        volumes.append(volume)
        sql_name = prefix + '-sql'
        containers.append(sql_name)
        docker('run', '--detach', '--name', sql_name, '--label', LABEL + '=' + run_id,
               '--network', prefix, '--memory', '2g', '--memory-swap', '2g', '--cpus', '.75',
               '--env', 'ACCEPT_EULA=Y', '--env', 'MSSQL_PID=Developer',
               '--env', 'MSSQL_MEMORY_LIMIT_MB=768', '--env', 'MSSQL_SA_PASSWORD=' + password,
               '--mount', 'type=volume,src=' + volume + ',dst=/var/opt/mssql', sql_image)
        for attempt in range(90):
            try:
                test_sql('SELECT 1;')
                break
            except backup.BackupError:
                time.sleep(2)
        else:
            raise backup.BackupError('Isolated SQL instance did not become ready.')
        docker('exec', '--user', '0', sql_name, 'mkdir', '-p', '/var/opt/mssql/backup')
        docker('cp', str(native), sql_name + ':/var/opt/mssql/backup/database.bak')
        docker('exec', '--user', '0', sql_name, 'sh', '-c',
               'chown 10001:0 /var/opt/mssql/backup/database.bak; chmod 600 /var/opt/mssql/backup/database.bak')
        report['stage'] = 'native_restore'
        restore('AndersonMultiMarcas')
        report['native_restore'] = 'passed'
        test_sql('DBCC CHECKDB ([AndersonMultiMarcas]) WITH NO_INFOMSGS, ALL_ERRORMSGS;')
        report['dbcc_checkdb'] = 'passed'
        violations = test_sql("CREATE TABLE #violations ([Table] nvarchar(517), [Constraint] nvarchar(517), [Where] nvarchar(max));\n"
                              "INSERT INTO #violations EXEC(N'DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS, NO_INFOMSGS;');\n"
                              "SELECT N'CONSTRAINT_VIOLATIONS='+CONVERT(nvarchar(20),COUNT_BIG(*)) FROM #violations;", 'AndersonMultiMarcas')
        count = re.search(r'CONSTRAINT_VIOLATIONS=(\d+)', violations)
        if not count:
            raise backup.BackupError('Constraint validation returned no aggregate receipt.')
        report['constraint_violation_count'] = int(count[1])
        report['referential_integrity'] = 'passed' if not report['constraint_violation_count'] else 'review_required'
        report['stage'] = 'restored_schema_snapshot'
        restored = snapshot('AndersonMultiMarcas')
        backup.atomic_json(target / 'restored-schema.json', restored)
        report['schema_and_counts_match_source'] = restored == source
        report['source_comparison'] = structural_diff(source, restored)
        if restored != source:
            # Concurrent legitimate production writes can change counts, never
            # silently call them corruption or migrate to hide a difference.
            report['source_comparison_note'] = 'Inspect structural metadata and aggregate counts; source may have concurrent writes.'
        test_sql("CREATE LOGIN amm_reader WITH PASSWORD=N'" + password + "';")
        key_volume = prefix + '-keys'
        docker('volume', 'create', '--label', LABEL + '=' + run_id, key_volume)
        volumes.append(key_volume)
        docker('run', '--rm', '--network', 'none', '--user', '0', '--cap-drop', 'ALL',
               '--cap-add', 'CHOWN', '--cap-add', 'FOWNER', '--security-opt', 'no-new-privileges:true',
               '--mount', 'type=volume,src=' + key_volume + ',dst=/keys', '--entrypoint', 'sh',
               runtime_image_id, '-c', 'chown 1654:1654 /keys; chmod 700 /keys')
        for database in ['AndersonMultiMarcas']:
            test_sql('CREATE USER amm_reader FOR LOGIN amm_reader; ALTER ROLE db_datareader ADD MEMBER amm_reader;', database)
        report['stage'] = 'application_on_untouched_restore'
        report['application_original'] = start_app('AndersonMultiMarcas', 'original')
        after_app = snapshot('AndersonMultiMarcas')
        report['original_schema_counts_unchanged_by_application'] = after_app == restored
        report['stage'] = 'second_copy_migration_trial'
        restore('AndersonSchemaTrial')
        report['migration_trial'] = test_sql(read_sql_script(args.migration_sql), 'AndersonSchemaTrial', allow_failure=True)
        if report['migration_trial']['passed']:
            trial = snapshot('AndersonSchemaTrial')
            backup.atomic_json(target / 'migration-trial-schema.json', trial)
            report['migration_changes'] = structural_diff(restored, trial)
            report['new_migration_history'] = sorted(set(x['MigrationId'] for x in trial['migrations']) -
                                                      set(x['MigrationId'] for x in restored['migrations']))
            report['migration_counts_unchanged'] = (
                [x for x in trial['counts'] if x['table'] != 'dbo.__EFMigrationsHistory'] ==
                [x for x in restored['counts'] if x['table'] != 'dbo.__EFMigrationsHistory'])
            report['migration_replay'] = test_sql(read_sql_script(args.migration_sql), 'AndersonSchemaTrial', allow_failure=True)
            report['migration_replay_unchanged'] = snapshot('AndersonSchemaTrial') == trial
            if args.schema_correction:
                report['schema_correction_sha256'] = backup.digest(args.schema_correction)
                report['schema_correction'] = test_sql(read_sql_script(args.schema_correction), 'AndersonSchemaTrial', allow_failure=True)
                if not report['schema_correction']['passed']:
                    raise backup.BackupError('Proposed storage widening failed only in the second isolated copy.')
                corrected = snapshot('AndersonSchemaTrial')
                backup.atomic_json(target / 'corrected-schema.json', corrected)
                report['schema_correction_changes'] = structural_diff(trial, corrected)
                report['schema_correction_counts_unchanged'] = corrected['counts'] == trial['counts']
                report['schema_correction_replay'] = test_sql(read_sql_script(args.schema_correction), 'AndersonSchemaTrial', allow_failure=True)
                report['schema_correction_replay_unchanged'] = snapshot('AndersonSchemaTrial') == corrected
                if args.unique_index_proposal:
                    report['unique_index_proposal_sha256'] = backup.digest(args.unique_index_proposal)
                    report['unique_index_proposal'] = test_sql(read_sql_script(args.unique_index_proposal), 'AndersonSchemaTrial', allow_failure=True)
                    report['unique_index_refusal_preserves_schema_and_counts'] = snapshot('AndersonSchemaTrial') == corrected
            test_sql('DBCC CHECKDB ([AndersonSchemaTrial]) WITH NO_INFOMSGS, ALL_ERRORMSGS;')
            test_sql('CREATE USER amm_reader FOR LOGIN amm_reader; ALTER ROLE db_datareader ADD MEMBER amm_reader;', 'AndersonSchemaTrial')
            report['application_migrated'] = start_app('AndersonSchemaTrial', 'migrated')
            report['data_protection_persisted_between_containers'] = (
                bool(report['application_original']['data_protection_key_hashes']) and
                report['application_original']['data_protection_key_hashes'] == report['application_migrated']['data_protection_key_hashes'])
        report['stage'] = 'fresh_expected_schema'
        test_sql('CREATE DATABASE AndersonExpected;')
        report['fresh_schema_script'] = test_sql(read_sql_script(args.migration_sql), 'AndersonExpected', allow_failure=True)
        if not report['fresh_schema_script']['passed']:
            raise backup.BackupError('CI SQL script failed on a new isolated database; review SQL error numbers.')
        expected_schema = snapshot('AndersonExpected')
        backup.atomic_json(target / 'expected-schema.json', expected_schema)
        report['expected_vs_original'] = structural_diff(expected_schema, restored)
        report['schema_compatibility_status'] = ('review_required' if any(
            value['missing'] or value['additional'] for value in report['expected_vs_original'].values()) else 'compatible')
        report['pending_migration_history'] = sorted(set(x['MigrationId'] for x in expected_schema['migrations'])-
                                                       set(x['MigrationId'] for x in restored['migrations']))
        report['tables'] = len(restored['counts'])
        report['counts'] = restored['counts']
        report['foreign_key_count'] = len(restored['foreign_keys'])
        report['untrusted_or_disabled_foreign_keys'] = sum(bool(x['is_disabled'] or x['is_not_trusted']) for x in restored['foreign_keys'])
        report['status'] = 'verified' if (not report['constraint_violation_count'] and report['schema_and_counts_match_source'] and report['application_original']['passed']
                                         and report['original_schema_counts_unchanged_by_application']) else 'review_required'
        report['stage'] = 'completed'
    except Exception as error:
        report['error_code'] = type(error).__name__
        if isinstance(error, (backup.BackupError,)):
            report['error_description'] = str(error)
    finally:
        report['cleanup'] = []
        for name in reversed(containers):
            try:
                value = json.loads(docker('inspect', name))[0]
                if value['Config']['Labels'].get(LABEL) != run_id:
                    raise backup.BackupError('Test container label diverged; retained for review.')
                docker('rm', '--force', name)
                report['cleanup'].append(name)
            except Exception:
                report['cleanup_failed'] = True
        for volume in volumes:
            try:
                value = json.loads(docker('volume', 'inspect', volume))[0]
                if value['Labels'].get(LABEL) != run_id:
                    raise backup.BackupError('Test volume label diverged; retained for review.')
                docker('volume', 'rm', volume)
            except Exception:
                report['cleanup_failed'] = True
        if network_created:
            try:
                docker('network', 'rm', prefix)
            except Exception:
                report['cleanup_failed'] = True
        report['completed_at'] = backup.now().astimezone(backup.ZONE).isoformat()
        backup.atomic_json(target / 'restore-report.json', report)
        if report['status'] == 'verified' and not report.get('cleanup_failed'):
            backup.atomic_json(backup.ROOT / 'state' / 'last-restore.json', {
                'status': 'verified', 'completed_at': report['completed_at'], 'run_id': run_id,
                'snapshot': original['snapshot'], 'native_restore': report['native_restore'],
                'dbcc_checkdb': report['dbcc_checkdb'], 'report': str(target / 'restore-report.json')})
        print(json.dumps({k: report.get(k) for k in ['status','stage','error_code','native_restore','dbcc_checkdb',
              'referential_integrity','schema_and_counts_match_source','application_original',
              'migration_trial','new_migration_history','cleanup_failed','completed_at']}))
    return 0 if report['status'] == 'verified' and not report.get('cleanup_failed') else 1


if __name__ == '__main__':
    raise SystemExit(main())
