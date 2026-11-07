import importlib.util
from pathlib import Path
import unittest
import tempfile

spec = importlib.util.spec_from_file_location('amm_restore', Path(__file__).parents[1] / 'restore_check.py')
restore = importlib.util.module_from_spec(spec)
spec.loader.exec_module(restore)


class StructuralComparisonTests(unittest.TestCase):
    def test_ef_utf8_bom_is_removed_for_stdin_without_changing_artifact(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'migration.sql'
            source = b'\xef\xbb\xbfSELECT 1;\nGO\n'
            path.write_bytes(source)
            self.assertEqual('SELECT 1;\nGO\n', restore.read_sql_script(path))
            self.assertEqual(source, path.read_bytes())

    def test_empty_sql_json_collections_compare_as_empty_lists(self):
        self.assertTrue(all(not value['missing'] and not value['additional'] for value in
                            restore.structural_diff({}, {'checks': []}).values()))

    def test_column_or_fk_changes_are_not_hidden_by_row_order(self):
        original = {'columns': [{'table': 'Loja', 'name': 'Cnpj', 'is_nullable': True}],
                    'foreign_keys': [{'name': 'FK_Example', 'is_not_trusted': False}]}
        changed = {'columns': [{'table': 'Loja', 'name': 'Cnpj', 'is_nullable': False}],
                   'foreign_keys': [{'name': 'FK_Example', 'is_not_trusted': True}]}
        result = restore.structural_diff(original, changed)
        self.assertEqual(1, len(result['columns']['missing']))
        self.assertEqual(1, len(result['foreign_keys']['additional']))


if __name__ == '__main__':
    unittest.main()
