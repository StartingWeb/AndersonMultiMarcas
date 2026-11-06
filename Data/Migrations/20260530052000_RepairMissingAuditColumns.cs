using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260530052000_RepairMissingAuditColumns")]
    public partial class RepairMissingAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[Veiculo]', N'U') IS NOT NULL AND COL_LENGTH('Veiculo', 'Ativo') IS NULL
                    ALTER TABLE [Veiculo] ADD [Ativo] bit NOT NULL CONSTRAINT [DF_Veiculo_Ativo] DEFAULT (1);

                IF OBJECT_ID(N'[Veiculo]', N'U') IS NOT NULL AND COL_LENGTH('Veiculo', 'DataCadastro') IS NULL
                    ALTER TABLE [Veiculo] ADD [DataCadastro] datetime2 NOT NULL CONSTRAINT [DF_Veiculo_DataCadastro] DEFAULT (SYSUTCDATETIME());

                IF OBJECT_ID(N'[VeiculoMidia]', N'U') IS NOT NULL AND COL_LENGTH('VeiculoMidia', 'Ativo') IS NULL
                    ALTER TABLE [VeiculoMidia] ADD [Ativo] bit NOT NULL CONSTRAINT [DF_VeiculoMidia_Ativo] DEFAULT (1);

                IF OBJECT_ID(N'[VeiculoMidia]', N'U') IS NOT NULL AND COL_LENGTH('VeiculoMidia', 'DataCadastro') IS NULL
                    ALTER TABLE [VeiculoMidia] ADD [DataCadastro] datetime2 NOT NULL CONSTRAINT [DF_VeiculoMidia_DataCadastro] DEFAULT (SYSUTCDATETIME());

                IF OBJECT_ID(N'[VeiculoCaracteristica]', N'U') IS NOT NULL AND COL_LENGTH('VeiculoCaracteristica', 'Ativo') IS NULL
                    ALTER TABLE [VeiculoCaracteristica] ADD [Ativo] bit NOT NULL CONSTRAINT [DF_VeiculoCaracteristica_Ativo] DEFAULT (1);

                IF OBJECT_ID(N'[VeiculoCaracteristica]', N'U') IS NOT NULL AND COL_LENGTH('VeiculoCaracteristica', 'DataCadastro') IS NULL
                    ALTER TABLE [VeiculoCaracteristica] ADD [DataCadastro] datetime2 NOT NULL CONSTRAINT [DF_VeiculoCaracteristica_DataCadastro] DEFAULT (SYSUTCDATETIME());

                IF OBJECT_ID(N'[Loja]', N'U') IS NOT NULL AND COL_LENGTH('Loja', 'Ativo') IS NULL
                    ALTER TABLE [Loja] ADD [Ativo] bit NOT NULL CONSTRAINT [DF_Loja_Ativo] DEFAULT (1);

                IF OBJECT_ID(N'[Loja]', N'U') IS NOT NULL AND COL_LENGTH('Loja', 'DataCadastro') IS NULL
                    ALTER TABLE [Loja] ADD [DataCadastro] datetime2 NOT NULL CONSTRAINT [DF_Loja_DataCadastro] DEFAULT (SYSUTCDATETIME());

                IF OBJECT_ID(N'[Marca]', N'U') IS NOT NULL AND COL_LENGTH('Marca', 'Ativo') IS NULL
                    ALTER TABLE [Marca] ADD [Ativo] bit NOT NULL CONSTRAINT [DF_Marca_Ativo] DEFAULT (1);

                IF OBJECT_ID(N'[Marca]', N'U') IS NOT NULL AND COL_LENGTH('Marca', 'DataCadastro') IS NULL
                    ALTER TABLE [Marca] ADD [DataCadastro] datetime2 NOT NULL CONSTRAINT [DF_Marca_DataCadastro] DEFAULT (SYSUTCDATETIME());

                IF OBJECT_ID(N'[Vendedor]', N'U') IS NOT NULL AND COL_LENGTH('Vendedor', 'Ativo') IS NULL
                    ALTER TABLE [Vendedor] ADD [Ativo] bit NOT NULL CONSTRAINT [DF_Vendedor_Ativo] DEFAULT (1);

                IF OBJECT_ID(N'[Vendedor]', N'U') IS NOT NULL AND COL_LENGTH('Vendedor', 'DataCadastro') IS NULL
                    ALTER TABLE [Vendedor] ADD [DataCadastro] datetime2 NOT NULL CONSTRAINT [DF_Vendedor_DataCadastro] DEFAULT (SYSUTCDATETIME());
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema repair migration is intentionally one-way.
        }
    }
}
