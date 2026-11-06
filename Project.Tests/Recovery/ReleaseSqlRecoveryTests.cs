using Core.Storage;
using Data;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Project.Infrastructure.Storage;
using Project.Pages;
using Project.Pages.Admin.Cadastros.Lojas;
using Project.Pages.Admin.Ferramentas;
using Project.Tests.Database;
using Xunit;

namespace Project.Tests.Recovery;

public sealed class ReleaseSqlRecoveryTests
{
    [SqlServerFact]
    public async Task SqlStoreAllowsMultipleMissingCnpjsAndRejectsRealDuplicates()
    {
        await WithDatabase(async db =>
        {
            db.Lojas.AddRange(Store(null), Store(null), Store(new Documento("04252011000110")));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.Lojas.CountAsync(x => x.Cnpj == null));
            db.Lojas.Add(Store(new Documento("04252011000110")));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.True(LojaDatabaseErrors.IsCnpjDuplicate(error));
        });
    }

    [SqlServerFact]
    public async Task SqlCoverSelectionRespectsUniqueIndexAndPreservesImages()
    {
        await WithDatabase(async db =>
        {
            var (vehicle, images) = await CoverReviewTests.Seed(db);
            images[0].DefinirComoCapa();
            await db.SaveChangesAsync();
            var page = new RevisarCapasVeiculosModel(db, NullLogger<RevisarCapasVeiculosModel>.Instance);
            var response = Assert.IsType<JsonResult>(await page.OnPostSaveCoverAsync(new()
            { VehicleId = vehicle.Id, MediaId = images[1].Id, Position = 1, Mode = "Todos" }, default));
            Assert.True(Assert.IsType<RevisarCapasVeiculosModel.CoverReviewSaveResult>(response.Value).Success);
            db.ChangeTracker.Clear();
            Assert.Equal(images[1].Id, (await db.VeiculoMidias.SingleAsync(x => x.Capa)).Id);
            Assert.Equal(3, await db.VeiculoMidias.CountAsync());
            var first = await db.VeiculoMidias.FindAsync(images[0].Id);
            first!.DefinirComoCapa();
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [SqlServerFact]
    public async Task PublicStorePageToleratesLegacyNullsAndHidesInactiveStores()
    {
        await WithDatabase(async db =>
        {
            var active = Store(null);
            var hidden = Store(null);
            hidden.Desativar();
            db.AddRange(active, hidden);
            await db.SaveChangesAsync();
            // Reproduce an older schema containing nullable required value-object columns, only inside this test database.
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE Loja ALTER COLUMN Email nvarchar(180) NULL;
                ALTER TABLE Loja ALTER COLUMN Telefone nvarchar(20) NULL;
                ALTER TABLE Loja ALTER COLUMN Cidade nvarchar(100) NULL;
                ALTER TABLE Loja ALTER COLUMN Uf nvarchar(2) NULL;
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Loja SET Email=NULL, Telefone=NULL, Cidade=NULL, Uf=NULL WHERE Id={active.Id}");
            db.ChangeTracker.Clear();
            var http = new DefaultHttpContext();
            http.Request.Scheme = "http";
            http.Request.Host = new HostString("localhost");
            var page = new ContatoModel(db, new StorageImageResolver(Options.Create(new StorageOptions())))
            {
                PageContext = new PageContext
                {
                    HttpContext = http,
                    ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                }
            };
            await page.OnGetAsync(default);
            var result = Assert.Single(page.Lojas);
            Assert.Equal(active.Nome, result.Nome);
            Assert.Empty(result.Email);
            Assert.Empty(result.Telefone);
            Assert.DoesNotContain(" - ", result.EnderecoCompleto);
            var admin = new Project.Pages.Admin.Cadastros.Lojas.IndexModel(db)
            {
                PageContext = page.PageContext
            };
            await admin.OnGetAsync(default);
            var adminResult = Assert.Single(admin.Lojas);
            Assert.Equal("-", adminResult.Cnpj);
            Assert.Equal("-", adminResult.Email);
        });
    }

    [SqlServerFact]
    public async Task CompanyPageHidesActiveSellerWhoseStoreIsInactive()
    {
        await WithDatabase(async db =>
        {
            var active = Store(null); var hidden = Store(null); hidden.Desativar();
            db.AddRange(active, hidden); await db.SaveChangesAsync();
            db.AddRange(new Vendedor(active.Id, "Visible seller"), new Vendedor(hidden.Id, "Hidden seller"));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var http = new DefaultHttpContext(); http.Request.Scheme = "http"; http.Request.Host = new HostString("localhost");
            var page = new EmpresaModel(db, new StorageImageResolver(Options.Create(new StorageOptions())))
            {
                PageContext = new PageContext { HttpContext = http, ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) }
            };
            await page.OnGetAsync(default);
            Assert.Equal("Visible seller", Assert.Single(page.Vendedores).Nome);
        });
    }

    private static Loja Store(Documento? cnpj) => new("Loja sintetica", "Loja LTDA", cnpj,
        new Email("loja@example.test"), new Telefone("16999990000"),
        new Endereco("Rua teste", "1", null, "Centro", "Taquaritinga", Uf.SP, "15900000"));

    private static async Task WithDatabase(Func<ApplicationDbContext, Task> action)
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ANDERSON_TEST_SQL"))
        { InitialCatalog = "AndersonRecovery_" + Guid.NewGuid().ToString("N") };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            await action(db);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
