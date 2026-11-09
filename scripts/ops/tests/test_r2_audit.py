import importlib.util
from pathlib import Path
import unittest
from unittest import mock

spec = importlib.util.spec_from_file_location('amm_r2', Path(__file__).parents[1] / 'validate_r2.py')
r2 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r2)


class R2AuditSafetyTests(unittest.TestCase):
    def values(self):
        return {'Storage__Provider': 'R2', **{'Storage__R2__'+key:value for key,value in {
            'AccountId':'synthetic-account','AccessKeyId':'synthetic-access','SecretAccessKey':'synthetic-secret',
            'BucketName':'synthetic-test-bucket','Region':'auto',
            'PublicBaseUrl':'https://images.example.test',
            'ServiceUrl':'https://synthetic-account.r2.cloudflarestorage.com'}.items()}}

    def test_authenticated_probe_is_head_bucket_only_without_object_operations(self):
        opener = mock.Mock()
        response = mock.MagicMock()
        response.__enter__.return_value.status = 200
        opener.open.return_value = response
        with mock.patch.object(r2.urllib.request, 'build_opener', return_value=opener):
            result = r2.audit(self.values())
        request = opener.open.call_args.args[0]
        self.assertEqual('HEAD', request.get_method())
        self.assertEqual('/synthetic-test-bucket', r2.urllib.parse.urlsplit(request.full_url).path)
        self.assertEqual('passed', result['head_bucket'])
        self.assertEqual(0, result['object_requests'])
        self.assertNotIn('synthetic-secret', str(result))

    def test_unexpected_endpoint_never_receives_credentials(self):
        values = self.values()
        values['Storage__R2__ServiceUrl'] = 'https://unrelated.example.test'
        with mock.patch.object(r2.urllib.request, 'build_opener') as opener:
            self.assertEqual('configuration_failed', r2.audit(values)['head_bucket'])
            opener.assert_not_called()


if __name__ == '__main__':
    unittest.main()
