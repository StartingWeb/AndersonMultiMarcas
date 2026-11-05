"""Keep browser-facing source and assets valid UTF-8."""
from pathlib import Path
import unittest


class TextEncodingTests(unittest.TestCase):
    def test_browser_text_is_valid_utf8_without_replacement_characters(self):
        project = Path(__file__).resolve().parents[2] / "Project"
        checked = 0
        for path in project.rglob("*"):
            if path.suffix.lower() not in {".cshtml", ".js", ".css", ".json"}:
                continue
            if {"bin", "obj"}.intersection(path.relative_to(project).parts):
                continue
            with self.subTest(path=str(path.relative_to(project))):
                text = path.read_bytes().decode("utf-8-sig")
                self.assertNotIn("\ufffd", text)
                checked += 1
        self.assertGreater(checked, 50)


if __name__ == "__main__":
    unittest.main()
