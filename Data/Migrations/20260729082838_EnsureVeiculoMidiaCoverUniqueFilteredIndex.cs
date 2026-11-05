// Reconstructed from Release fb8a205 Data.dll; deterministic duplicate repair added for bootstrap.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260729082838_EnsureVeiculoMidiaCoverUniqueFilteredIndex")]
public class EnsureVeiculoMidiaCoverUniqueFilteredIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ;WITH Covers AS (
                SELECT Id, ROW_NUMBER() OVER (PARTITION BY VeiculoId ORDER BY Ordem, Id) AS Position
                FROM VeiculoMidia WHERE Capa = 1
            )
            UPDATE VeiculoMidia SET Capa = 0 WHERE Id IN (SELECT Id FROM Covers WHERE Position > 1);
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VeiculoMidia_VeiculoId_Capa' AND object_id = OBJECT_ID(N'VeiculoMidia'))
                DROP INDEX IX_VeiculoMidia_VeiculoId_Capa ON VeiculoMidia;
            CREATE UNIQUE INDEX IX_VeiculoMidia_VeiculoId_Capa ON VeiculoMidia (VeiculoId, Capa) WHERE Capa = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_VeiculoMidia_VeiculoId_Capa", table: "VeiculoMidia");
        migrationBuilder.CreateIndex(name: "IX_VeiculoMidia_VeiculoId_Capa", table: "VeiculoMidia", columns: ["VeiculoId", "Capa"]);
    }
}
