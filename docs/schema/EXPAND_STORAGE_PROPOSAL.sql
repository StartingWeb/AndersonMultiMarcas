-- PROPOSTA PARA REVISAO. Somente a segunda copia em instancia SQL isolada.
-- Nao modifica dados, reduz comprimentos ou endurece nulabilidade existente.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_WARNINGS ON;
SET ANSI_PADDING ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF DB_NAME() <> N'AndersonSchemaTrial'
    THROW 51090, 'Proposta restrita a AndersonSchemaTrial em instancia isolada; producao nao autorizada.', 1;
GO
BEGIN TRANSACTION;
DECLARE @targets TABLE ([table] sysname, [column] sysname, characters int, allow_null bit);
INSERT @targets VALUES
 (N'Loja',N'Email',180,0),
 (N'Marca',N'LogoUrl',400,1),
 (N'Veiculo',N'Cor',60,0),
 (N'Veiculo',N'Descricao',4000,0),
 (N'Veiculo',N'Modelo',150,0),
 (N'Veiculo',N'ObservacoesInternas',2000,0),
 (N'Veiculo',N'Titulo',180,0),
 (N'Veiculo',N'UrlVideo',400,0),
 (N'Veiculo',N'Versao',150,0),
 (N'Vendedor',N'Cargo',120,0),
 (N'Vendedor',N'Email',180,0),
 (N'Vendedor',N'FotoUrl',400,0);
DECLARE @table sysname,@column sysname,@characters int,@allowNull bit,
        @currentLength int,@currentNullable bit,@type sysname,@computed bit,@statement nvarchar(max);
DECLARE changes CURSOR LOCAL FAST_FORWARD FOR SELECT [table],[column],characters,allow_null FROM @targets;
OPEN changes;
FETCH NEXT FROM changes INTO @table,@column,@characters,@allowNull;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @currentLength=NULL;
    SELECT @currentLength=c.max_length,@currentNullable=c.is_nullable,
           @type=TYPE_NAME(c.user_type_id),@computed=c.is_computed
    FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(@table)) AND c.name=@column;
    IF @currentLength IS NULL OR @type<>N'nvarchar' OR @computed<>0
        THROW 51091, 'Coluna esperada ausente ou tipo divergente; nenhuma conversao automatica permitida.', 1;
    IF (@currentLength<>-1 AND @currentLength<@characters*2) OR (@allowNull=1 AND @currentNullable=0)
    BEGIN
        SET @statement=N'ALTER TABLE [dbo].'+QUOTENAME(@table)+N' ALTER COLUMN '+QUOTENAME(@column)+N' nvarchar('
          +CASE WHEN @currentLength=-1 THEN N'max' ELSE CONVERT(nvarchar(12),CASE WHEN @currentLength/2>@characters THEN @currentLength/2 ELSE @characters END) END
          +N') '+CASE WHEN @currentNullable=1 OR @allowNull=1 THEN N'NULL' ELSE N'NOT NULL' END+N';';
        EXEC sys.sp_executesql @statement;
    END;
    FETCH NEXT FROM changes INTO @table,@column,@characters,@allowNull;
END;
CLOSE changes;
DEALLOCATE changes;
COMMIT;
GO
