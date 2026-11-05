using Microsoft.EntityFrameworkCore.Migrations;

namespace Data.Migrations;

public partial class AlignRecoveredReleaseModel : Migration
{
    // Release migrations already made CNPJ nullable and installed the filtered cover index.
    // Align the source snapshot without repeating DDL or changing an installed schema.
    protected override void Up(MigrationBuilder migrationBuilder) { }
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
