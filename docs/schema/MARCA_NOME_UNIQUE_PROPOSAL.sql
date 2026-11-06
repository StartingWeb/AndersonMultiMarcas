-- PROPOSTA SEPARADA: nao mescla, exclui ou renomeia marcas.
-- A copia real possui duplicidade: a recusa 51021 e o resultado esperado.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO
IF DB_NAME() <> N'AndersonSchemaTrial'
    THROW 51090, 'Proposta restrita a AndersonSchemaTrial em instancia isolada; producao nao autorizada.', 1;
GO
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.Marca') AND i.name=N'IX_Marca_Nome'
 AND (i.type<>2 OR i.has_filter=1 OR i.is_disabled=1 OR i.is_hypothetical=1 OR i.ignore_dup_key=1
 OR (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id)<>1
 OR NOT EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
 WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'Nome')))
 THROW 51022,'Unexpected existing index shape; nothing removed.',1;
IF EXISTS (SELECT Nome FROM dbo.Marca WITH (UPDLOCK,HOLDLOCK) GROUP BY Nome HAVING COUNT_BIG(*)>1)
    THROW 51021, 'Nomes duplicados existentes; indice preservado; decisao de dados necessaria.', 1;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'IX_Marca_Nome' AND is_unique=0)
    DROP INDEX [IX_Marca_Nome] ON [dbo].[Marca];
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'IX_Marca_Nome' AND is_unique=1)
    CREATE UNIQUE INDEX [IX_Marca_Nome] ON [dbo].[Marca]([Nome]);
COMMIT;
GO
