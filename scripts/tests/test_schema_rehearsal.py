"""Offline checks of the isolated rehearsal's form and Identity boundaries."""
import base64
import hashlib
import importlib.util
from pathlib import Path
import struct
import unittest

spec=importlib.util.spec_from_file_location('rehearsal',Path(__file__).parents[1]/'compatibility/rehearse.py')
rehearsal=importlib.util.module_from_spec(spec);spec.loader.exec_module(rehearsal)

class RehearsalBoundaryTests(unittest.TestCase):
    def test_antiforgery_and_encoded_fields_are_preserved_without_executing_html(self):
        form=rehearsal.Form('<input name="__RequestVerificationToken" value="token&amp;fixture"><input name="Id" value="7"><script>raise()</script>')
        self.assertEqual({'__RequestVerificationToken':'token&fixture','Id':'7'},form.fields)

    def test_unchecked_checkbox_does_not_override_a_checked_value(self):
        form=rehearsal.Form('<input name="Ativo" type="checkbox" checked value="true"><input name="Other" type="checkbox" value="true">')
        self.assertEqual({'Ativo':'true'},form.fields)

    def test_identity_v3_hash_has_a_verifiable_key_and_explicit_work_factor(self):
        raw=base64.b64decode(rehearsal.password_hash('synthetic-only',b'x'*16))
        self.assertEqual(1,raw[0]);self.assertEqual((2,100000,16),struct.unpack('>III',raw[1:13]))
        self.assertEqual(hashlib.pbkdf2_hmac('sha512',b'synthetic-only',raw[13:29],100000,32),raw[29:])

if __name__=='__main__':unittest.main()
