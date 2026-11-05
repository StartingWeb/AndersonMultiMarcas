// Reconstructed from Release fb8a205 Data.dll; reviewed reference, not original source.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260803150000_NormalizeLegacyStoreData")]
public class NormalizeLegacyStoreData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'[Loja]', N'U') IS NOT NULL
            BEGIN
                UPDATE [Loja]
                SET
                    [Nome] = CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE([Nome], N''))), N'') IS NULL THEN CONCAT(N'Loja ', [Id]) ELSE LTRIM(RTRIM([Nome])) END,
                    [RazaoSocial] = CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE([RazaoSocial], N''))), N'') IS NULL THEN CONCAT(N'Loja ', [Id]) ELSE LTRIM(RTRIM([RazaoSocial])) END,
                    [Email] = CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE([Email], N''))), N'') IS NULL OR CHARINDEX(N'@', [Email]) = 0 THEN CONCAT(N'loja-', [Id], N'@andersonmultimarcas.local') ELSE LOWER(LTRIM(RTRIM([Email]))) END,
                    [Telefone] = CASE WHEN NULLIF(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(COALESCE([Telefone], N''))), N'(', N''), N')', N''), N'-', N''), N' ', N''), CHAR(9), N''), N'') IS NULL THEN N'0000000000' ELSE REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([Telefone])), N'(', N''), N')', N''), N'-', N''), N' ', N''), CHAR(9), N'') END,
                    [Endereco] = CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE([Endereco], N''))), N'') IS NULL THEN N'Endereco nao informado' ELSE LTRIM(RTRIM([Endereco])) END,
                    [Numero] = CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE([Numero], N''))), N'') IS NULL THEN N'S/N' ELSE LTRIM(RTRIM([Numero])) END,
                    [Complemento] = NULLIF(LTRIM(RTRIM(COALESCE([Complemento], N''))), N''),
                    [Bairro] = CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE([Bairro], N''))), N'') IS NULL THEN N'Nao informado' ELSE LTRIM(RTRIM([Bairro])) END,
                    [Cidade] = CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE([Cidade], N''))), N'') IS NULL THEN N'Taquaritinga' ELSE LTRIM(RTRIM([Cidade])) END,
                    [Uf] = CASE WHEN UPPER(LTRIM(RTRIM(COALESCE([Uf], N'')))) IN (N'AC',N'AL',N'AP',N'AM',N'BA',N'CE',N'DF',N'ES',N'GO',N'MA',N'MT',N'MS',N'MG',N'PA',N'PB',N'PR',N'PE',N'PI',N'RJ',N'RN',N'RS',N'RO',N'RR',N'SC',N'SP',N'SE',N'TO') THEN UPPER(LTRIM(RTRIM([Uf]))) ELSE N'SP' END,
                    [Cep] = CASE WHEN NULLIF(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(COALESCE([Cep], N''))), N'.', N''), N'-', N''), N' ', N''), N'') IS NULL THEN N'00000000' ELSE LEFT(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM([Cep])), N'.', N''), N'-', N''), N' ', N''), 8) END
                WHERE
                    NULLIF(LTRIM(RTRIM(COALESCE([Nome], N''))), N'') IS NULL
                    OR NULLIF(LTRIM(RTRIM(COALESCE([RazaoSocial], N''))), N'') IS NULL
                    OR NULLIF(LTRIM(RTRIM(COALESCE([Email], N''))), N'') IS NULL
                    OR CHARINDEX(N'@', COALESCE([Email], N'')) = 0
                    OR NULLIF(LTRIM(RTRIM(COALESCE([Telefone], N''))), N'') IS NULL
                    OR NULLIF(LTRIM(RTRIM(COALESCE([Endereco], N''))), N'') IS NULL
                    OR NULLIF(LTRIM(RTRIM(COALESCE([Numero], N''))), N'') IS NULL
                    OR NULLIF(LTRIM(RTRIM(COALESCE([Bairro], N''))), N'') IS NULL
                    OR NULLIF(LTRIM(RTRIM(COALESCE([Cidade], N''))), N'') IS NULL
                    OR UPPER(LTRIM(RTRIM(COALESCE([Uf], N'')))) NOT IN (N'AC',N'AL',N'AP',N'AM',N'BA',N'CE',N'DF',N'ES',N'GO',N'MA',N'MT',N'MS',N'MG',N'PA',N'PB',N'PR',N'PE',N'PI',N'RJ',N'RN',N'RS',N'RO',N'RR',N'SC',N'SP',N'SE',N'TO')
                    OR NULLIF(LTRIM(RTRIM(COALESCE([Cep], N''))), N'') IS NULL;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
