import argparse,importlib.util,json,subprocess,tempfile
from pathlib import Path
repository=Path(__file__).resolve().parents[3]
s=importlib.util.spec_from_file_location('p',repository/'scripts/ops/provision_runtime.py');p=importlib.util.module_from_spec(s);s.loader.exec_module(p)
original_run=p.run
def checked(argv,*args,**kwargs):
 if argv and argv[0]=='python3':
  result=subprocess.run(argv,capture_output=True,text=True)
  if result.returncode:raise RuntimeError(result.stderr.strip())
  return result.stdout
 return original_run(argv,*args,**kwargs)
p.run=checked
parser=argparse.ArgumentParser(description='Disposable installer integration only; never production.')
parser.add_argument('--container',required=True);parser.add_argument('--schema-sql',type=Path,required=True)
args=parser.parse_args();container=args.container
info=json.loads(p.run(['docker','inspect',container]))[0]
if (info.get('Config',{}).get('Labels') or {}).get('anderson.purpose') not in {'disposable-local','disposable-ci'}:raise SystemExit('Disposable SQL label required')
p.admin(container,'CREATE DATABASE AndersonMultiMarcas;',database='master')
p.admin(container,args.schema_sql.read_text(encoding='utf-8-sig'))
info=json.loads(p.run(['docker','inspect',container]))[0]
x=json.loads(''.join(p.admin(container,"SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS server_name,CONVERT(nvarchar(36),database_guid) AS database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID() FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;").splitlines()))
with tempfile.TemporaryDirectory() as directory:
 root=Path(directory).resolve();root.chmod(0o700);(root/'shared').mkdir(mode=0o700)
 env=root/'shared/runtime.env';env.write_text('ConnectionStrings__DefaultConnectionProd=Server=sqlserver,1433;Database=AndersonMultiMarcas;User Id=sa;Password=synthetic-old;\nStorage__Provider=R2\n');env.chmod(0o600)
 argv=['python3',str(repository/'scripts/ops/provision_runtime.py'),'--mode','trial','--container',container,'--root',str(root),'--expected-container',info['Id'],'--expected-server',x['server_name'],'--expected-database-guid',x['database_guid'],'--authorization-ref','isolated-installer-test','--grants',str(repository/'deploy/sql/runtime_permissions.production.sql')]
 p.run(argv);print('PASS preflight no DDL')
 p.run(argv+['--execute']);print('PASS installer and effective permissions')
 first=(root/'shared/credentials/anderson_runtime.password').read_bytes()
 p.run(argv+['--execute']);assert first==(root/'shared/credentials/anderson_runtime.password').read_bytes();print('PASS replay without credential rotation')
 password=first.decode().strip()
 # Synthetic data only; never executed against the production target.
 p.runtime(container,password,"BEGIN TRAN; INSERT dbo.Marca(Nome,LogoUrl,DataCadastro,Ativo) VALUES(N'Installer synthetic',N'',SYSUTCDATETIME(),1); UPDATE dbo.Marca SET Nome=N'Installer edited' WHERE Nome=N'Installer synthetic'; SELECT COUNT(*) FROM dbo.Marca; DELETE dbo.Marca WHERE Nome=N'Installer edited'; ROLLBACK;")
 print('PASS isolated runtime CRUD')
 for statement in ['CREATE TABLE dbo.Forbidden(Id int)','CREATE USER Forbidden WITHOUT LOGIN','ALTER TABLE dbo.Marca ADD Forbidden int','CREATE DATABASE Forbidden']:
  try:p.runtime(container,password,statement)
  except p.Refused:pass
  else:raise AssertionError('Administrative operation was permitted')
 print('PASS isolated administrative denials')
 assert 'User Id=anderson_runtime' in env.read_text();assert (env.stat().st_mode&0o777)==0o600
 print('PASS private prepared connection and preserved other setting')
p.admin(container,'USE master; ALTER DATABASE AndersonMultiMarcas SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE AndersonMultiMarcas; DROP LOGIN anderson_runtime;',database='master')
