// Reconstructed from Release fb8a205 Data.dll; reviewed reference, not original source.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260803193000_RepairVeiculoMidiaUploadSchema")]
public class RepairVeiculoMidiaUploadSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[dbo].[VeiculoMidia]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.VeiculoMidia', 'BlobName') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[VeiculoMidia] ALTER COLUMN [BlobName] nvarchar(250) NULL;
                END;
            
                IF COL_LENGTH('dbo.VeiculoMidia', 'Container') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[VeiculoMidia] ALTER COLUMN [Container] nvarchar(250) NULL;
                END;
            
                IF COL_LENGTH('dbo.VeiculoMidia', 'ContentType') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[VeiculoMidia] ALTER COLUMN [ContentType] nvarchar(120) NULL;
                END;
            
                IF COL_LENGTH('dbo.VeiculoMidia', 'Tipo') IS NOT NULL
                BEGIN
                    UPDATE [dbo].[VeiculoMidia]
                    SET [Tipo] = N'Imagem'
                    WHERE [Tipo] IS NULL OR LTRIM(RTRIM([Tipo])) = N'';
            
                    ALTER TABLE [dbo].[VeiculoMidia] ALTER COLUMN [Tipo] nvarchar(20) NOT NULL;
                END;
            
                IF COL_LENGTH('dbo.VeiculoMidia', 'Capa') IS NOT NULL
                BEGIN
                    ;WITH CapasDuplicadas AS (
                        SELECT
                            [Id],
                            ROW_NUMBER() OVER (PARTITION BY [VeiculoId] ORDER BY [Ordem], [Id]) AS [Numero]
                        FROM [dbo].[VeiculoMidia]
                        WHERE [Capa] = 1
                    )
                    UPDATE vm
                    SET [Capa] = 0
                    FROM [dbo].[VeiculoMidia] vm
                    INNER JOIN CapasDuplicadas cd ON cd.[Id] = vm.[Id]
                    WHERE cd.[Numero] > 1;
                END;
            
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_VeiculoMidia_VeiculoId_Capa'
                      AND object_id = OBJECT_ID(N'[dbo].[VeiculoMidia]')
                )
                BEGIN
                    DROP INDEX [IX_VeiculoMidia_VeiculoId_Capa] ON [dbo].[VeiculoMidia];
                END;
            
                IF COL_LENGTH('dbo.VeiculoMidia', 'VeiculoId') IS NOT NULL
                   AND COL_LENGTH('dbo.VeiculoMidia', 'Capa') IS NOT NULL
                BEGIN
                    CREATE UNIQUE INDEX [IX_VeiculoMidia_VeiculoId_Capa]
                    ON [dbo].[VeiculoMidia] ([VeiculoId], [Capa])
                    WHERE [Capa] = 1;
                END;
            
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_VeiculoMidia_VeiculoId_Ordem'
                      AND object_id = OBJECT_ID(N'[dbo].[VeiculoMidia]')
                )
                   AND COL_LENGTH('dbo.VeiculoMidia', 'VeiculoId') IS NOT NULL
                   AND COL_LENGTH('dbo.VeiculoMidia', 'Ordem') IS NOT NULL
                BEGIN
                    CREATE INDEX [IX_VeiculoMidia_VeiculoId_Ordem]
                    ON [dbo].[VeiculoMidia] ([VeiculoId], [Ordem]);
                END;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
