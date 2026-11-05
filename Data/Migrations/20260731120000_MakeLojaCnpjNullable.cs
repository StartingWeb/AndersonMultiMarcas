// Reconstructed from Release fb8a205 Data.dll; reviewed reference, not original source.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260731120000_MakeLojaCnpjNullable")]
public class MakeLojaCnpjNullable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[Loja]', N'U') IS NOT NULL AND COL_LENGTH('Loja', 'Cnpj') IS NOT NULL
            BEGIN
                IF EXISTS
                (
                    SELECT 1
                    FROM
                    (
                        SELECT
                            [NormalizedCnpj] = NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(COALESCE([Cnpj], N''))), N'.', N''), N'-', N''), N'/', N''), N' ', N''), CHAR(9), N''), CHAR(160), N''), N'')
                        FROM [Loja]
                    ) AS Normalized
                    WHERE [NormalizedCnpj] IS NOT NULL
                    GROUP BY [NormalizedCnpj]
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    THROW 51000, 'Nao foi possivel normalizar Loja.Cnpj: existem CNPJs duplicados apos remover mascara.', 1;
                END;

                IF EXISTS
                (
                    SELECT 1
                    FROM
                    (
                        SELECT
                            [NormalizedCnpj] = NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(COALESCE([Cnpj], N''))), N'.', N''), N'-', N''), N'/', N''), N' ', N''), CHAR(9), N''), CHAR(160), N''), N'')
                        FROM [Loja]
                    ) AS Normalized
                    WHERE [NormalizedCnpj] IS NOT NULL AND LEN([NormalizedCnpj]) <> 14
                )
                BEGIN
                    THROW 51001, 'Nao foi possivel normalizar Loja.Cnpj: existem CNPJs preenchidos com quantidade de digitos diferente de 14.', 1;
                END;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Loja_Cnpj' AND object_id = OBJECT_ID(N'[Loja]'))
                    DROP INDEX [IX_Loja_Cnpj] ON [Loja];

                ALTER TABLE [Loja] ALTER COLUMN [Cnpj] nvarchar(18) NULL;

                ;WITH Normalized AS
                (
                    SELECT
                        [Id],
                        [NormalizedCnpj] = NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(COALESCE([Cnpj], N''))), N'.', N''), N'-', N''), N'/', N''), N' ', N''), CHAR(9), N''), CHAR(160), N''), N'')
                    FROM [Loja]
                )
                UPDATE l
                SET [Cnpj] = n.[NormalizedCnpj]
                FROM [Loja] l
                INNER JOIN Normalized n ON n.[Id] = l.[Id];

                ALTER TABLE [Loja] ALTER COLUMN [Cnpj] nvarchar(14) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Loja_Cnpj' AND object_id = OBJECT_ID(N'[Loja]'))
                    CREATE UNIQUE INDEX [IX_Loja_Cnpj] ON [Loja] ([Cnpj]) WHERE [Cnpj] IS NOT NULL;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[Loja]', N'U') IS NOT NULL AND COL_LENGTH('Loja', 'Cnpj') IS NOT NULL
            BEGIN
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Loja_Cnpj' AND object_id = OBJECT_ID(N'[Loja]'))
                    DROP INDEX [IX_Loja_Cnpj] ON [Loja];

                UPDATE [Loja]
                SET [Cnpj] = N''
                WHERE [Cnpj] IS NULL;

                ALTER TABLE [Loja] ALTER COLUMN [Cnpj] nvarchar(14) NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Loja_Cnpj' AND object_id = OBJECT_ID(N'[Loja]'))
                    CREATE UNIQUE INDEX [IX_Loja_Cnpj] ON [Loja] ([Cnpj]) WHERE [Cnpj] IS NOT NULL;
            END
            """);
    }
}
