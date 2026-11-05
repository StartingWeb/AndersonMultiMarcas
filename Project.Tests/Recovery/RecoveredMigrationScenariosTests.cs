using Data;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Project.Tests.Database;
using Xunit;

namespace Project.Tests.Recovery;

public sealed class RecoveredMigrationScenariosTests
{
    [SqlServerFact]
    public async Task RecoveredMigrationsNormalizeFormattedCnpjAndIncompleteLegacyStore()
    {
        await WithDatabase(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260728032441_FinalImportOperationCenterEnhancements");
            var first = Store("04252011000110");
            var second = Store("11222333000181");
            db.AddRange(first, second);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("""
                DROP INDEX IX_Loja_Cnpj ON Loja;
                ALTER TABLE Loja ALTER COLUMN Cnpj nvarchar(18) NULL;
                ALTER TABLE Loja ALTER COLUMN Email nvarchar(180) NULL;
                ALTER TABLE Loja ALTER COLUMN Telefone nvarchar(20) NULL;
                ALTER TABLE Loja ALTER COLUMN Cidade nvarchar(100) NULL;
                ALTER TABLE Loja ALTER COLUMN Uf nvarchar(2) NULL;
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Loja SET Cnpj=N'04.252.011/0001-10' WHERE Id={first.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Loja SET Cnpj=N'  ', Email=NULL, Telefone=NULL, Cidade=NULL, Uf=NULL WHERE Id={second.Id}");
            db.ChangeTracker.Clear();
            await migrator.MigrateAsync();
            var all = await db.Lojas.OrderBy(x => x.Id).ToListAsync();
            Assert.Equal("04252011000110", all[0].Cnpj?.Valor);
            Assert.Null(all[1].Cnpj);
            Assert.Contains("@", all[1].Email.Valor);
            Assert.Equal("Taquaritinga", all[1].Endereco.Cidade);
            Assert.Equal(Uf.SP, all[1].Endereco.Uf);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        });
    }

    [SqlServerFact]
    public async Task RecoveredMigrationsRepairMissingCountersAuditColumnAndUploadLengths()
    {
        await WithDatabase(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260728032441_FinalImportOperationCenterEnhancements");
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @sql nvarchar(max)=N'';
                SELECT @sql=@sql + N'ALTER TABLE Veiculo DROP CONSTRAINT [' + dc.name + N'];'
                FROM sys.default_constraints dc INNER JOIN sys.columns c ON c.default_object_id=dc.object_id
                WHERE c.object_id=OBJECT_ID(N'Veiculo') AND c.name IN (N'QuantidadeCliques',N'QuantidadeVisualizacoes');
                IF @sql<>N'' EXEC sp_executesql @sql;
                ALTER TABLE Veiculo DROP COLUMN QuantidadeCliques, QuantidadeVisualizacoes;
                ALTER TABLE Loja DROP COLUMN DataAtualizacao;
                ALTER TABLE VeiculoMidia ALTER COLUMN BlobName nvarchar(10) NOT NULL;
                ALTER TABLE VeiculoMidia ALTER COLUMN Container nvarchar(10) NOT NULL;
                ALTER TABLE VeiculoMidia ALTER COLUMN ContentType nvarchar(10) NOT NULL;
                """);
            await migrator.MigrateAsync();
            var (_, images) = await CoverReviewTests.Seed(db);
            images[0].UpdateStorage(new string('b', 250), new string('c', 250), "image/webp", 123);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var vehicle = await db.Veiculos.SingleAsync();
            Assert.Equal(0, vehicle.QuantidadeCliques);
            Assert.Equal(0, vehicle.QuantidadeVisualizacoes);
            Assert.Equal(250, (await db.VeiculoMidias.SingleAsync(x => x.Id == images[0].Id)).BlobName!.Length);
            var store = await db.Lojas.SingleAsync();
            Assert.Null(store.DataAtualizacao);
        });
    }

    [SqlServerFact]
    public async Task RecoveredCnpjMigrationRefusesDuplicatesInsteadOfDroppingData()
    {
        await WithDatabase(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260729082838_EnsureVeiculoMidiaCoverUniqueFilteredIndex");
            var first = Store("04252011000110");
            var second = Store("11222333000181");
            db.AddRange(first, second);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("DROP INDEX IX_Loja_Cnpj ON Loja; ALTER TABLE Loja ALTER COLUMN Cnpj nvarchar(18) NULL;");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Loja SET Cnpj=N'04.252.011/0001-10' WHERE Id={second.Id}");
            var error = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync());
            Assert.Equal(51000, error.Number);
            Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM Loja").SingleAsync());
            Assert.DoesNotContain("20260731120000_MakeLojaCnpjNullable", await db.Database.GetAppliedMigrationsAsync());
        });
    }

    private static Loja Store(string cnpj) => new("Loja sintetica", "Loja LTDA", new Documento(cnpj),
        new Email("loja@example.test"), new Telefone("16999990000"),
        new Endereco("Rua teste", "1", null, "Centro", "Taquaritinga", Uf.SP, "15900000"));

    private static async Task WithDatabase(Func<ApplicationDbContext, Task> action)
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ANDERSON_TEST_SQL"))
        { InitialCatalog = "AndersonRecoveredMigrations_" + Guid.NewGuid().ToString("N") };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try { await action(db); }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
