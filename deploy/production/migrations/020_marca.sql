-- Reviewed production candidate. NOT an execution authorization.
-- Requires pinned target context, explicit operator authorization and script SHA.
SET NOCOUNT ON; SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
SET ANSI_WARNINGS ON; SET ANSI_PADDING ON; SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;
IF DB_NAME()<>N'AndersonMultiMarcas'
 OR ISNULL(CONVERT(nvarchar(128),SESSION_CONTEXT(N'Anderson.ExpectedServer')),N'')<>CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))
 OR ISNULL(CONVERT(nvarchar(36),SESSION_CONTEXT(N'Anderson.ExpectedDatabaseGuid')),N'')<>
 ISNULL((SELECT CONVERT(nvarchar(36),database_guid) FROM sys.database_recovery_status WHERE database_id=DB_ID()),N'missing')
 OR ISNULL(CONVERT(nvarchar(40),SESSION_CONTEXT(N'Anderson.Scope')),N'')<>N'schema-B'
 OR ISNULL(CONVERT(nvarchar(20),SESSION_CONTEXT(N'Anderson.SchemaExecutionApproved')),N'')<>N'approved'
 OR NULLIF(CONVERT(nvarchar(200),SESSION_CONTEXT(N'Anderson.Authorization')),N'') IS NULL
 THROW 52000,'Production migration target/scope/authorization mismatch.',1;
DECLARE @key nvarchar(100)=N'020-rename-marca-4-v1',@digest varchar(64)=CONVERT(varchar(64),SESSION_CONTEXT(N'Anderson.ScriptSha256')),
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
 THROW 52003,'Migration A journal is required; no implicit prerequisites.',1;
DECLARE @candidate nvarchar(4000)=CONVERT(nvarchar(4000),SESSION_CONTEXT(N'Anderson.ApprovedMarcaName')),
 @old nvarchar(4000)=CONVERT(nvarchar(4000),SESSION_CONTEXT(N'Anderson.ExpectedMarcaName')),
 @nameApproval nvarchar(200)=CONVERT(nvarchar(200),SESSION_CONTEXT(N'Anderson.MarcaNameApproval'));
IF NULLIF(@nameApproval,N'') IS NULL OR NULLIF(@candidate,N'') IS NULL OR NULLIF(@old,N'') IS NULL
 OR DATALENGTH(@candidate)>200 OR DATALENGTH(@old)>200
 OR CONVERT(varbinary(max),@candidate)<>CONVERT(varbinary(max),LTRIM(RTRIM(@candidate)))
 OR CHARINDEX(N'[duplicada #4]',@candidate)>0 OR @candidate=@old
 THROW 52020,'Approved commercial label is missing/invalid; no trial suffix is allowed.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.AndersonProductionMigrationJournal WHERE MigrationId=N'010-expand-write-limits-v1')
 THROW 52030,'Migration A is required.',1;
IF EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID(N'dbo.Marca') AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'Nome' AND max_length=200
 AND TYPE_NAME(user_type_id)=N'nvarchar' AND is_computed=0 AND collation_name=N'SQL_Latin1_General_CP1_CI_AS')
 THROW 52021,'Unexpected Marca name contract or enabled trigger.',1;
DECLARE @columns nvarchar(max),@sql nvarchar(max),@beforeOther varbinary(32),@afterOther varbinary(32),@beforeRow varbinary(32),@afterRow varbinary(32);
SELECT @columns=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),N',') WITHIN GROUP(ORDER BY column_id)
 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name<>N'Nome';
SET @sql=N'SELECT @h=HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),(SELECT '+@columns+N' FROM dbo.Marca WITH(UPDLOCK,HOLDLOCK) WHERE Id=4 FOR JSON PATH,INCLUDE_NULL_VALUES)));';
EXEC sys.sp_executesql @sql,N'@h varbinary(32) OUTPUT',@h=@beforeOther OUTPUT;
SET @beforeRow=HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM dbo.Marca WITH(UPDLOCK,HOLDLOCK) WHERE Id=4 FOR JSON PATH,INCLUDE_NULL_VALUES)));
IF EXISTS(SELECT 1 FROM dbo.AndersonProductionMigrationJournal WHERE MigrationId=@key)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.AndersonProductionMigrationJournal WHERE MigrationId=@key AND ScriptSha256=@digest
 AND AfterFingerprint=@beforeRow AND CONVERT(varbinary(200),JSON_VALUE(DetailsJson,'$.new_name'))=CONVERT(varbinary(200),@candidate))
 THROW 52024,'Commercial rename replay differs from the recorded result.',1;
 COMMIT; RETURN;
END;
IF (SELECT COUNT(*) FROM dbo.Marca WITH(UPDLOCK,HOLDLOCK) WHERE Id IN(1,4) AND Ativo=1 AND CONVERT(varbinary(200),Nome)=CONVERT(varbinary(200),@old))<>2
 OR (SELECT COUNT(*) FROM dbo.Veiculo WITH(UPDLOCK,HOLDLOCK) WHERE MarcaId=1)<>1
 OR EXISTS(SELECT 1 FROM dbo.Veiculo WITH(UPDLOCK,HOLDLOCK) WHERE MarcaId=4)
 OR (SELECT COUNT(*) FROM sys.foreign_keys WHERE referenced_object_id=OBJECT_ID(N'dbo.Marca'))<>1
 OR NOT EXISTS(SELECT 1 FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
 WHERE f.name=N'FK_Veiculo_Marca_MarcaId' AND f.parent_object_id=OBJECT_ID(N'dbo.Veiculo')
 AND f.referenced_object_id=OBJECT_ID(N'dbo.Marca') AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'MarcaId'
 AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'Id')
 THROW 52021,'Marca IDs/status/names/references changed; refresh functional approval.',1;
IF EXISTS(SELECT 1 FROM dbo.Marca WITH(UPDLOCK,HOLDLOCK) WHERE Id<>4 AND Nome=@candidate)
 THROW 52022,'Approved label collides under the actual column collation.',1;
DECLARE @others varbinary(32)=HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM dbo.Marca WITH(HOLDLOCK) WHERE Id<>4 ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES))),
 @vehicles varbinary(32)=HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM dbo.Veiculo WITH(HOLDLOCK) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)));
UPDATE dbo.Marca SET Nome=CONVERT(nvarchar(100),@candidate) WHERE Id=4 AND CONVERT(varbinary(200),Nome)=CONVERT(varbinary(200),@old);
IF @@ROWCOUNT<>1 THROW 52023,'Rename cardinality mismatch.',1;
EXEC sys.sp_executesql @sql,N'@h varbinary(32) OUTPUT',@h=@afterOther OUTPUT;
IF @beforeOther<>@afterOther
 OR @others<>HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM dbo.Marca WHERE Id<>4 ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)))
 OR @vehicles<>HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM dbo.Veiculo ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)))
 THROW 52023,'Other fields, brands or vehicles changed; rename rolled back.',1;
SET @afterRow=HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM dbo.Marca WHERE Id=4 FOR JSON PATH,INCLUDE_NULL_VALUES)));
INSERT dbo.AndersonProductionMigrationJournal VALUES(@key,@digest,SYSUTCDATETIME(),SUSER_SNAME(),@authorization,@beforeRow,@afterRow,
 (SELECT @old AS original_name,@candidate AS new_name,@nameApproval AS name_approval,N'Preserve both IDs; no deleted rows or changed relationships.' AS impact FOR JSON PATH,WITHOUT_ARRAY_WRAPPER));
COMMIT;
