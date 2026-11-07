import importlib.util
from pathlib import Path
import tempfile
import sys
import unittest
from unittest import mock

spec=importlib.util.spec_from_file_location('runtime',Path(__file__).parents[1]/'render_runtime_connection.py')
runtime=importlib.util.module_from_spec(spec);spec.loader.exec_module(runtime)

class RuntimeConnectionTests(unittest.TestCase):
    def test_password_delimiters_are_quoted_as_adonet_not_shell(self):
        value=runtime.connection('x'*24+';"$`', 'isolated-sql,1433','AndersonMultiMarcas')
        self.assertIn('User Id=anderson_runtime;',value)
        self.assertIn('Password="'+'x'*24+';""$`";',value)

    def test_newline_and_host_injection_are_refused(self):
        for password,host in [('x'*24+'\n','sql'),('x'*24,'sql;User Id=sa')]:
            with self.assertRaises(ValueError):runtime.connection(password,host,'AndersonMultiMarcas')

    def test_private_output_never_overwrites_existing_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory).resolve();root.chmod(0o700)
            secret=root/'password';secret.write_text('x'*30);secret.chmod(0o600)
            output=root/'runtime.env'
            argv=['render_runtime_connection.py','--password-file',str(secret),'--host','isolated','--output',str(output)]
            with mock.patch.object(sys,'argv',argv):runtime.main()
            self.assertEqual(0o600,output.stat().st_mode&0o777)
            before=output.read_bytes()
            with mock.patch.object(sys,'argv',argv),self.assertRaises(FileExistsError):runtime.main()
            self.assertEqual(before,output.read_bytes())

if __name__=='__main__':unittest.main()
