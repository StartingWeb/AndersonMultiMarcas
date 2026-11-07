#!/usr/bin/env python3
"""Read-only R2 configuration and signed HeadBucket audit. No object requests."""
from datetime import datetime, timezone
import hashlib
import hmac
import json
from pathlib import Path
import re
import urllib.error
import urllib.parse
import urllib.request
import importlib.util
import os


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


def audit(values):
    required = ['AccountId','AccessKeyId','SecretAccessKey','BucketName','PublicBaseUrl','ServiceUrl','Region']
    options = {k: values.get('Storage__R2__' + k, '') for k in required}
    report = {'checked_at': datetime.now(timezone.utc).isoformat(),
              'provider': values.get('Storage__Provider'),
              'dual_read_enabled': values.get('Storage__DualReadEnabled'),
              'configured_fields': {k: bool(v) for k,v in options.items()},
              'region': options['Region'], 'object_requests': 0,
              'production_uploads': 0, 'production_deletions': 0,
              'test_bucket_configured': False, 'write_cycle_test': 'blocked_no_exclusive_test_bucket'}
    endpoint = urllib.parse.urlsplit(options['ServiceUrl'])
    public = urllib.parse.urlsplit(options['PublicBaseUrl'])
    report['endpoint_is_account_r2_https'] = (endpoint.scheme == 'https' and
        endpoint.hostname == options['AccountId'] + '.r2.cloudflarestorage.com' and
        not endpoint.username and not endpoint.password and endpoint.path in ('', '/'))
    report['public_url_is_https'] = public.scheme == 'https' and bool(public.hostname) and not public.username
    if not report['endpoint_is_account_r2_https'] or not all(options.values()):
        report['head_bucket'] = 'configuration_failed'
        return report
    bucket = options['BucketName']
    if not re.fullmatch(r'[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]', bucket):
        report['head_bucket'] = 'configuration_failed'
        return report
    stamp = datetime.now(timezone.utc)
    amzdate, datestamp = stamp.strftime('%Y%m%dT%H%M%SZ'), stamp.strftime('%Y%m%d')
    payload_hash = hashlib.sha256(b'').hexdigest()
    uri = '/' + urllib.parse.quote(bucket, safe='')
    headers = 'host:' + endpoint.netloc + '\n' + 'x-amz-content-sha256:' + payload_hash + '\n' + 'x-amz-date:' + amzdate + '\n'
    signed = 'host;x-amz-content-sha256;x-amz-date'
    request = '\n'.join(['HEAD',uri,'',headers,signed,payload_hash])
    scope = datestamp + '/' + options['Region'] + '/s3/aws4_request'
    to_sign = '\n'.join(['AWS4-HMAC-SHA256',amzdate,scope,hashlib.sha256(request.encode()).hexdigest()])
    key = ('AWS4' + options['SecretAccessKey']).encode()
    for part in [datestamp, options['Region'], 's3', 'aws4_request']:
        key = hmac.new(key, part.encode(), hashlib.sha256).digest()
    signature = hmac.new(key,to_sign.encode(),hashlib.sha256).hexdigest()
    auth = 'AWS4-HMAC-SHA256 Credential=' + options['AccessKeyId'] + '/' + scope + ', SignedHeaders=' + signed + ', Signature=' + signature
    req = urllib.request.Request(options['ServiceUrl'].rstrip('/') + uri, method='HEAD',
        headers={'x-amz-date':amzdate,'x-amz-content-sha256':payload_hash,'Authorization':auth})
    try:
        with urllib.request.build_opener(NoRedirect()).open(req, timeout=20) as response:
            report['head_bucket_http_status'] = response.status
            report['head_bucket'] = 'passed' if response.status == 200 else 'review_required'
    except urllib.error.HTTPError as error:
        report.update(head_bucket='rejected',head_bucket_http_status=error.code)
    except Exception:
        report['head_bucket'] = 'transport_failed'
    return report


if __name__ == '__main__':
    os.umask(0o077)
    root = Path('/opt/startingweb/apps/anderson-multimarcas-unified')
    spec = importlib.util.spec_from_file_location('amm_backup', Path(__file__).with_name('backup.py'))
    backup = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(backup)
    values = dict(line.split('=',1) for line in backup.private(root/'shared/runtime.env').read_text().splitlines() if line and not line.startswith('#'))
    result = audit(values)
    backup.atomic_json(root/'history/r2-validation.json',result)
    print(json.dumps(result))
