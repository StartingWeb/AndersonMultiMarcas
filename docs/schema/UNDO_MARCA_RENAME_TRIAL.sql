-- Undo ONLY the optional isolated trial rename/index; NEVER shrink expanded columns.
SET NOCOUNT ON; SET XACT_ABORT ON; SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
IF DB_NAME()<>N'AndersonSchemaTrial' THROW 51090,'Isolated trial only.',1;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.AndersonMarcaRenameAudit',N'U') IS NULL BEGIN COMMIT; RETURN; END;
IF (SELECT COUNT(*) FROM dbo.AndersonMarcaRenameAudit)<>1 OR NOT EXISTS(
 SELECT 1 FROM dbo.AndersonMarcaRenameAudit a JOIN dbo.Marca m WITH(UPDLOCK,HOLDLOCK) ON m.Id=a.MarcaId
 JOIN dbo.Marca canonical ON canonical.Id=1 AND CONVERT(varbinary(200),canonical.Nome)=CONVERT(varbinary(200),a.OriginalName)
 WHERE a.MarcaId=4 AND CONVERT(varbinary(200),m.Nome)=CONVERT(varbinary(200),a.NewName))
 THROW 51037,'Unexpected rename ledger or current state; undo refused.',1;
-- This trial may have added ONLY the exact single-key unique index of the proposal.
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'IX_Marca_Nome' AND is_unique=1)
BEGIN
 IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'IX_Marca_Nome'
 AND (type<>2 OR has_filter=1 OR is_disabled=1 OR is_hypothetical=1 OR ignore_dup_key=1))
 OR (SELECT COUNT(*) FROM sys.index_columns WHERE object_id=OBJECT_ID(N'dbo.Marca') AND index_id=INDEXPROPERTY(OBJECT_ID(N'dbo.Marca'),N'IX_Marca_Nome','IndexID'))<>1
 OR NOT EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
 WHERE ic.object_id=OBJECT_ID(N'dbo.Marca') AND ic.index_id=INDEXPROPERTY(OBJECT_ID(N'dbo.Marca'),N'IX_Marca_Nome','IndexID') AND ic.key_ordinal=1 AND c.name=N'Nome')
 THROW 51038,'Unexpected index shape; undo refused.',1;
 DROP INDEX IX_Marca_Nome ON dbo.Marca;
 CREATE INDEX IX_Marca_Nome ON dbo.Marca(Nome);
END;
UPDATE m SET Nome=a.OriginalName FROM dbo.Marca m JOIN dbo.AndersonMarcaRenameAudit a ON a.MarcaId=m.Id;
DROP TABLE dbo.AndersonMarcaRenameAudit;
COMMIT;
