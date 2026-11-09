"""Security regressions for the disposable SQL helper, with no Docker or network."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

SPEC = importlib.util.spec_from_file_location('local_sql', Path(__file__).resolve().parents[1] / 'dev' / 'local_sql.py')
local_sql = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(local_sql)


class LocalSqlTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.state = Path(self.temporary.name)
        self.patch_state = mock.patch.object(local_sql, 'STATE', self.state)
        self.patch_state.start()
        self.addCleanup(self.patch_state.stop)
        (self.state / 'config.json').write_text(json.dumps({'containerId': 'owned-container',
            'connection': 'synthetic-isolated-connection', 'bootstrapPassword': 'synthetic-bootstrap'}))
        self.inspect = [{'Id': 'owned-container', 'Config': {'Labels': {'anderson.purpose': 'disposable-local'}}}]

    def invoke(self, action):
        with mock.patch.object(sys, 'argv', ['local_sql.py', action]):
            local_sql.main()

    def test_replaced_container_is_never_removed_even_with_same_label(self):
        self.inspect[0]['Id'] = 'different-container'
        with mock.patch.object(local_sql.subprocess, 'check_output', return_value=json.dumps(self.inspect).encode()), \
                mock.patch.object(local_sql, 'run') as run, self.assertRaisesRegex(SystemExit, 'differs'):
            self.invoke('stop')
        run.assert_not_called()

    def test_development_environment_and_migration_guard_override_inherited_values(self):
        with mock.patch.object(local_sql.subprocess, 'check_output', return_value=json.dumps(self.inspect).encode()), \
                mock.patch.dict(local_sql.os.environ, {'DOTNET_ENVIRONMENT': 'Production', 'StorageMigration__Execute': 'true'}), \
                mock.patch.object(local_sql, 'run') as run:
            self.invoke('test')
        env = run.call_args.kwargs['env']
        self.assertEqual('Development', env['DOTNET_ENVIRONMENT'])
        self.assertEqual('Development', env['ASPNETCORE_ENVIRONMENT'])
        self.assertEqual('false', env['StorageMigration__Execute'])
        self.assertEqual('false', env['Database__ApplyMigrationsOnStartup'])
        self.assertEqual('false', env['DevelopmentBootstrap__Enabled'])

    def test_stop_preserves_unrelated_local_files(self):
        note = self.state / 'unrelated.txt'
        note.write_text('preserve fixture')
        with mock.patch.object(local_sql.subprocess, 'check_output', return_value=json.dumps(self.inspect).encode()), \
                mock.patch.object(local_sql, 'run'):
            self.invoke('stop')
        self.assertEqual('preserve fixture', note.read_text())
        self.assertFalse((self.state / 'config.json').exists())

    def test_start_passes_password_through_container_environment_and_pins_image(self):
        (self.state / 'config.json').unlink()
        self.inspect[0]['NetworkSettings'] = {'Ports': {'1433/tcp': [{'HostPort': '14331'}]}}
        with mock.patch.object(local_sql.subprocess, 'check_output', return_value=json.dumps(self.inspect).encode()), \
                mock.patch.object(local_sql.subprocess, 'run', return_value=subprocess.CompletedProcess([], 0)) as probe, \
                mock.patch.object(local_sql, 'run') as run:
            self.invoke('start')
        self.assertIn('@sha256:', run.call_args.args[-1])
        env = dict(line.split('=', 1) for line in (self.state / 'sql.env').read_text().splitlines())
        self.assertEqual(env['MSSQL_SA_PASSWORD'], env['SQLCMDPASSWORD'])
        self.assertIn('-b', probe.call_args.args[0])
        config = json.loads((self.state / 'config.json').read_text())
        self.assertEqual('owned-container', config['containerId'])


if __name__ == '__main__':
    unittest.main()
