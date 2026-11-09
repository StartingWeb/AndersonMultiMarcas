-- Read-only structural evidence and aggregate counts. No customer values.
SET NOCOUNT ON;
DECLARE @counts nvarchar(max), @countJson nvarchar(max);
SELECT @counts=STRING_AGG(CONVERT(nvarchar(max),
  N'SELECT N''' + REPLACE(SCHEMA_NAME(schema_id)+N'.'+name,'''','''''') +
  N''' AS [table], COUNT_BIG(*) AS [count] FROM ' + QUOTENAME(SCHEMA_NAME(schema_id))+N'.'+QUOTENAME(name)),
  N' UNION ALL ') WITHIN GROUP (ORDER BY name)
FROM sys.tables WHERE is_ms_shipped=0;
SET @counts=N'SELECT @output=(SELECT * FROM ('+@counts+N') c ORDER BY [table] FOR JSON PATH);';
EXEC sys.sp_executesql @counts,N'@output nvarchar(max) OUTPUT',@output=@countJson OUTPUT;
SELECT
 JSON_QUERY((SELECT SCHEMA_NAME(t.schema_id) AS [schema],t.name AS [table],c.name,
  ty.name AS [type],c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,
  c.is_computed,dc.definition AS [default],cc.definition AS computed_definition
  FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
  JOIN sys.types ty ON ty.user_type_id=c.user_type_id
  LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id
  LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id
  WHERE t.is_ms_shipped=0 ORDER BY t.name,c.column_id FOR JSON PATH)) AS columns,
 JSON_QUERY((SELECT t.name AS [table],i.name,i.type_desc,i.is_unique,i.is_primary_key,
  i.is_unique_constraint,i.has_filter,i.filter_definition,c.name AS [column],
  ic.key_ordinal,ic.is_descending_key,ic.is_included_column
  FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id
  JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
  JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
  WHERE t.is_ms_shipped=0 ORDER BY t.name,i.name,ic.index_column_id FOR JSON PATH)) AS indexes,
 JSON_QUERY((SELECT f.name,OBJECT_NAME(f.parent_object_id) AS [table],
  pc.name AS [column],OBJECT_NAME(f.referenced_object_id) AS referenced_table,
  rc.name AS referenced_column,f.delete_referential_action_desc,
  f.update_referential_action_desc,f.is_disabled,f.is_not_trusted
  FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
  JOIN sys.columns pc ON pc.object_id=fc.parent_object_id AND pc.column_id=fc.parent_column_id
  JOIN sys.columns rc ON rc.object_id=fc.referenced_object_id AND rc.column_id=fc.referenced_column_id
  ORDER BY f.name,fc.constraint_column_id FOR JSON PATH)) AS foreign_keys,
 JSON_QUERY((SELECT OBJECT_NAME(parent_object_id) AS [table],name,definition,is_disabled,is_not_trusted
  FROM sys.check_constraints ORDER BY name FOR JSON PATH)) AS checks,
 JSON_QUERY((SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId FOR JSON PATH)) AS migrations,
 JSON_QUERY(@countJson) AS counts
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
