-- Read-only column metadata and aggregates. No business values or credentials.
SET NOCOUNT ON;
IF DB_NAME() NOT IN (N'AndersonMultiMarcas',N'AndersonSchemaTrial') THROW 51090,'Unexpected audit database.',1;
DECLARE @targets TABLE(tbl sysname,col sysname,capacity int);
INSERT @targets VALUES(N'Loja',N'Email',180),(N'Marca',N'LogoUrl',400),(N'Veiculo',N'Cor',60),
(N'Veiculo',N'Descricao',4000),(N'Veiculo',N'Modelo',150),(N'Veiculo',N'ObservacoesInternas',2000),
(N'Veiculo',N'Titulo',180),(N'Veiculo',N'UrlVideo',400),(N'Veiculo',N'Versao',150),
(N'Vendedor',N'Cargo',120),(N'Vendedor',N'Email',180),(N'Vendedor',N'FotoUrl',400);
DECLARE @stats TABLE(tbl sysname,col sysname,rows bigint,nulls bigint,max_characters int,above_model_capacity bigint);
DECLARE @tbl sysname,@col sysname,@capacity int,@sql nvarchar(max);
DECLARE targets CURSOR LOCAL FAST_FORWARD FOR SELECT tbl,col,capacity FROM @targets;
OPEN targets;FETCH NEXT FROM targets INTO @tbl,@col,@capacity;
WHILE @@FETCH_STATUS=0
BEGIN
 IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.'+QUOTENAME(@tbl)) AND name=@col)
 THROW 51040,'Expected audit column missing.',1;
 SET @sql=N'SELECT @t,@c,COUNT_BIG(*),COALESCE(SUM(CONVERT(bigint,CASE WHEN '+QUOTENAME(@col)+N' IS NULL THEN 1 ELSE 0 END)),0),MAX(COALESCE(DATALENGTH('+QUOTENAME(@col)+N')/2,0)),COALESCE(SUM(CONVERT(bigint,CASE WHEN DATALENGTH('+QUOTENAME(@col)+N')>@cap*2 THEN 1 ELSE 0 END)),0) FROM dbo.'+QUOTENAME(@tbl)+N';';
 INSERT @stats EXEC sys.sp_executesql @sql,N'@t sysname,@c sysname,@cap int',@t=@tbl,@c=@col,@cap=@capacity;
 FETCH NEXT FROM targets INTO @tbl,@col,@capacity;
END;
CLOSE targets;DEALLOCATE targets;
SELECT t.tbl AS [table],t.col AS [column],TYPE_NAME(c.user_type_id) AS [type],c.max_length/2 AS current_capacity,
 t.capacity AS model_capacity,c.is_nullable,s.rows,s.nulls,s.max_characters,s.above_model_capacity
FROM @targets t JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(t.tbl)) AND c.name=t.col
JOIN @stats s ON s.tbl=t.tbl AND s.col=t.col ORDER BY t.tbl,t.col FOR JSON PATH;
