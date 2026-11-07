-- Reviewed production candidate. NOT an execution authorization.
-- Requires pinned target context, explicit operator authorization and script SHA.
SET NOCOUNT ON; SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
SET ANSI_WARNINGS ON; SET ANSI_PADDING ON; SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;
IF DB_NAME()<>N'AndersonMultiMarcas'
 OR ISNULL(CONVERT(nvarchar(128),SESSION_CONTEXT(N'Anderson.ExpectedServer')),N'')<>CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))
 OR ISNULL(CONVERT(nvarchar(36),SESSION_CONTEXT(N'Anderson.ExpectedDatabaseGuid')),N'')<>
 ISNULL((SELECT CONVERT(nvarchar(36),database_guid) FROM sys.database_recovery_status WHERE database_id=DB_ID()),N'missing')
 OR ISNULL(CONVERT(nvarchar(40),SESSION_CONTEXT(N'Anderson.Scope')),N'')<>N'schema-A'
 OR ISNULL(CONVERT(nvarchar(20),SESSION_CONTEXT(N'Anderson.SchemaExecutionApproved')),N'')<>N'approved'
 OR NULLIF(CONVERT(nvarchar(200),SESSION_CONTEXT(N'Anderson.Authorization')),N'') IS NULL
 THROW 52000,'Production migration target/scope/authorization mismatch.',1;
DECLARE @key nvarchar(100)=N'010-expand-write-limits-v1',@digest varchar(64)=CONVERT(varchar(64),SESSION_CONTEXT(N'Anderson.ScriptSha256')),
 @authorization nvarchar(200)=CONVERT(nvarchar(200),SESSION_CONTEXT(N'Anderson.Authorization'));
IF @digest IS NULL OR LEN(@digest)<>64 OR @digest LIKE '%[^0-9a-f]%'
 THROW 52002,'Reviewed script checksum is required.',1;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=N'Anderson-Production-Migrations',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=0;
IF @lock<0 THROW 52001,'Another migration is active.',1;
IF EXISTS(SELECT 1 FROM sys.triggers WHERE parent_class=0 AND is_disabled=0)
 THROW 52004,'Enabled database DDL triggers require separate review.',1;
IF OBJECT_ID(N'dbo.AndersonProductionMigrationJournal',N'U') IS NULL
 CREATE TABLE dbo.AndersonProductionMigrationJournal(MigrationId nvarchar(100) NOT NULL PRIMARY KEY,
 ScriptSha256 char(64) NOT NULL,ExecutedAt datetime2(7) NOT NULL,OperatorLogin sysname NOT NULL,
 AuthorizationRef nvarchar(200) NOT NULL,BeforeFingerprint varbinary(32) NOT NULL,
 AfterFingerprint varbinary(32) NOT NULL,DetailsJson nvarchar(max) NOT NULL);
DECLARE @logoNullable bit=CASE WHEN CONVERT(nvarchar(20),SESSION_CONTEXT(N'Anderson.AllowLogoNullability'))=N'approved' THEN 1 ELSE 0 END;
IF @logoNullable=1 AND NULLIF(CONVERT(nvarchar(200),SESSION_CONTEXT(N'Anderson.LogoNullabilityAuthorization')),N'') IS NULL
 THROW 52010,'LogoUrl nullability change needs its own explicit authorization.',1;
DECLARE @targets TABLE(tbl sysname,col sysname,old_size int,new_size int,old_null bit);
INSERT @targets VALUES(N'Loja',N'Email',150,180,1),(N'Marca',N'LogoUrl',255,400,0),
(N'Veiculo',N'Cor',30,60,1),(N'Veiculo',N'Descricao',1000,4000,1),(N'Veiculo',N'Modelo',100,150,1),
(N'Veiculo',N'ObservacoesInternas',1000,2000,1),(N'Veiculo',N'Titulo',150,180,0),(N'Veiculo',N'UrlVideo',255,400,1),
(N'Veiculo',N'Versao',100,150,1),(N'Vendedor',N'Cargo',100,120,1),(N'Vendedor',N'Email',150,180,1),(N'Vendedor',N'FotoUrl',255,400,1);
IF EXISTS(SELECT 1 FROM @targets t LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(t.tbl)) AND c.name=t.col
 WHERE c.column_id IS NULL OR TYPE_NAME(c.user_type_id)<>N'nvarchar' OR c.is_computed=1
 OR c.max_length NOT IN (t.old_size*2,t.new_size*2)
 OR (c.is_nullable<>t.old_null AND NOT(t.tbl=N'Marca' AND t.col=N'LogoUrl' AND @logoNullable=1 AND c.is_nullable=1)))
 THROW 52010,'Unexpected write-column state; nothing widened.',1;
DECLARE @schemaFingerprint varbinary(32)=HASHBYTES('SHA2_256',CONVERT(varbinary(max),(
 SELECT t.name AS tbl,c.name,TYPE_NAME(c.user_type_id) AS typ,c.max_length,c.is_nullable,c.is_computed
 FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id WHERE t.name IN (N'Loja',N'Marca',N'Veiculo',N'Vendedor')
 ORDER BY t.name,c.column_id FOR JSON PATH,INCLUDE_NULL_VALUES)));
IF EXISTS(SELECT 1 FROM dbo.AndersonProductionMigrationJournal WHERE MigrationId=@key)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.AndersonProductionMigrationJournal WHERE MigrationId=@key AND ScriptSha256=@digest
 AND AfterFingerprint=@schemaFingerprint AND JSON_VALUE(DetailsJson,'$.logo_nullable')=CASE WHEN @logoNullable=1 THEN N'true' ELSE N'false' END)
 THROW 52011,'Schema replay differs from the recorded result or approved policy.',1;
 COMMIT; RETURN;
END;
DECLARE @beforeSchema varbinary(32)=@schemaFingerprint;
DECLARE @beforeFk varbinary(32)=HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM sys.foreign_keys ORDER BY object_id FOR JSON PATH,INCLUDE_NULL_VALUES)));
DECLARE @beforeData TABLE(tbl sysname PRIMARY KEY,h varbinary(32));
DECLARE @tablebeforeData sysname,@sqlbeforeData nvarchar(max),@hbeforeData varbinary(32);
DECLARE databeforeData CURSOR LOCAL FAST_FORWARD FOR SELECT t FROM (VALUES(N'Loja'),(N'Marca'),(N'Veiculo'),(N'Vendedor')) x(t);
OPEN databeforeData;FETCH NEXT FROM databeforeData INTO @tablebeforeData;
WHILE @@FETCH_STATUS=0 BEGIN
 SET @sqlbeforeData=N'SELECT @h=HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),(SELECT * FROM dbo.'+QUOTENAME(@tablebeforeData)+N' WITH(HOLDLOCK) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)));';
 EXEC sys.sp_executesql @sqlbeforeData,N'@h varbinary(32) OUTPUT',@h=@hbeforeData OUTPUT;
 INSERT @beforeData VALUES(@tablebeforeData,@hbeforeData);
 FETCH NEXT FROM databeforeData INTO @tablebeforeData;
END;CLOSE databeforeData;DEALLOCATE databeforeData;
DECLARE @tbl sysname,@col sysname,@size int,@nullable bit,@ddl nvarchar(max);
DECLARE changes CURSOR LOCAL FAST_FORWARD FOR SELECT tbl,col,new_size,CASE WHEN tbl=N'Marca' AND col=N'LogoUrl' AND @logoNullable=1 THEN 1 ELSE old_null END FROM @targets;
OPEN changes;FETCH NEXT FROM changes INTO @tbl,@col,@size,@nullable;
WHILE @@FETCH_STATUS=0 BEGIN
 SET @ddl=N'ALTER TABLE dbo.'+QUOTENAME(@tbl)+N' ALTER COLUMN '+QUOTENAME(@col)+N' nvarchar('+CONVERT(nvarchar(10),@size)+N') '+CASE WHEN @nullable=1 THEN N'NULL' ELSE N'NOT NULL' END+N';';
 EXEC sys.sp_executesql @ddl;
 FETCH NEXT FROM changes INTO @tbl,@col,@size,@nullable;
END;CLOSE changes;DEALLOCATE changes;
DECLARE @afterData TABLE(tbl sysname PRIMARY KEY,h varbinary(32));
DECLARE @tableafterData sysname,@sqlafterData nvarchar(max),@hafterData varbinary(32);
DECLARE dataafterData CURSOR LOCAL FAST_FORWARD FOR SELECT t FROM (VALUES(N'Loja'),(N'Marca'),(N'Veiculo'),(N'Vendedor')) x(t);
OPEN dataafterData;FETCH NEXT FROM dataafterData INTO @tableafterData;
WHILE @@FETCH_STATUS=0 BEGIN
 SET @sqlafterData=N'SELECT @h=HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),(SELECT * FROM dbo.'+QUOTENAME(@tableafterData)+N' WITH(HOLDLOCK) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)));';
 EXEC sys.sp_executesql @sqlafterData,N'@h varbinary(32) OUTPUT',@h=@hafterData OUTPUT;
 INSERT @afterData VALUES(@tableafterData,@hafterData);
 FETCH NEXT FROM dataafterData INTO @tableafterData;
END;CLOSE dataafterData;DEALLOCATE dataafterData;
IF EXISTS(SELECT * FROM @beforeData EXCEPT SELECT * FROM @afterData)
 OR @beforeFk<>HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM sys.foreign_keys ORDER BY object_id FOR JSON PATH,INCLUDE_NULL_VALUES)))
 THROW 52012,'Data or foreign-key fingerprint changed; transaction refused.',1;
SET @schemaFingerprint=HASHBYTES('SHA2_256',CONVERT(varbinary(max),(
 SELECT t.name AS tbl,c.name,TYPE_NAME(c.user_type_id) AS typ,c.max_length,c.is_nullable,c.is_computed
 FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id WHERE t.name IN(N'Loja',N'Marca',N'Veiculo',N'Vendedor')
 ORDER BY t.name,c.column_id FOR JSON PATH,INCLUDE_NULL_VALUES)));
INSERT dbo.AndersonProductionMigrationJournal VALUES(@key,@digest,SYSUTCDATETIME(),SUSER_SNAME(),@authorization,@beforeSchema,@schemaFingerprint,
 (SELECT @logoNullable AS logo_nullable,N'No commercial data changed; binary rollback keeps widened columns.' AS rollback_policy FOR JSON PATH,WITHOUT_ARRAY_WRAPPER));
COMMIT;
