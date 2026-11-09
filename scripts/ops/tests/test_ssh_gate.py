import importlib.util
from pathlib import Path
import shlex
import tempfile
import unittest
from unittest import mock

spec = importlib.util.spec_from_file_location('amm_ssh_gate', Path(__file__).parents[1] / 'ssh_gate.py')
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class SshGateTests(unittest.TestCase):
    def test_shell_sftp_reads_and_unrelated_paths_are_rejected(self):
        for command in ['id', 'bash', 'internal-sftp', 'scp -f /etc/passwd',
                        'scp -t /opt/startingweb/apps/anderson-multimarcas',
                        'python3 -c "print(1)"', 'anderson-preflight; id']:
            with self.subTest(command=command), self.assertRaises(ValueError):
                gate.classify(command)

    def test_protocol_only_allows_fresh_scoped_directory_and_receive(self):
        with tempfile.TemporaryDirectory() as directory, mock.patch.object(gate, 'ROOT', Path(directory).resolve()):
            (gate.ROOT / 'incoming').mkdir(mode=0o700)
            dest = gate.ROOT / 'incoming' / ('a' * 40 + '-1-1')
            command = f'umask 077; test -d {gate.ROOT}/incoming && mkdir {dest}'
            self.assertEqual(('mkdir', dest), gate.classify(command))
            gate.execute(command)
            self.assertEqual(('upload', dest), gate.classify('scp -t ' + str(dest) + '/'))
            with self.assertRaises(FileExistsError):
                gate.execute(command)
            with self.assertRaises(ValueError):
                gate.classify(command + '; id')

    def test_arbitrary_python_or_changed_executor_is_never_run(self):
        with tempfile.TemporaryDirectory() as directory, mock.patch.object(gate, 'ROOT', Path(directory).resolve()):
            (gate.ROOT / 'incoming').mkdir(mode=0o700)
            (gate.ROOT / 'bin').mkdir(mode=0o700)
            (gate.ROOT / 'bin/release.py').write_text('# reviewed fixture')
            dest = gate.ROOT / 'incoming' / ('a' * 40 + '-1-1')
            dest.mkdir(mode=0o700)
            (dest / 'release.py').write_text('# unapproved fixture')
            command = shlex.join(['python3','-c',gate.VERIFY_CODE,str(dest/'release.py'),'0'*64,
                                  'deploy','--root',str(gate.ROOT),'--bundle',str(dest),'--manifest-sha256','1'*64])
            self.assertEqual('executor', gate.classify(command)[0])
            with mock.patch.object(gate.subprocess, 'run') as run:
                with self.assertRaises(ValueError):
                    gate.execute(command)
                run.assert_not_called()
            with self.assertRaises(ValueError):
                gate.classify(command.replace('import hashlib,runpy,sys;', 'import os;'))


if __name__ == '__main__':
    unittest.main()
