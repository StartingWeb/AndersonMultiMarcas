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
IF EXISTS (SELECT Nome FROM dbo.Marca WITH (UPDLOCK,HOLDLOCK) GROUP BY Nome HAVING COUNT_BIG(*)>1)
    THROW 51021, 'Nomes duplicados existentes; indice preservado; decisao de dados necessaria.', 1;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'IX_Marca_Nome' AND is_unique=0)
    DROP INDEX [IX_Marca_Nome] ON [dbo].[Marca];
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'IX_Marca_Nome' AND is_unique=1)
    CREATE UNIQUE INDEX [IX_Marca_Nome] ON [dbo].[Marca]([Nome]);
COMMIT;
GO
