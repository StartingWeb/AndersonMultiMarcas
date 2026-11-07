-- Read-only aggregates and necessary technical IDs. No names or customer rows.
SET NOCOUNT ON;
IF DB_NAME() NOT IN (N'AndersonMultiMarcas',N'AndersonSchemaTrial')
    THROW 51090, 'Unexpected database for read-only audit.', 1;
SELECT CAST(DATABASEPROPERTYEX(DB_NAME(),'Collation') AS nvarchar(128)) AS database_collation,
 (SELECT collation_name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Marca') AND name='Nome') AS name_collation,
 (SELECT COUNT(*) FROM (SELECT Nome FROM dbo.Marca GROUP BY Nome HAVING COUNT(*)>1) g) AS duplicate_groups,
 (SELECT COUNT(*) FROM (SELECT Nome COLLATE Latin1_General_100_CI_AI AS n FROM dbo.Marca GROUP BY Nome COLLATE Latin1_General_100_CI_AI HAVING COUNT(*)>1) g) AS ci_ai_groups,
 (SELECT COUNT(*) FROM (SELECT CONVERT(varbinary(200),Nome) AS n FROM dbo.Marca GROUP BY CONVERT(varbinary(200),Nome) HAVING COUNT(*)>1) g) AS exact_byte_groups,
 JSON_QUERY((SELECT m.Id,m.Ativo,DATALENGTH(m.Nome)/2 AS characters,
   DATALENGTH(m.Nome)/2-LEN(m.Nome) AS trailing_spaces, LEN(m.Nome)-LEN(LTRIM(m.Nome)) AS leading_spaces,
   (SELECT COUNT(*) FROM dbo.Veiculo v WHERE v.MarcaId=m.Id) AS vehicle_references,
   (SELECT COUNT(*) FROM dbo.Marca z WHERE z.Nome=m.Nome) AS collation_conflict_members,
   (SELECT COUNT(DISTINCT CONVERT(varbinary(200),z.Nome)) FROM dbo.Marca z WHERE z.Nome=m.Nome) AS byte_variants
  FROM dbo.Marca m WHERE EXISTS(SELECT 1 FROM dbo.Marca z WHERE z.Id<>m.Id AND z.Nome=m.Nome) ORDER BY m.Id FOR JSON PATH)) AS conflicts,
 JSON_QUERY((SELECT f.name AS fk,OBJECT_NAME(f.parent_object_id) AS referring_table,c.name AS referring_column,f.delete_referential_action_desc AS on_delete
 FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
 JOIN sys.columns c ON c.object_id=fc.parent_object_id AND c.column_id=fc.parent_column_id
 WHERE f.referenced_object_id=OBJECT_ID('dbo.Marca') FOR JSON PATH)) AS foreign_keys
 FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
