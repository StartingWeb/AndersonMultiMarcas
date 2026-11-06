import importlib.util
import json
from datetime import datetime, timezone
from pathlib import Path
import tempfile
import unittest
from contextlib import nullcontext, redirect_stdout
import io
import sys
from unittest import mock

spec = importlib.util.spec_from_file_location('amm_backup', Path(__file__).parents[1] / 'backup.py')
backup = importlib.util.module_from_spec(spec)
spec.loader.exec_module(backup)


class BackupSafetyTests(unittest.TestCase):
    def test_failed_configured_notification_is_not_a_successful_service_exit(self):
        with mock.patch.object(backup, 'load_config', return_value={'recipient': 'owner@example.com'}), \
                mock.patch.object(backup, 'lock', return_value=nullcontext()), \
                mock.patch.object(backup, 'run_backup', return_value={'backup_status':'success','notification':{'status':'failed'}}), \
                mock.patch.object(sys, 'argv', ['backup.py','backup']), \
                mock.patch.object(backup.signal, 'signal'), redirect_stdout(io.StringIO()):
            self.assertEqual(1, backup.main())

    def test_calendar_uses_civil_brasilia_time_and_next_day_at_boundary(self):
        self.assertEqual('2026-11-06T03:00:00-03:00', backup.next_execution(
            datetime(2026, 11, 6, 5, 59, tzinfo=timezone.utc)))
        self.assertEqual('2026-11-07T03:00:00-03:00', backup.next_execution(
            datetime(2026, 11, 6, 6, 0, tzinfo=timezone.utc)))

    def test_private_files_reject_symlinks_and_public_permissions(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            path = root / 'private'
            path.write_text('fixture')
            path.chmod(0o600)
            self.assertEqual(path, backup.private(path))
            path.chmod(0o644)
            with self.assertRaises(backup.BackupError):
                backup.private(path)
            path.chmod(0o600)
            link = root / 'link'
            link.symlink_to(path)
            with self.assertRaises(backup.BackupError):
                backup.private(link)

    def test_missing_recipient_never_reads_smtp_credentials(self):
        with mock.patch.object(backup, 'read_smtp_source') as read:
            self.assertEqual('pending_authorized_recipient', backup.send_email({}, {})['status'])
            read.assert_not_called()

    def test_message_has_required_subject_and_no_attachments_or_paths(self):
        report = {'backup_status': 'success', 'started_at': '2026-11-06T03:00:00-03:00',
                  'completed_at': '2026-11-06T03:00:42-03:00', 'duration_seconds': 42,
                  'sql_verify': 'aprovado', 'size_bytes': 1000}
        message = backup.message_for(report, 'sender@example.com', 'owner@example.com')
        self.assertEqual('[AndersonMultiMarcas] Backup diário concluído com sucesso', message['Subject'])
        self.assertEqual([], list(message.iter_attachments()))
        self.assertNotIn('/opt/', message.get_content())
        report['backup_status'] = 'failed'
        self.assertEqual('[AndersonMultiMarcas] ALERTA — Falha no backup diário',
                         backup.message_for(report, 'sender@example.com', 'owner@example.com')['Subject'])

    def test_smtp_header_injection_and_unapproved_source_path_are_rejected(self):
        with self.assertRaises(ValueError):
            backup.mailbox('owner@example.com\nBcc: stranger@example.com')
        with mock.patch.object(backup, 'execute') as execute:
            with self.assertRaises(backup.BackupError):
                backup.read_smtp_source(Path('/etc/shadow'))
            execute.assert_not_called()

    def test_retention_must_keep_newest_verified_snapshot(self):
        repository = object.__new__(backup.Repository)
        repository.snapshots = lambda: [{'id': 'newest', 'tags': ['verified']}]
        repository.command = mock.Mock(return_value=json.dumps([{'keep': [], 'remove': [{'id': 'newest'}]}]))
        with self.assertRaises(backup.BackupError):
            repository.retain('newest')
        self.assertEqual(1, repository.command.call_count)

    def test_retention_authenticates_before_deletion_and_checks_final_inventory(self):
        repository = object.__new__(backup.Repository)
        repository.snapshots = mock.Mock(side_effect=[
            [{'id': 'newest', 'tags': ['verified']}, {'id': 'old', 'tags': ['verified']}],
            [{'id': 'newest', 'tags': ['verified']}]])
        repository.command = mock.Mock(side_effect=[json.dumps([
            {'keep': [{'id': 'newest'}], 'remove': [{'id': 'old'}]}]), '', '', '', ''])
        self.assertEqual(1, repository.retain('newest'))
        self.assertEqual(('check', '--read-data'), repository.command.call_args_list[1].args)
        self.assertEqual(('forget', 'old'), repository.command.call_args_list[2].args)

    def test_failed_sql_backup_never_becomes_valid_native_copy(self):
        with tempfile.TemporaryDirectory() as directory, mock.patch.object(backup, 'ROOT', Path(directory)):
            for name in ('work', 'state', 'native', 'logs'):
                (Path(directory) / name).mkdir(mode=0o700)
            with mock.patch.object(backup, 'sql', side_effect=backup.BackupError('fixture failure')), \
                    mock.patch.object(backup, 'execute'), mock.patch.object(backup, 'send_email', return_value={'status': 'pending_authorized_recipient'}):
                report = backup.run_backup({})
            self.assertEqual('failed', report['backup_status'])
            self.assertFalse((Path(directory) / 'state' / 'last-valid.json').exists())
            self.assertEqual([], list((Path(directory) / 'native').iterdir()))
            self.assertEqual(1, len(list((Path(directory) / 'work').glob('*/failure.json'))))

    def test_smtp_quit_failure_does_not_duplicate_accepted_message(self):
        settings = {'provider': 'gmail', 'host': 'smtp.gmail.com', 'port': 587,
                    'tls': 'starttls', 'credential_kind': 'google_app_password',
                    'from': 'sender@example.com', 'username': 'sender@example.com',
                    'password_file': '/etc/tscomproouro-backup/test-password'}
        connection = mock.Mock()
        connection.send_message.return_value = {}
        connection.sock.version.return_value = 'TLSv1.3'
        connection.quit.side_effect = OSError('fixture disconnect after acceptance')
        with mock.patch.object(backup, 'read_smtp_source', side_effect=[json.dumps({'email': settings}), 'a' * 16]), \
                mock.patch.object(backup.smtplib, 'SMTP', return_value=connection):
            result = backup.send_email({'recipient': 'owner@example.com'},
                                       {'backup_status': 'success', 'started_at': 'fixture'})
        self.assertEqual('accepted_by_smtp', result['status'])
        self.assertEqual(1, connection.send_message.call_count)

    def test_smtp_socket_inspection_after_acceptance_cannot_trigger_a_retry(self):
        settings = {'provider': 'gmail', 'host': 'smtp.gmail.com', 'port': 587,
                    'tls': 'starttls', 'credential_kind': 'google_app_password',
                    'from': 'sender@example.com', 'username': 'sender@example.com',
                    'password_file': '/etc/tscomproouro-backup/test-password'}
        connection = mock.Mock()
        connection.send_message.return_value = {}
        connection.sock.version.side_effect = OSError('socket closed after DATA acceptance')
        with mock.patch.object(backup, 'read_smtp_source', side_effect=[json.dumps({'email': settings}), 'a' * 16]), \
                mock.patch.object(backup.smtplib, 'SMTP', return_value=connection):
            result = backup.send_email({'recipient': 'owner@example.com'},
                                       {'backup_status': 'success', 'started_at': 'fixture'})
        self.assertEqual('accepted_by_smtp', result['status'])
        self.assertEqual(1, connection.send_message.call_count)


if __name__ == '__main__':
    unittest.main()
