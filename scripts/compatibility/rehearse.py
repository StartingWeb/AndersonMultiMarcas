#!/usr/bin/env python3
"""Exercise old/new binaries on a restored SQL copy, never production writes.

Run on the authorized VPS. Only fixed, audited backup/application roots are read.
All mutable resources have a random prefix and ownership label; no ports or
production networks/mounts/credentials are inherited. Reports contain statuses,
counts, technical IDs, hashes and errors numbers, never pages or customer values.
"""
import argparse
import base64
from datetime import datetime, timezone
import hashlib
from html.parser import HTMLParser
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import signal
import struct
import subprocess
import time
from urllib.parse import urlencode
import uuid

ROOT = Path('/opt/startingweb/apps/anderson-multimarcas-unified')
BACKUPS = Path('/opt/startingweb/backups/anderson-multimarcas')
LABEL = 'com.startingweb.anderson-compatibility'
LEGACY = 'sha256:605b1b3cd5f1485442fa2ddfb8bd6e7ac68edcffe63f95c1fa39f8ef98e66df5'


class Refused(RuntimeError):
    pass


class Form(HTMLParser):
    def __init__(self, html):
        super().__init__(); self.fields = {}; self.feed(html)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == 'input' and attrs.get('name'):
            if attrs.get('type') not in ('submit', 'file', 'checkbox', 'radio'):
                self.fields[attrs['name']] = attrs.get('value', '')
            elif attrs.get('type') in ('checkbox', 'radio') and 'checked' in attrs:
                self.fields[attrs['name']] = attrs.get('value', 'true')


def password_hash(password, salt):
    # ASP.NET Identity V3, HMACSHA512, same non-production test identity for both binaries.
    return base64.b64encode(b'\x01' + struct.pack('>III', 2, 100000, len(salt)) + salt +
                            hashlib.pbkdf2_hmac('sha512', password.encode(), salt, 100000, 32)).decode()


def main():
    os.umask(0o077)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--schema-dir', type=Path, required=True)
    parser.add_argument('--runtime-grants', type=Path, required=True)
    args = parser.parse_args()
    run = secrets.token_hex(6); prefix = 'amm-compat-' + run
    target = ROOT / 'snapshots' / prefix
    target.mkdir(mode=0o700)
    report = {'run': run, 'started_at': datetime.now(timezone.utc).isoformat(),
              'status': 'running', 'phases': [], 'negative_permissions': [],
              'production_writes': 0, 'external_storage_requests': 0}
    containers, volumes = [], []; network_created = False
    report['source_hashes'] = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [Path(__file__),args.runtime_grants,*sorted(args.schema_dir.glob('*.sql'))]}
    def interrupted(*_): raise Refused('Isolated rehearsal interrupted')
    signal.signal(signal.SIGTERM, interrupted)
    sanitized_config = target/'appsettings.sanitized.json'
    sanitized_config.write_text(json.dumps({'Logging':{'LogLevel':{'Default':'Warning'}},'AllowedHosts':'*'}))
    sanitized_config.chmod(0o644)
    admin_password = 'Aa9!' + secrets.token_hex(24)
    runtime_password = 'Rt9!' + secrets.token_hex(24)
    user_password = 'Ui9!' + secrets.token_hex(24)
    email = 'rehearsal-' + run + '@example.invalid'
    sql_name = prefix + '-sql'

    def save():
        temp = target / '.report.tmp'
        temp.write_text(json.dumps(report, indent=2, sort_keys=True) + '\n')
        temp.chmod(0o600); os.replace(temp, target / 'report.json')

    def command(*argv, data=None, timeout=900, binary=False, allow_failure=False):
        r = subprocess.run(argv, input=data, capture_output=True, text=not binary, timeout=timeout)
        if r.returncode and not allow_failure:
            raise Refused('Isolated command failed: ' + argv[0] + ' ' + (argv[1] if len(argv)>1 else '') + ' exit=' + str(r.returncode))
        return r if allow_failure else r.stdout

    def docker(*argv, **kwargs):
        return command('docker', *argv, **kwargs)

    def sql(query, *, runtime=False, allow_failure=False, database='AndersonSchemaTrial'):
        user, password = ('anderson_runtime', runtime_password) if runtime else ('sa', admin_password)
        r = docker('exec', '-i', '-e', 'SQLCMDPASSWORD=' + password, sql_name,
                   '/opt/mssql-tools18/bin/sqlcmd', '-C', '-I', '-S', 'localhost', '-U', user,
                   '-b', '-l', '5', '-y', '0', '-w', '65535', '-d', database,
                   data='SET NOCOUNT ON;SET QUOTED_IDENTIFIER ON;SET ANSI_NULLS ON;SET ANSI_WARNINGS ON;'
                        'SET ANSI_PADDING ON;SET ARITHABORT ON;SET CONCAT_NULL_YIELDS_NULL ON;'
                        'SET NUMERIC_ROUNDABORT OFF;\nGO\n' + query,
                   allow_failure=True)
        if r.returncode and not allow_failure:
            raise Refused('Isolated SQL failed; error numbers=' + ','.join(re.findall(r'Msg (\d+)', r.stdout+r.stderr)))
        return r if allow_failure else r.stdout

    def scalar(query, runtime=False):
        return int(sql(query, runtime=runtime).strip())

    def script(name):
        return (args.schema_dir / name).read_text(encoding='utf-8-sig')

    def digest(path):
        h = hashlib.sha256()
        with path.open('rb') as stream:
            for block in iter(lambda: stream.read(1048576), b''): h.update(block)
        return h.hexdigest()

    def snapshot():
        raw = sql((BACKUPS/'bin/schema.sql').read_text())
        data = json.loads(''.join(raw.splitlines()))
        for k in ('columns','indexes','foreign_keys','checks','migrations','counts'): data.setdefault(k,[])
        return data

    def assert_owned(kind, name):
        info = json.loads(docker(kind, 'inspect', name))[0]
        labels = info.get('Config',{}).get('Labels') if kind=='container' else info.get('Labels')
        if (labels or {}).get(LABEL) != run: raise Refused('Cleanup ownership check failed')

    def request(app, path, fields=None, upload=None):
        argv = ['exec', '-i', app, 'curl', '--silent', '--max-time', '40',
                '--header', 'X-Forwarded-Proto: https', '--cookie', '/tmp/rehearsal.cookies',
                '--cookie-jar', '/tmp/rehearsal.cookies', '--write-out', '\n%{http_code}',
                '--dump-header', '/tmp/rehearsal.headers']
        data = None
        if path.startswith('/uploads/'):
            argv += ['--output','/dev/null']
        if upload:
            for key,value in fields.items(): argv.extend(['--form-string', key+'='+str(value)])
            argv.extend(['--form', 'NovasMidias=@/tmp/synthetic.png;type=image/png'])
        elif fields is not None:
            argv += ['--data-binary', '@-', '--header', 'Content-Type: application/x-www-form-urlencoded']
            data = urlencode(fields)
        raw = docker(*argv, 'http://127.0.0.1:8080'+path, data=data, timeout=90)
        body, code = raw.rsplit('\n',1)
        headers = docker('exec', app, 'cat', '/tmp/rehearsal.headers')
        location = re.search(r'(?im)^Location:\s*([^\r\n]+)', headers)
        return int(code), body, location.group(1) if location else ''

    def phase(image, label, widened=False):
        app = prefix + '-web-' + label
        containers.append(app)
        env = {'ASPNETCORE_ENVIRONMENT':'Production','DOTNET_ENVIRONMENT':'Production',
               'ASPNETCORE_HTTP_PORTS':'8080','ASPNETCORE_URLS':'http://+:8080',
               'Database__ApplyMigrationsOnStartup':'false','StorageMigration__Execute':'false',
               'LegacyImageImport__RecoverPendingJobsOnStartup':'false','DevelopmentBootstrap__Enabled':'false',
               'Storage__Provider':'Local','Storage__DualReadEnabled':'false',
               'Storage__R2__AccessKeyId':'','Storage__R2__SecretAccessKey':'','Storage__R2__BucketName':'',
               'Storage__R2__ServiceUrl':'','Storage__R2__AccountId':'',
               'DataProtection__ApplicationName':'AndersonCompatibilityTest',
               'DataProtection__KeysPath':'/home/app/.aspnet/DataProtection-Keys',
               'Seo__BaseUrl':'https://rehearsal.invalid'}
        connection = f'Server={sql_name};Database=AndersonSchemaTrial;User Id=anderson_runtime;Password={runtime_password};Encrypt=False;TrustServerCertificate=True'
        for key in ('DefaultConnectionProd','DefaultConnectionDev','DefaultConnection'): env['ConnectionStrings__'+key]=connection
        argv = ['run','--detach','--name',app,'--label',LABEL+'='+run,'--network',prefix,
                '--memory','384m','--memory-swap','384m','--cpus','.6','--cap-drop','ALL',
                '--security-opt','no-new-privileges:true','--log-driver','none',
                '--mount',f'type=volume,src={prefix}-keys,dst=/home/app/.aspnet/DataProtection-Keys',
                '--mount',f'type=volume,src={prefix}-uploads,dst=/app/wwwroot/uploads']
        for filename in ('appsettings.json','appsettings.Production.json','appsettings.Development.json'):
            argv += ['--mount',f'type=bind,src={sanitized_config},dst=/app/{filename},readonly']
        for key,value in env.items(): argv+=['--env',key+'='+value]
        before = snapshot()
        docker(*argv, image)
        result = {'phase':label,'image':image,'schema_widened':widened,'checks':[], 'passed':False}
        report['phases'].append(result); save()
        def passed(name):
            result['checks'].append(name);save();print('PASS '+label+': '+name,flush=True)
        try:
            for _ in range(80):
                ready = docker('exec',app,'curl','--silent','--max-time','2','--output','/dev/null','--write-out','%{http_code}',
                               '--header','X-Forwarded-Proto: https','http://127.0.0.1:8080/health',allow_failure=True)
                if ready.returncode==0 and ready.stdout.strip()=='200': break
                time.sleep(1)
            else: raise Refused('Isolated app readiness failed')
            after = snapshot()
            if any(before[k]!=after[k] for k in ('columns','indexes','foreign_keys','checks','migrations')):
                raise Refused('Application startup modified schema/history')
            passed('startup_without_schema_changes')
            for route in ('/','/Veiculos','/Empresa','/Contato','/Login'):
                code,_,_ = request(app,route)
                if code!=200: raise Refused('Public route failed '+route+' '+str(code))
            passed('public_read_routes')
            code,html,_ = request(app,'/Login')
            fields = Form(html).fields
            fields.update({'Input.Email':email,'Input.Password':user_password,'Input.RememberMe':'false'})
            code,_,location = request(app,'/Login',fields)
            if code not in (302,303) or '/Login' in location: raise Refused('Synthetic login failed '+str(code))
            if request(app,'/Admin')[0]!=200: raise Refused('Authenticated administration unavailable')
            passed('synthetic_identity_login_with_restricted_sql')
            if label=='old_binary_rollback' and report.get('boundary_vehicle'):
                boundary=report['boundary_vehicle'];route='/Admin/Veiculo/Upsert/'+str(boundary['id'])
                code,html,_=request(app,route)
                if code!=200:raise Refused('Old binary cannot read the new boundary-length vehicle')
                values=Form(html).fields
                values.update(boundary['fields']);values['Veiculo.Id']=str(boundary['id']);values['Veiculo.PrecoVenda']='97.000,00'
                code,_,_=request(app,route,values)
                if code not in (302,303) or scalar(f"SELECT COUNT(*) FROM dbo.Veiculo WHERE Id={boundary['id']} AND LEN(Modelo)=150 AND LEN(Versao)=150 AND LEN(Titulo)=180 AND LEN(Cor)=60 AND LEN(Descricao)=4000 AND PrecoVenda=97000")!=1:
                    raise Refused('Old binary did not preserve new boundary-length values during edit')
                passed('old_binary_edits_new_maximum_length_values_without_truncation')
            token_label = 'AMM '+run+' '+label
            def upsert(path, values, table, name):
                code,html,_ = request(app,path)
                if code!=200: raise Refused('Form unavailable '+path+' '+str(code))
                fields = Form(html).fields; fields.update(values)
                code,_,location=request(app,path,fields)
                if code not in (302,303): raise Refused('Form save failed '+path+' '+str(code))
                identity=scalar(f"SELECT Id FROM dbo.{table} WHERE Nome=N'{name}'")
                return identity
            loja_name=token_label+' loja'
            loja_fields={'Loja.Nome':loja_name,'Loja.RazaoSocial':'Empresa sintetica', 'Loja.Cnpj':'',
                         'Loja.Email':'loja@example.invalid','Loja.Telefone':'16999990000',
                         'Loja.Logradouro':'Rua sintetica','Loja.Numero':'1','Loja.Complemento':'',
                         'Loja.Bairro':'Centro','Loja.Cidade':'Cidade teste','Loja.Uf':'25','Loja.Cep':'15900000'}
            loja=upsert('/Admin/Cadastros/Lojas/Upsert',loja_fields,'Loja',loja_name);passed('store_create')
            loja_fields.update({'Loja.Id':str(loja),'Loja.Numero':'2'})
            upsert('/Admin/Cadastros/Lojas/Upsert/'+str(loja),loja_fields,'Loja',loja_name)
            if scalar(f'SELECT COUNT(*) FROM dbo.Loja WHERE Id={loja} AND Numero=N\'2\'')!=1:raise Refused('Store edit not persisted')
            passed('store_edit_read')
            marca_name=token_label+' marca'
            marca_fields={'Marca.Nome':marca_name,'Marca.LogoUrl':'/img/carroDefault.png'}
            marca=upsert('/Admin/Cadastros/Marcas/Upsert',marca_fields,'Marca',marca_name);passed('brand_create')
            marca_fields.update({'Marca.Id':str(marca),'Marca.Nome':marca_name+' edit'})
            upsert('/Admin/Cadastros/Marcas/Upsert/'+str(marca),marca_fields,'Marca',marca_name+' edit');passed('brand_edit_read')
            seller_name=token_label+' vendedor'
            seller_fields={'Vendedor.Nome':seller_name,'Vendedor.LojaId':str(loja),'Vendedor.Email':'seller@example.invalid',
                           'Vendedor.Telefone':'16999990000','Vendedor.Whatsapp':'16999990000','Vendedor.Cpf':'',
                           'Vendedor.FotoUrl':'/img/carroDefault.png','Vendedor.Cargo':'Teste'}
            seller=upsert('/Admin/Cadastros/Vendedores/Upsert',seller_fields,'Vendedor',seller_name);passed('seller_create')
            seller_fields.update({'Vendedor.Id':str(seller),'Vendedor.Cargo':'Teste editado'})
            upsert('/Admin/Cadastros/Vendedores/Upsert/'+str(seller),seller_fields,'Vendedor',seller_name);passed('seller_edit_read')
            path='/Admin/Veiculo/Upsert';code,html,_=request(app,path);fields=Form(html).fields
            fields.update({'Veiculo.LojaId':str(loja),'Veiculo.MarcaId':str(marca),'Veiculo.VendedorId':str(seller),
                           'Veiculo.Modelo':token_label,'Veiculo.Titulo':token_label,'Veiculo.Versao':'Teste',
                           'Veiculo.AnoFabricacao':'2025','Veiculo.AnoModelo':'2026','Veiculo.Cor':'Branco',
                           'Veiculo.Quilometragem':'0','Veiculo.PrecoVenda':'99.000,00','Veiculo.Combustivel':'3',
                           'Veiculo.Cambio':'2','Veiculo.Ativo':'true','Veiculo.Seminovo':'false',
                           'Veiculo.Descricao':'Descricao sintetica','MidiasOrdenadasJson':'[]'})
            code,_,location=request(app,path,fields)
            if code not in (302,303): raise Refused('Vehicle creation failed '+str(code))
            vehicle=scalar(f"SELECT Id FROM dbo.Veiculo WHERE Modelo=N'{token_label}'")
            passed('vehicle_create')
            edit='/Admin/Veiculo/Upsert/'+str(vehicle)
            code,html,_=request(app,edit);fields=Form(html).fields
            fields.update({'Veiculo.Id':str(vehicle),'Veiculo.LojaId':str(loja),'Veiculo.MarcaId':str(marca),
                           'Veiculo.Modelo':token_label,'Veiculo.Titulo':token_label,'Veiculo.Versao':'Editado',
                           'Veiculo.AnoFabricacao':'2025','Veiculo.AnoModelo':'2026','Veiculo.Cor':'Prata',
                           'Veiculo.Quilometragem':'1','Veiculo.PrecoVenda':'98.000,00','Veiculo.Combustivel':'3',
                           'Veiculo.Cambio':'2','Veiculo.Ativo':'true','Veiculo.Seminovo':'true',
                           'Veiculo.Descricao':'Edicao sintetica','MidiasOrdenadasJson':'[]'})
            code,_,_=request(app,edit,fields)
            if code not in (302,303) or scalar(f"SELECT COUNT(*) FROM dbo.Veiculo WHERE Id={vehicle} AND Cor=N'Prata'")!=1:raise Refused('Vehicle edit failed')
            passed('vehicle_edit_read')
            # The shipped default car image is a public, non-customer fixture.
            docker('exec',app,'cp','/app/wwwroot/img/carroDefault.png','/tmp/synthetic.png')
            code,html,_=request(app,edit);fields.update(Form(html).fields)
            code,_,_=request(app,edit+'?handler=Midias',fields,upload=True)
            if code not in (302,303):raise Refused('Synthetic upload failed '+str(code))
            media=sql(f'SELECT Url FROM dbo.VeiculoMidia WHERE VeiculoId={vehicle}').strip()
            if not media.startswith('/uploads/') or request(app,media)[0]!=200:raise Refused('Uploaded reference did not resolve')
            returned=docker('exec',app,'curl','--silent','--fail','--header','X-Forwarded-Proto: https','http://127.0.0.1:8080'+media,binary=True)
            if not returned:raise Refused('Uploaded media response was empty')
            report.setdefault('synthetic_uploads',[]).append({'vehicle':vehicle,'url':media})
            passed('upload_reference_and_bytes')
            sentinel='/uploads/rehearsal-'+label+'.png'
            docker('exec',app,'cp','/tmp/synthetic.png','/app/wwwroot'+sentinel)
            report.setdefault('retained_media',[]).append(sentinel)
            if request(app,'/veiculo/'+str(vehicle)+'/')[0]!=200:raise Refused('Public synthetic vehicle unavailable')
            passed('vehicle_public_queries')
            media_id=scalar(f'SELECT Id FROM dbo.VeiculoMidia WHERE VeiculoId={vehicle}')
            code,html,_=request(app,edit);remove=Form(html).fields
            remove.update({'Veiculo.Id':str(vehicle),'RemoverMidiaIds':str(media_id),'MidiasOrdenadasJson':'[]'})
            code,_,_=request(app,edit+'?handler=Midias',remove)
            if code not in (302,303) or scalar(f'SELECT COUNT(*) FROM dbo.VeiculoMidia WHERE Id={media_id}')!=0:
                raise Refused('Physical media-row removal failed')
            passed('physical_media_row_delete')
            if label=='new_widened':
                code,html,_=request(app,'/Admin/Veiculo/Upsert');boundary_fields=Form(html).fields
                boundary_fields.update({'Veiculo.LojaId':str(scalar('SELECT MIN(Id) FROM dbo.Loja WHERE Ativo=1')),
                    'Veiculo.MarcaId':'1','Veiculo.Modelo':('Boundary '+run).ljust(150,'x'),
                    'Veiculo.Titulo':('Boundary '+run).ljust(180,'x'),'Veiculo.Versao':'v'*150,
                    'Veiculo.Cor':'c'*60,'Veiculo.Descricao':'d'*4000,'Veiculo.AnoFabricacao':'2025',
                    'Veiculo.AnoModelo':'2026','Veiculo.Quilometragem':'0','Veiculo.PrecoVenda':'99.000,00',
                    'Veiculo.Combustivel':'3','Veiculo.Cambio':'2','Veiculo.Ativo':'true','Veiculo.Seminovo':'false'})
                code,_,_=request(app,'/Admin/Veiculo/Upsert',boundary_fields)
                if code not in (302,303):raise Refused('New maximum-length write failed')
                boundary_id=scalar(f"SELECT Id FROM dbo.Veiculo WHERE Modelo=N'{boundary_fields['Veiculo.Modelo']}'")
                report['boundary_vehicle']={'id':boundary_id,'fields':{k:v for k,v in boundary_fields.items() if k.startswith('Veiculo.')}}
                passed('new_binary_writes_expanded_column_limits')
            for table,identity,route in [('Veiculo',vehicle,'/Admin/Veiculo'),('Vendedores',seller,'/Admin/Cadastros/Vendedores'),
                                         ('Marcas',marca,'/Admin/Cadastros/Marcas'),('Lojas',loja,'/Admin/Cadastros/Lojas')]:
                code,html,_=request(app,route);form=Form(html).fields
                form.update({'id':str(identity)})
                code,_,_=request(app,route+'?handler=Delete',form)
                actual={'Veiculo':'Veiculo','Vendedores':'Vendedor','Marcas':'Marca','Lojas':'Loja'}[table]
                if code not in (302,303) or scalar(f'SELECT COUNT(*) FROM dbo.{actual} WHERE Id={identity} AND Ativo=1')!=0:raise Refused('Logical deletion failed '+actual)
                passed(actual.lower()+'_logical_delete')
            # Deleting a vehicle may intentionally remove its media. Prior-phase
            # retained sentinel files are separately checked on binary rollback.
            for sentinel in report.get('retained_media',[]):
                if request(app,sentinel)[0]!=200:raise Refused('Rollback lost a retained synthetic media file')
            passed('prior_binary_media_reference_compatibility')
            result['passed']=True
        except Exception as error:
            result['error_type']=type(error).__name__
            result['reason']=str(error) if isinstance(error,Refused) else 'Sanitized isolated test failure'
        finally:
            assert_owned('container',app);docker('stop','--time','10',app)
            save()

    try:
        memory=int(re.search(r'MemAvailable:\s+(\d+)',Path('/proc/meminfo').read_text())[1])*1024
        if memory<2200*1048576:raise Refused('Insufficient memory reserve for isolated trial')
        last=json.loads((BACKUPS/'state/last-valid.json').read_text())
        native=BACKUPS/'native'/last['run_id']/'database.bak'
        if digest(native)!=last['sha256']:raise Refused('Backup checksum mismatch')
        report['backup_sha256']=last['sha256']
        bundle=ROOT/'incoming/rehearsal-artifact'
        spec=importlib.util.spec_from_file_location('release',ROOT/'bin/release.py');release=importlib.util.module_from_spec(spec);spec.loader.exec_module(release)
        manifest=release.verify_bundle(bundle)
        new_image=release.Docker().check_image(manifest,bundle/'image.tar.gz')
        report['new_image']=new_image;report['legacy_image']=LEGACY
        source=json.loads(docker('inspect','sqlserver'))[0]
        # Read only image identity, never its connection settings or environment.
        sql_image=source['Image']
        docker('network','create','--internal','--label',LABEL+'='+run,prefix);network_created=True
        for suffix in ('sql-data','keys','uploads'):
            volume=prefix+'-'+suffix;docker('volume','create','--label',LABEL+'='+run,volume);volumes.append(volume)
        docker('run','--rm','--network','none','--user','0','--cap-drop','ALL','--cap-add','CHOWN','--cap-add','FOWNER',
               '--mount',f'type=volume,src={prefix}-keys,dst=/keys','--mount',f'type=volume,src={prefix}-uploads,dst=/uploads',
               '--entrypoint','sh',new_image,'-c','chown 1654:1654 /keys /uploads; chmod 700 /keys /uploads')
        containers.append(sql_name)
        docker('run','--detach','--name',sql_name,'--label',LABEL+'='+run,'--network',prefix,'--memory','2g','--memory-swap','2g',
               '--cpus','1','--env','ACCEPT_EULA=Y','--env','MSSQL_PID=Developer','--env','MSSQL_MEMORY_LIMIT_MB=768',
               '--env','MSSQL_SA_PASSWORD='+admin_password,'--log-driver','none',
               '--mount',f'type=volume,src={prefix}-sql-data,dst=/var/opt/mssql',sql_image)
        for _ in range(90):
            if sql('SELECT 1;',database='master',allow_failure=True).returncode==0:break
            time.sleep(2)
        else:raise Refused('Isolated SQL readiness failed')
        docker('exec','--user','0',sql_name,'mkdir','-p','/var/opt/mssql/backup')
        docker('cp',str(native),sql_name+':/var/opt/mssql/backup/database.bak')
        docker('exec','--user','0',sql_name,'sh','-c','chown 10001:0 /var/opt/mssql/backup/database.bak; chmod 600 /var/opt/mssql/backup/database.bak')
        # Logical names are metadata from the authenticated backup, not user input.
        sql("DECLARE @files TABLE(LogicalName nvarchar(128),PhysicalName nvarchar(260),Type char(1),FileGroupName nvarchar(128),Size numeric(20),MaxSize numeric(20),FileId bigint,CreateLSN numeric(25),DropLSN numeric(25),UniqueID uniqueidentifier,ReadOnlyLSN numeric(25),ReadWriteLSN numeric(25),BackupSizeInBytes bigint,SourceBlockSize int,FileGroupId int,LogGroupGUID uniqueidentifier,DifferentialBaseLSN numeric(25),DifferentialBaseGUID uniqueidentifier,IsReadOnly bit,IsPresent bit,TDEThumbprint varbinary(32),SnapshotURL nvarchar(360)); INSERT @files EXEC(N'RESTORE FILELISTONLY FROM DISK=N''/var/opt/mssql/backup/database.bak'''); SELECT LogicalName FROM @files ORDER BY Type;",database='master')
        # Audited SQL logical names; no live database writes.
        files=sql("RESTORE FILELISTONLY FROM DISK=N'/var/opt/mssql/backup/database.bak';",database='master')
        # The previously audited backup has these fixed logical names. Fail if the
        # restore does not recognize them, instead of interpolating arbitrary text.
        sql("RESTORE DATABASE AndersonSchemaTrial FROM DISK=N'/var/opt/mssql/backup/database.bak' WITH MOVE N'AndersonMultiMarcas' TO N'/var/opt/mssql/data/trial.mdf', MOVE N'AndersonMultiMarcas_log' TO N'/var/opt/mssql/data/trial_log.ldf',CHECKSUM,RECOVERY;",database='master')
        report['original_snapshot_sha256']=hashlib.sha256(json.dumps(snapshot(),sort_keys=True).encode()).hexdigest()
        sql("CREATE LOGIN anderson_runtime WITH PASSWORD=N'"+runtime_password+"',CHECK_POLICY=ON; DENY CREATE ANY DATABASE,ALTER ANY LOGIN,ALTER SERVER STATE TO anderson_runtime;",database='master')
        sql(args.runtime_grants.read_text());sql(args.runtime_grants.read_text())
        sql("CREATE TABLE dbo.PermissionProbe(Id int); INSERT dbo.PermissionProbe VALUES(1);")
        if scalar("SELECT IS_SRVROLEMEMBER(N'sysadmin')",runtime=True)!=0:raise Refused('Runtime is administrator')
        for name,query in [('create_table','CREATE TABLE dbo.Forbidden(Id int)'),('alter_table','ALTER TABLE dbo.Marca ADD Forbidden int NULL'),
                           ('drop_table','DROP TABLE dbo.PermissionProbe'),('create_user','CREATE USER Forbidden WITHOUT LOGIN'),
                           ('create_login',"CREATE LOGIN Forbidden WITH PASSWORD=N'"+secrets.token_hex(20)+"!aA9'"),
                           ('grant', 'GRANT SELECT ON dbo.Marca TO public'),('server_role','ALTER SERVER ROLE sysadmin ADD MEMBER anderson_runtime'),
                           ('create_database','CREATE DATABASE Forbidden'),('admin_dbcc','DBCC FREEPROCCACHE')]:
            r=sql(query,runtime=True,allow_failure=True)
            if r.returncode==0:raise Refused('Runtime administrative operation unexpectedly succeeded: '+name)
            report['negative_permissions'].append({'operation':name,'denied':True,'errors':re.findall(r'Msg (\d+)',r.stdout+r.stderr)})
        sql('DROP TABLE dbo.PermissionProbe;')
        # A synthetic Identity user is provisioned only in the disposable copy.
        identity=str(uuid.uuid4()); hashed=password_hash(user_password,secrets.token_bytes(16))
        sql(f"INSERT dbo.AspNetUsers(Id,UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount) VALUES('{identity}',N'{email}',N'{email.upper()}',N'{email}',N'{email.upper()}',1,N'{hashed}',N'{uuid.uuid4()}',N'{uuid.uuid4()}',0,0,1,0); INSERT dbo.AspNetUserRoles(UserId,RoleId) SELECT '{identity}',Id FROM dbo.AspNetRoles;")
        report['runtime_grants_sha256']=digest(args.runtime_grants)
        phase(LEGACY,'old_original')
        before=snapshot();sql(script('EXPAND_STORAGE_PROPOSAL.sql'));after=snapshot()
        sql(script('EXPAND_STORAGE_PROPOSAL.sql'))
        report['widening_idempotent']=snapshot()==after
        report['widening_preserves_counts']=before['counts']==after['counts']
        report['unique_index_before_resolution']=sql(script('MARCA_NOME_UNIQUE_PROPOSAL.sql'),allow_failure=True).returncode!=0
        # This is an optional deterministic label-change proposal, not deduplication.
        sql(script('RENAME_UNREFERENCED_MARCA_PROPOSAL.sql'));sql(script('RENAME_UNREFERENCED_MARCA_PROPOSAL.sql'))
        sql(script('MARCA_NOME_UNIQUE_PROPOSAL.sql'));sql(script('MARCA_NOME_UNIQUE_PROPOSAL.sql'))
        report['optional_rename_and_unique_index']='passed_on_copy_only'
        phase(LEGACY,'old_widened',True)
        phase(new_image,'new_widened',True)
        phase(LEGACY,'old_binary_rollback',True)
        # Undo only optional rename/index. Column widening deliberately remains.
        sql(script('UNDO_MARCA_RENAME_TRIAL.sql'));sql(script('UNDO_MARCA_RENAME_TRIAL.sql'))
        report['rename_undo_idempotent']=True
        sql('DBCC CHECKDB(AndersonSchemaTrial) WITH NO_INFOMSGS,ALL_ERRORMSGS;')
        report['dbcc_checkdb']='passed'
        report['status']='passed' if all(p['passed'] for p in report['phases']) and report['widening_idempotent'] and report['widening_preserves_counts'] else 'review_required'
    except BaseException as error:
        report['status']='failed';report['error_type']=type(error).__name__
        report['reason']=str(error) if isinstance(error,Refused) else 'Sanitized isolated validation failure'
    finally:
        for name in reversed(containers):
            if docker('container','inspect',name,allow_failure=True).returncode==0:
                assert_owned('container',name);docker('rm','--force','--volumes',name)
        for name in reversed(volumes):assert_owned('volume',name);docker('volume','rm',name)
        if network_created:assert_owned('network',prefix);docker('network','rm',prefix)
        report['cleanup']={kind:docker(*argv).split() for kind,argv in
            [('containers',('ps','-aq','--filter','label='+LABEL+'='+run)),
             ('volumes',('volume','ls','-q','--filter','label='+LABEL+'='+run)),
             ('networks',('network','ls','-q','--filter','label='+LABEL+'='+run))]}
        report['completed_at']=datetime.now(timezone.utc).isoformat();save()
        print(json.dumps({'run':run,'status':report['status'],'reason':report.get('reason'),
                          'phases':[{'phase':p['phase'],'passed':p['passed'],'reason':p.get('reason')} for p in report['phases']],
                          'cleanup':report['cleanup']}),flush=True)
    return 0 if report['status']=='passed' and not any(report['cleanup'].values()) else 1


if __name__=='__main__':raise SystemExit(main())
