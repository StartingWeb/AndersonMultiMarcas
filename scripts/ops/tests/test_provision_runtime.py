import argparse
import importlib.util
import os
from pathlib import Path
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('provision',Path(__file__).parents[1]/'provision_runtime.py')
p=importlib.util.module_from_spec(spec);spec.loader.exec_module(p)

class RuntimeInstallerTests(unittest.TestCase):
    def args(self,mode='production'):
        return argparse.Namespace(mode=mode,container='sqlserver',root=p.ROOT,expected_container='owned',
            expected_server='isolated',expected_database_guid='00000000-0000-0000-0000-000000000001',authorization_ref='test-runtime-only')

    def test_container_and_mode_are_explicit_and_fail_closed(self):
        args=self.args();p.validate_target(args,{'Id':'owned'})
        with self.assertRaises(p.Refused):p.validate_target(args,{'Id':'different'})
        args.container='other-project'
        with self.assertRaises(p.Refused):p.validate_target(args,{'Id':'owned'})

    def test_trial_rejects_an_unlabelled_production_container(self):
        args=self.args('trial')
        with self.assertRaises(p.Refused):p.validate_target(args,{'Id':'owned'})
        p.validate_target(args,{'Id':'owned','Config':{'Labels':{'anderson.purpose':'disposable-local'}}})

    def test_private_file_and_atomic_write_are_restrictive(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory).resolve();root.chmod(0o700)
            file=root/'receipt';p.atomic(file,'non-sensitive fixture')
            self.assertEqual(0o600,file.stat().st_mode&0o777);p.private(file)
            link=root/'link';link.symlink_to(file)
            with self.assertRaises(p.Refused):p.private(link)
            file.chmod(0o644)
            with self.assertRaises(p.Refused):p.private(file)

    def test_concurrent_installers_are_refused(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory).resolve();root.chmod(0o700);(root/'shared').mkdir(mode=0o700)
            with p.installer_lock(root):
                with self.assertRaises(p.Refused):
                    with p.installer_lock(root):pass

    def test_sql_literal_is_data_and_rejects_newlines(self):
        self.assertEqual("N'a''b'",p.literal("a'b"))
        with self.assertRaises(p.Refused):p.literal('a\nb')

    def test_permission_profiles_match_while_production_has_extra_identity_guards(self):
        root=Path(__file__).resolve().parents[3]
        trial=(root/'deploy/sql/runtime_permissions_trial.sql').read_text()
        production=(root/'deploy/sql/runtime_permissions.production.sql').read_text()
        marker="IF SUSER_ID(N'anderson_runtime')"
        self.assertEqual(trial[trial.index(marker):],production[production.index(marker):])
        for guard in ['Anderson.ExpectedServer','Anderson.ExpectedDatabaseGuid','Anderson.Authorization','Anderson.Scope','51110']:
            self.assertIn(guard,production)

if __name__=='__main__':unittest.main()
