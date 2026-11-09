#!/usr/bin/env python3
"""Install only Anderson's dedicated runtime login and private future connection.

No application deployment, container restart, schema migration or commercial
record write. Production requires explicit execution and immutable identities.
Secrets are generated on the target, stored privately and passed to sqlcmd via
stdin, never command arguments or output. Trial mode accepts owned disposable SQL.
"""
import argparse
from contextlib import contextmanager
import fcntl
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import stat
import subprocess
import uuid

ROOT=Path('/opt/startingweb/apps/anderson-multimarcas-unified')
LOGIN='anderson_runtime'

class Refused(RuntimeError):pass

def literal(value):
    if any(ord(c)<32 for c in value):raise Refused('Control characters refused')
    return "N'"+value.replace("'","''")+"'"

def private(path,directory=False):
    info=path.lstat();kind=stat.S_ISDIR if directory else stat.S_ISREG
    if not kind(info.st_mode) or info.st_uid!=os.geteuid() or info.st_mode&0o077 or any(p.is_symlink() for p in path.parents):
        raise Refused('Private path ownership/type/permissions refused')
    return path

def atomic(path,data):
    temporary=path.with_name('.'+path.name+'-'+secrets.token_hex(6))
    fd=os.open(temporary,os.O_CREAT|os.O_EXCL|os.O_WRONLY|os.O_NOFOLLOW,0o600)
    try:
        with os.fdopen(fd,'w') as stream:stream.write(data);stream.flush();os.fsync(stream.fileno())
        os.replace(temporary,path)
    finally:temporary.unlink(missing_ok=True)

def run(argv,data=None):
    result=subprocess.run(argv,input=data,capture_output=True,text=True,timeout=120)
    if result.returncode:raise Refused('External operation refused; sensitive output suppressed')
    return result.stdout

def admin(container,query,database='AndersonMultiMarcas'):
    return run(['docker','exec','-i',container,'sh','-c',
        'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" exec /opt/mssql-tools18/bin/sqlcmd -C -I -S localhost -U sa -b -y 0 -w 65535 -d '+database],
        'SET NOCOUNT ON;\n'+query)

def runtime(container,password,query):
    return run(['docker','exec','-i',container,'sh','-c',
        'IFS= read -r runtime_password; export SQLCMDPASSWORD="$runtime_password"; '
        'exec /opt/mssql-tools18/bin/sqlcmd -C -I -S localhost -U anderson_runtime -b -y 0 -w 65535 -d AndersonMultiMarcas'],
        password+'\nSET NOCOUNT ON;\n'+query)

def validate_target(args,info):
    if info.get('Id')!=args.expected_container:raise Refused('SQL container identity changed')
    if args.mode=='production':
        if args.container!='sqlserver' or args.root!=ROOT:raise Refused('Unexpected production destination')
    elif not ((info.get('Config',{}).get('Labels') or {}).get('anderson.purpose')in {'disposable-local','disposable-ci'}
              or (info.get('Config',{}).get('Labels') or {}).get('com.startingweb.anderson-runtime-trial')):
        raise Refused('Trial target lacks disposable ownership label')
    if not re.fullmatch('[A-Za-z0-9_.-]{1,128}',args.expected_server):raise Refused('Invalid SQL identity')
    uuid.UUID(args.expected_database_guid)
    if not re.fullmatch('[A-Za-z0-9_.:-]{8,160}',args.authorization_ref):raise Refused('Authorization reference required')

@contextmanager
def installer_lock(root):
    private(root,True);private(root/'shared',True)
    path=root/'shared/runtime-provision.lock'
    descriptor=os.open(path,os.O_CREAT|os.O_RDWR|os.O_NOFOLLOW,0o600)
    try:
        private(path)
        try:fcntl.flock(descriptor,fcntl.LOCK_EX|fcntl.LOCK_NB)
        except BlockingIOError:raise Refused('Another runtime installation is active') from None
        yield
    finally:os.close(descriptor)


def main():
    os.umask(0o077)
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mode',choices=('trial','production'),required=True)
    parser.add_argument('--container',required=True)
    parser.add_argument('--root',type=Path,required=True)
    parser.add_argument('--expected-container',required=True)
    parser.add_argument('--expected-server',required=True)
    parser.add_argument('--expected-database-guid',required=True)
    parser.add_argument('--authorization-ref',required=True)
    parser.add_argument('--grants',type=Path,required=True)
    parser.add_argument('--execute',action='store_true')
    args=parser.parse_args()
    args.root=args.root.resolve()
    with installer_lock(args.root):
        apply(args)

def apply(args):
    info=json.loads(run(['docker','inspect',args.container]))[0]
    validate_target(args,info)
    private(args.root,True);shared=private(args.root/'shared',True)
    env_path=private(shared/'runtime.env')
    observed=json.loads(''.join(admin(args.container,"""SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS server_name,
      CONVERT(nvarchar(36),database_guid) AS database_guid FROM sys.database_recovery_status
      WHERE database_id=DB_ID() FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;""").splitlines()))
    if observed['server_name']!=args.expected_server or uuid.UUID(observed['database_guid'])!=uuid.UUID(args.expected_database_guid):
        raise Refused('SQL server/database identity changed')
    policy=args.grants.read_text(encoding='utf-8-sig')
    if 'Production runtime permissions ONLY' not in policy or '51110' not in policy:raise Refused('Reviewed production policy required')
    original=env_path.read_text()
    values=dict(line.split('=',1) for line in original.splitlines() if line and not line.startswith('#') and '=' in line)
    if 'ConnectionStrings__DefaultConnectionProd' not in values:raise Refused('Prepared production connection missing')
    # Preserve the same audited SQL target, not an unvalidated host from argv.
    old_connection=values['ConnectionStrings__DefaultConnectionProd']
    host=re.search(r'(?:^|;)\s*(?:Server|Data Source)\s*=\s*([^;]+)',old_connection,re.I)
    if not host or host.group(1).strip() not in {'sqlserver','sqlserver,1433'}:raise Refused('Prepared SQL host is outside the audited Docker alias')
    database=re.search(r'(?:^|;)\s*(?:Database|Initial Catalog)\s*=\s*([^;]+)',old_connection,re.I)
    if not database or database.group(1).strip()!='AndersonMultiMarcas':raise Refused('Unexpected prepared database')
    if any(k.startswith('ConnectionStrings__') and k!='ConnectionStrings__DefaultConnectionProd' and v for k,v in values.items()):
        raise Refused('Unexpected additional configured connection')
    receipt_path=shared/'runtime-provisioned.json'
    receipt=json.loads(private(receipt_path).read_text()) if receipt_path.exists() else None
    existing=int(admin(args.container,"SELECT CASE WHEN SUSER_ID(N'anderson_runtime') IS NULL THEN 0 ELSE 1 END;").strip())
    if existing and (not receipt or receipt.get('container_id')!=info['Id'] or receipt.get('server_name')!=observed['server_name'] or receipt.get('database_guid')!=observed['database_guid']):
        raise Refused('Existing login is not owned by this installation; no adoption or rotation')
    if not args.execute:
        print(json.dumps({'status':'preflight_passed','login_exists':bool(existing),'changes':False}));return
    credentials=shared/'credentials'
    if not credentials.exists():credentials.mkdir(mode=0o700)
    private(credentials,True)
    password_path=credentials/'anderson_runtime.password'
    if password_path.exists():password=private(password_path).read_text().rstrip('\n')
    else:
        if existing:raise Refused('Existing login has no owned credential')
        password='Rt9!'+secrets.token_hex(32)
        fd=os.open(password_path,os.O_CREAT|os.O_EXCL|os.O_WRONLY|os.O_NOFOLLOW,0o600)
        with os.fdopen(fd,'w') as stream:stream.write(password+'\n');stream.flush();os.fsync(stream.fileno())
    if not re.fullmatch('Rt9![a-f0-9]{64}',password):raise Refused('Unexpected credential format')
    if not existing:
        admin(args.container,'CREATE LOGIN anderson_runtime WITH PASSWORD='+literal(password)+
              ', CHECK_POLICY=ON, CHECK_EXPIRATION=OFF; DENY CREATE ANY DATABASE,ALTER ANY LOGIN,ALTER SERVER STATE TO anderson_runtime;',database='master')
        # Record ownership immediately; a later grant failure must not orphan an
        # unrecognizable login or cause a password rotation on retry.
        receipt={'container_id':info['Id'],'server_name':observed['server_name'],'database_guid':observed['database_guid'],
                 'login':LOGIN,'status':'login_created','authorization':args.authorization_ref}
        atomic(receipt_path,json.dumps(receipt,indent=2)+'\n')
    context='\n'.join('EXEC sys.sp_set_session_context @key='+literal(key)+',@value='+literal(value)+';' for key,value in {
        'Anderson.ExpectedServer':observed['server_name'],'Anderson.ExpectedDatabaseGuid':observed['database_guid'],
        'Anderson.Scope':'runtime-permissions','Anderson.Authorization':args.authorization_ref}.items())
    admin(args.container,context+'\n'+policy)
    # This is a read-only verification in production. Real DML and negative DDL
    # tests run only against disposable copies, never on the live database.
    evidence=json.loads(''.join(runtime(args.container,password,"""SELECT USER_NAME() AS database_user,
      IS_SRVROLEMEMBER('sysadmin') AS sysadmin,IS_MEMBER('db_owner') AS db_owner,
      HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE TABLE') AS create_table,
      HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER ANY USER') AS alter_user,
      HAS_PERMS_BY_NAME(NULL,NULL,'CREATE ANY DATABASE') AS create_database,
      HAS_PERMS_BY_NAME(NULL,NULL,'ALTER ANY LOGIN') AS alter_login,
      (SELECT COUNT(*) FROM sys.fn_my_permissions('dbo.Marca','OBJECT') WHERE subentity_name=N'' AND permission_name IN ('SELECT','INSERT','UPDATE','DELETE')) AS brand_crud_permissions
      FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;""").splitlines()))
    if (evidence.get('database_user')!=LOGIN or evidence.get('brand_crud_permissions')!=4
            or any(evidence.get(k)!=0 for k in ('sysadmin','db_owner','create_table','alter_user','create_database','alter_login'))):
        raise Refused('Effective runtime permissions differ from the approved profile')
    new_connection='Server=sqlserver;Database=AndersonMultiMarcas;User Id=anderson_runtime;Password='+password+';Encrypt=True;TrustServerCertificate=True;'
    lines=[]
    for line in original.splitlines():
        key,sep,value=line.partition('=')
        if key=='ConnectionStrings__DefaultConnectionProd':line=key+'='+new_connection
        elif key.startswith('ConnectionStrings__') and value:raise Refused('Unexpected additional configured connection; review before installation')
        lines.append(line)
    updated='\n'.join(lines)+'\n'
    before_sha=hashlib.sha256(original.encode()).hexdigest()
    if updated!=original:
        backup_path=shared/('runtime.env.before-runtime-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ'))
        fd=os.open(backup_path,os.O_CREAT|os.O_EXCL|os.O_WRONLY|os.O_NOFOLLOW,0o600)
        with os.fdopen(fd,'w') as stream:stream.write(original);stream.flush();os.fsync(stream.fileno())
        atomic(env_path,updated)
    receipt.update(status='verified_and_prepared',permissions=evidence,policy_sha256=hashlib.sha256(policy.encode()).hexdigest(),
                   completed_at=datetime.now(timezone.utc).isoformat(),previous_runtime_sha256=before_sha,
                   runtime_sha256=hashlib.sha256(updated.encode()).hexdigest(),production_business_writes=0)
    atomic(receipt_path,json.dumps(receipt,indent=2)+'\n')
    print(json.dumps({'status':receipt['status'],'login':LOGIN,'permissions':evidence,'credential_mode':'0600',
                      'application_started':False,'schema_migrations':False,'business_writes':0}))

if __name__=='__main__':
    try:main()
    except Exception as error:raise SystemExit('Runtime provisioning refused: '+(str(error) if isinstance(error,Refused) else type(error).__name__)) from None
