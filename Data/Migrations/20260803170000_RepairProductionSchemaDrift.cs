// Reconstructed from Release fb8a205 Data.dll; reviewed reference, not original source.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260803170000_RepairProductionSchemaDrift")]
public class RepairProductionSchemaDrift : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[Veiculo]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.Veiculo', 'QuantidadeCliques') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Veiculo]
                    ADD [QuantidadeCliques] int NOT NULL
                        CONSTRAINT [DF_Veiculo_QuantidadeCliques_RepairProductionSchemaDrift] DEFAULT (0);
                END;
            
                IF COL_LENGTH('dbo.Veiculo', 'QuantidadeCliques') IS NOT NULL
                   AND EXISTS (
                       SELECT 1
                       FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_SCHEMA = 'dbo'
                         AND TABLE_NAME = 'Veiculo'
                         AND COLUMN_NAME = 'QuantidadeCliques'
                         AND DATA_TYPE <> 'int'
                   )
                BEGIN
                    THROW 51020, 'Schema drift em dbo.Veiculo.QuantidadeCliques: tipo diferente de int.', 1;
                END;
            
                IF COL_LENGTH('dbo.Veiculo', 'QuantidadeCliques') IS NOT NULL
                   AND EXISTS (
                       SELECT 1
                       FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_SCHEMA = 'dbo'
                         AND TABLE_NAME = 'Veiculo'
                         AND COLUMN_NAME = 'QuantidadeCliques'
                         AND IS_NULLABLE = 'YES'
                   )
                BEGIN
                    EXEC(N'UPDATE [dbo].[Veiculo] SET [QuantidadeCliques] = 0 WHERE [QuantidadeCliques] IS NULL;');
            
                    ALTER TABLE [dbo].[Veiculo]
                    ALTER COLUMN [QuantidadeCliques] int NOT NULL;
                END;
            
                IF COL_LENGTH('dbo.Veiculo', 'QuantidadeCliques') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM sys.default_constraints dc
                       INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                       INNER JOIN sys.tables t ON t.object_id = c.object_id
                       INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                       WHERE s.name = N'dbo'
                         AND t.name = N'Veiculo'
                         AND c.name = N'QuantidadeCliques'
                   )
                BEGIN
                    ALTER TABLE [dbo].[Veiculo]
                    ADD CONSTRAINT [DF_Veiculo_QuantidadeCliques_RepairProductionSchemaDrift] DEFAULT (0) FOR [QuantidadeCliques];
                END;
            
                IF COL_LENGTH('dbo.Veiculo', 'QuantidadeVisualizacoes') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Veiculo]
                    ADD [QuantidadeVisualizacoes] int NOT NULL
                        CONSTRAINT [DF_Veiculo_QuantidadeVisualizacoes_RepairProductionSchemaDrift] DEFAULT (0);
                END;
            
                IF COL_LENGTH('dbo.Veiculo', 'QuantidadeVisualizacoes') IS NOT NULL
                   AND EXISTS (
                       SELECT 1
                       FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_SCHEMA = 'dbo'
                         AND TABLE_NAME = 'Veiculo'
                         AND COLUMN_NAME = 'QuantidadeVisualizacoes'
                         AND DATA_TYPE <> 'int'
                   )
                BEGIN
                    THROW 51021, 'Schema drift em dbo.Veiculo.QuantidadeVisualizacoes: tipo diferente de int.', 1;
                END;
            
                IF COL_LENGTH('dbo.Veiculo', 'QuantidadeVisualizacoes') IS NOT NULL
                   AND EXISTS (
                       SELECT 1
                       FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_SCHEMA = 'dbo'
                         AND TABLE_NAME = 'Veiculo'
                         AND COLUMN_NAME = 'QuantidadeVisualizacoes'
                         AND IS_NULLABLE = 'YES'
                   )
                BEGIN
                    EXEC(N'UPDATE [dbo].[Veiculo] SET [QuantidadeVisualizacoes] = 0 WHERE [QuantidadeVisualizacoes] IS NULL;');
            
                    ALTER TABLE [dbo].[Veiculo]
                    ALTER COLUMN [QuantidadeVisualizacoes] int NOT NULL;
                END;
            
                IF COL_LENGTH('dbo.Veiculo', 'QuantidadeVisualizacoes') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM sys.default_constraints dc
                       INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                       INNER JOIN sys.tables t ON t.object_id = c.object_id
                       INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                       WHERE s.name = N'dbo'
                         AND t.name = N'Veiculo'
                         AND c.name = N'QuantidadeVisualizacoes'
                   )
                BEGIN
                    ALTER TABLE [dbo].[Veiculo]
                    ADD CONSTRAINT [DF_Veiculo_QuantidadeVisualizacoes_RepairProductionSchemaDrift] DEFAULT (0) FOR [QuantidadeVisualizacoes];
                END;
            END;
            
            IF OBJECT_ID(N'[dbo].[Loja]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.Loja', 'DataAtualizacao') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Loja]
                    ADD [DataAtualizacao] datetime2 NULL;
                END;
            
                IF COL_LENGTH('dbo.Loja', 'DataAtualizacao') IS NOT NULL
                   AND EXISTS (
                       SELECT 1
                       FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_SCHEMA = 'dbo'
                         AND TABLE_NAME = 'Loja'
                         AND COLUMN_NAME = 'DataAtualizacao'
                         AND DATA_TYPE <> 'datetime2'
                   )
                BEGIN
                    THROW 51022, 'Schema drift em dbo.Loja.DataAtualizacao: tipo diferente de datetime2.', 1;
                END;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
