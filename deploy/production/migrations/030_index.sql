-- Reviewed production candidate. NOT an execution authorization.
-- Requires pinned target context, explicit operator authorization and script SHA.
SET NOCOUNT ON; SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;
SET ANSI_WARNINGS ON; SET ANSI_PADDING ON; SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;
IF DB_NAME()<>N'AndersonMultiMarcas'
 OR ISNULL(CONVERT(nvarchar(128),SESSION_CONTEXT(N'Anderson.ExpectedServer')),N'')<>CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))
 OR ISNULL(CONVERT(nvarchar(36),SESSION_CONTEXT(N'Anderson.ExpectedDatabaseGuid')),N'')<>
 ISNULL((SELECT CONVERT(nvarchar(36),database_guid) FROM sys.database_recovery_status WHERE database_id=DB_ID()),N'missing')
 OR ISNULL(CONVERT(nvarchar(40),SESSION_CONTEXT(N'Anderson.Scope')),N'')<>N'schema-C'
 OR ISNULL(CONVERT(nvarchar(20),SESSION_CONTEXT(N'Anderson.SchemaExecutionApproved')),N'')<>N'approved'
 OR NULLIF(CONVERT(nvarchar(200),SESSION_CONTEXT(N'Anderson.Authorization')),N'') IS NULL
 THROW 52000,'Production migration target/scope/authorization mismatch.',1;
DECLARE @key nvarchar(100)=N'030-unique-marca-name-v1',@digest varchar(64)=CONVERT(varchar(64),SESSION_CONTEXT(N'Anderson.ScriptSha256')),
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
IF NOT EXISTS(SELECT 1 FROM dbo.AndersonProductionMigrationJournal WHERE MigrationId=N'010-expand-write-limits-v1')
 OR NOT EXISTS(SELECT 1 FROM dbo.AndersonProductionMigrationJournal WHERE MigrationId=N'020-rename-marca-4-v1')
 THROW 52030,'Migrations A and approved B are required.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.Marca m JOIN dbo.AndersonProductionMigrationJournal j ON j.MigrationId=N'020-rename-marca-4-v1'
 WHERE m.Id=4 AND CONVERT(varbinary(200),m.Nome)=CONVERT(varbinary(200),JSON_VALUE(j.DetailsJson,'$.new_name')))
 THROW 52030,'Approved brand label changed after migration B.',1;
IF EXISTS(SELECT Nome FROM dbo.Marca WITH(UPDLOCK,HOLDLOCK) GROUP BY Nome HAVING COUNT(*)>1)
 THROW 52032,'Duplicate names remain; no index removed.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.Marca') AND i.name=N'IX_Marca_Nome'
 AND i.type=2 AND i.has_filter=0 AND i.is_disabled=0 AND i.is_hypothetical=0 AND i.ignore_dup_key=0
 AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id)=1
 AND EXISTS(SELECT 1 FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id
 AND ic.key_ordinal=1 AND ic.is_descending_key=0 AND COL_NAME(ic.object_id,ic.column_id)=N'Nome'))
 THROW 52031,'Existing brand-name index is unexpected; nothing removed.',1;
DECLARE @beforeIndex varbinary(32)=HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT name,type,is_unique,has_filter,is_disabled,ignore_dup_key
 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'IX_Marca_Nome' FOR JSON PATH))),@afterIndex varbinary(32);
IF EXISTS(SELECT 1 FROM dbo.AndersonProductionMigrationJournal WHERE MigrationId=@key)
BEGIN
 IF NOT EXISTS(SELECT 1 FROM dbo.AndersonProductionMigrationJournal WHERE MigrationId=@key AND ScriptSha256=@digest AND AfterFingerprint=@beforeIndex)
 THROW 52033,'Index replay differs from the recorded result.',1;
 COMMIT; RETURN;
END;
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'IX_Marca_Nome' AND is_unique=1)
 THROW 52031,'An unrecorded unique index requires separate review.',1;
DECLARE @data varbinary(32)=HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM dbo.Marca WITH(HOLDLOCK) ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)));
DROP INDEX IX_Marca_Nome ON dbo.Marca;
CREATE UNIQUE INDEX IX_Marca_Nome ON dbo.Marca(Nome);
IF @data<>HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT * FROM dbo.Marca ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)))
 THROW 52034,'Brand data changed during index creation.',1;
SET @afterIndex=HASHBYTES('SHA2_256',CONVERT(varbinary(max),(SELECT name,type,is_unique,has_filter,is_disabled,ignore_dup_key
 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'IX_Marca_Nome' FOR JSON PATH)));
INSERT dbo.AndersonProductionMigrationJournal VALUES(@key,@digest,SYSUTCDATETIME(),SUSER_SNAME(),@authorization,@beforeIndex,@afterIndex,N'{"commercial_data_changed":false}');
COMMIT;
