-- OPTIONAL BUSINESS PROPOSAL, NOT PRODUCTION AUTHORIZATION.
-- Preserve IDs, names in a private ledger, vehicle references, and all records.
-- A label change needs human acceptance. Never deduplicate automatically.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
IF DB_NAME()<>N'AndersonSchemaTrial' THROW 51090,'Isolated trial only.',1;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.AndersonMarcaRenameAudit',N'U') IS NOT NULL
BEGIN
 IF (SELECT COUNT(*) FROM dbo.AndersonMarcaRenameAudit)<>1 OR NOT EXISTS(
  SELECT 1 FROM dbo.AndersonMarcaRenameAudit a JOIN dbo.Marca m ON m.Id=a.MarcaId
  JOIN dbo.Marca canonical ON canonical.Id=1 AND CONVERT(varbinary(200),canonical.Nome)=CONVERT(varbinary(200),a.OriginalName)
  WHERE a.MarcaId=4 AND CONVERT(varbinary(200),m.Nome)=CONVERT(varbinary(200),a.NewName))
  OR EXISTS(SELECT 1 FROM dbo.Marca GROUP BY Nome HAVING COUNT(*)>1)
  THROW 51031,'Rename replay encountered an unexpected state.',1;
 COMMIT; RETURN;
END;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'Nome' AND max_length=200 AND TYPE_NAME(user_type_id)=N'nvarchar')
 THROW 51032,'Unexpected name column type/capacity.',1;
IF (SELECT collation_name FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'Nome')<>N'SQL_Latin1_General_CP1_CI_AS'
 THROW 51032,'Unexpected collation.',1;
IF (SELECT COUNT(*) FROM (SELECT Nome FROM dbo.Marca WITH (UPDLOCK,HOLDLOCK) GROUP BY Nome HAVING COUNT(*)>1) g)<>1
 OR (SELECT COUNT(*) FROM dbo.Marca WITH (UPDLOCK,HOLDLOCK) WHERE Id IN (1,4) AND Ativo=1)<>2
 OR NOT EXISTS(SELECT 1 FROM dbo.Marca a JOIN dbo.Marca b ON a.Id=1 AND b.Id=4
 WHERE CONVERT(varbinary(200),a.Nome)=CONVERT(varbinary(200),b.Nome))
 OR (SELECT COUNT(*) FROM dbo.Marca WHERE Nome=(SELECT Nome FROM dbo.Marca WHERE Id=1))<>2
 OR EXISTS(SELECT 1 FROM dbo.Veiculo WITH (UPDLOCK,HOLDLOCK) WHERE MarcaId=4)
 OR EXISTS(SELECT 1 FROM sys.foreign_keys WHERE referenced_object_id=OBJECT_ID(N'dbo.Marca')
 AND NOT(name=N'FK_Veiculo_Marca_MarcaId' AND parent_object_id=OBJECT_ID(N'dbo.Veiculo')))
 THROW 51033,'Unexpected duplicates or references; no rename allowed.',1;
DECLARE @old nvarchar(100)=(SELECT Nome FROM dbo.Marca WHERE Id=4),@new nvarchar(100);
IF DATALENGTH(@old)/2+DATALENGTH(N' [duplicada #4]')/2>100 THROW 51034,'Rename would truncate the original name.',1;
SET @new=@old+N' [duplicada #4]';
IF EXISTS(SELECT 1 FROM dbo.Marca WHERE Nome=@new) THROW 51035,'Proposed label already exists.',1;
CREATE TABLE dbo.AndersonMarcaRenameAudit(MarcaId int NOT NULL PRIMARY KEY,OriginalName nvarchar(100) NOT NULL,NewName nvarchar(100) NOT NULL);
INSERT dbo.AndersonMarcaRenameAudit VALUES(4,@old,@new);
UPDATE dbo.Marca SET Nome=@new WHERE Id=4 AND CONVERT(varbinary(200),Nome)=CONVERT(varbinary(200),@old);
IF @@ROWCOUNT<>1 THROW 51036,'Rename cardinality mismatch.',1;
COMMIT;
