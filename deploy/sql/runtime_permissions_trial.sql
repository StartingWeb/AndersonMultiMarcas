-- Reviewed proposal: execute ONLY in a disposable SQL instance restored for testing.
-- Login creation/password provisioning is separate; no password in this file.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT IN (N'AndersonSchemaTrial',N'AndersonRuntimeTrial')
 THROW 51100,'Runtime grants are restricted to isolated trial databases.',1;
IF SUSER_ID(N'anderson_runtime') IS NULL OR IS_SRVROLEMEMBER(N'sysadmin',N'anderson_runtime')<>0
 THROW 51101,'A dedicated non-administrative trial login must already exist.',1;
IF EXISTS(SELECT 1 FROM sys.server_role_members WHERE member_principal_id=SUSER_ID(N'anderson_runtime'))
 OR EXISTS(SELECT 1 FROM sys.server_permissions WHERE grantee_principal_id=SUSER_ID(N'anderson_runtime')
 AND state IN ('G','W') AND permission_name<>N'CONNECT SQL')
 THROW 51105,'Unexpected runtime server privileges.',1;
BEGIN TRANSACTION;
IF USER_ID(N'anderson_runtime') IS NULL CREATE USER anderson_runtime FOR LOGIN anderson_runtime;
IF EXISTS(SELECT 1 FROM sys.database_role_members rm JOIN sys.database_principals r ON r.principal_id=rm.role_principal_id
 WHERE rm.member_principal_id IN (USER_ID(N'anderson_runtime'),USER_ID(N'anderson_application')) AND r.name<>N'anderson_application')
 THROW 51102,'Unexpected runtime role membership; refusing to broaden privileges.',1;
IF DATABASE_PRINCIPAL_ID(N'anderson_application') IS NULL CREATE ROLE anderson_application AUTHORIZATION dbo;
IF EXISTS(SELECT 1 FROM sys.database_principals WHERE name=N'anderson_application' AND owning_principal_id<>1)
 OR EXISTS(SELECT 1 FROM sys.database_role_members WHERE role_principal_id=USER_ID(N'anderson_application') AND member_principal_id<>USER_ID(N'anderson_runtime'))
 THROW 51107,'Unexpected application role ownership or membership.',1;
IF EXISTS(SELECT 1 FROM sys.database_principals WHERE principal_id=USER_ID(N'anderson_runtime') AND sid=SUSER_SID(N'sa'))
 THROW 51103,'Administrative runtime identity refused.',1;
ALTER ROLE anderson_application ADD MEMBER anderson_runtime;
GRANT CONNECT TO anderson_runtime;
DECLARE @table sysname,@sql nvarchar(max);
DECLARE @writeTables TABLE(name sysname PRIMARY KEY);
INSERT @writeTables VALUES
 (N'Loja'),(N'Marca'),(N'Veiculo'),(N'VeiculoCaracteristica'),(N'VeiculoMidia'),(N'Vendedor'),
 (N'ImportJob'),(N'ImportJobItem'),(N'ImportJobLog'),(N'ImportJobHistory'),
 (N'AspNetUsers'),(N'AspNetRoles'),(N'AspNetUserRoles'),(N'AspNetRoleClaims');
DECLARE targets CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM @writeTables;
OPEN targets; FETCH NEXT FROM targets INTO @table;
WHILE @@FETCH_STATUS=0
BEGIN
 IF OBJECT_ID(N'dbo.'+QUOTENAME(@table),N'U') IS NULL THROW 51104,'Expected runtime table missing.',1;
 SET @sql=N'GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.'+QUOTENAME(@table)+N' TO anderson_application;';
 EXEC sys.sp_executesql @sql;
 FETCH NEXT FROM targets INTO @table;
END;
CLOSE targets; DEALLOCATE targets;
GRANT SELECT ON OBJECT::dbo.AspNetUserClaims TO anderson_application;
GRANT SELECT ON OBJECT::dbo.AspNetUserLogins TO anderson_application;
GRANT SELECT ON OBJECT::dbo.AspNetUserTokens TO anderson_application;
GRANT SELECT ON OBJECT::dbo.__EFMigrationsHistory TO anderson_application;
IF OBJECT_ID(N'dbo.AspNetMenus',N'U') IS NOT NULL GRANT SELECT ON OBJECT::dbo.AspNetMenus TO anderson_application;
IF OBJECT_ID(N'dbo.AspNetMenuRoles',N'U') IS NOT NULL GRANT SELECT ON OBJECT::dbo.AspNetMenuRoles TO anderson_application;
DENY CREATE TABLE,CREATE VIEW,CREATE PROCEDURE,CREATE FUNCTION,CREATE SCHEMA,
 ALTER ANY USER,ALTER ANY ROLE,ALTER ANY APPLICATION ROLE,ALTER ANY SCHEMA,
 BACKUP DATABASE,BACKUP LOG TO anderson_application;
DENY ALTER ON SCHEMA::dbo TO anderson_application;
IF EXISTS(SELECT 1 FROM sys.database_permissions p
 WHERE p.grantee_principal_id IN (USER_ID(N'anderson_runtime'),USER_ID(N'anderson_application')) AND p.state IN ('G','W')
 AND (p.state='W' OR NOT ((p.class=0 AND p.permission_name=N'CONNECT') OR
 (p.class=1 AND OBJECT_SCHEMA_NAME(p.major_id)=N'dbo' AND
 ((p.permission_name IN (N'SELECT',N'INSERT',N'UPDATE',N'DELETE') AND OBJECT_NAME(p.major_id) IN (SELECT name FROM @writeTables))
 OR (p.permission_name=N'SELECT' AND OBJECT_NAME(p.major_id) IN (N'__EFMigrationsHistory',N'AspNetMenus',N'AspNetMenuRoles',N'AspNetUserClaims',N'AspNetUserLogins',N'AspNetUserTokens')))))))
 THROW 51106,'Unexpected runtime database privileges; transaction rolled back.',1;
COMMIT;
