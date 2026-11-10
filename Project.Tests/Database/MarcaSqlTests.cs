using Data;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Project.Pages.Admin.Cadastros.Marcas;
using Xunit;
using static Project.Tests.Cadastros.MarcaTestSupport;

namespace Project.Tests.Database;

public sealed class MarcaSqlTests
{
    [SqlServerFact]
    public async Task NewNamesPersistAsActiveWithoutLogoAndEditKeepsIdentity()
    {
        await WithDatabase(async db =>
        {
            foreach (var name in new[] { "ZEEKR", "TESTE-MARCA-AUDITORIA-20261110" })
            {
                var page = Page(db, name);
                Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(default));
                Assert.Equal("Marca cadastrada com sucesso.", page.TempData["SuccessMessage"]);
            }
            db.ChangeTracker.Clear();
            var brand = await db.Marcas.SingleAsync(x => x.Nome == "ZEEKR");
            Assert.True(brand.Ativo); Assert.Null(brand.LogoUrl);
            var edit = Page(db, "  ZEEKR editada  ", brand.Id);
            edit.Marca.LogoUrl = " /synthetic.png ";
            Assert.IsType<RedirectToPageResult>(await edit.OnPostAsync(default));
            Assert.Equal("Marca atualizada com sucesso.", edit.TempData["SuccessMessage"]);
            db.ChangeTracker.Clear();
            var saved = await db.Marcas.SingleAsync(x => x.Id == brand.Id);
            Assert.Equal("ZEEKR editada", saved.Nome); Assert.Equal("/synthetic.png", saved.LogoUrl);
            Assert.True(saved.Ativo); Assert.Equal(2, await db.Marcas.CountAsync());
        });
    }

    [SqlServerFact]
    public async Task UniqueIndexRejectsDuplicatesEvenWhenWritingDirectlyThroughEf()
    {
        await WithDatabase(async db =>
        {
            db.Add(new Marca("ZEEKR", null)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            db.Add(new Marca("zeekr", null));
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(2601, Assert.IsType<SqlException>(exception.InnerException).Number);
            Assert.True(MarcaDatabaseErrors.IsNameDuplicate(exception));
            db.ChangeTracker.Clear();
            Assert.Equal("ZEEKR", (await db.Marcas.SingleAsync()).Nome);
        });
    }

    [SqlServerFact]
    public async Task ActiveDuplicatesUseRealCollationAndNeverAnnounceSuccess()
    {
        await WithDatabase(async db =>
        {
            db.Add(new Marca("ZEEKR", null)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            foreach (var name in new[] { "ZEEKR", "zeekr", " \u00a0ZeEkR\t " })
            {
                var logger = new TestLogger();
                var page = Page(db, name, logger: logger);
                Assert.IsType<PageResult>(await page.OnPostAsync(default));
                Assert.Equal(DuplicateMessage, Error(page));
                Assert.False(page.TempData.ContainsKey("SuccessMessage"));
                Assert.True(MarcaDatabaseErrors.IsNameDuplicate(Assert.Single(logger.Entries).Exception!));
                db.ChangeTracker.Clear();
                Assert.Equal(1, await db.Marcas.CountAsync());
            }
            // The production collation is accent-sensitive; do not broaden equality in .NET.
            Assert.IsType<RedirectToPageResult>(await Page(db, "ZÉEKR").OnPostAsync(default));
            Assert.Equal(2, await db.Marcas.CountAsync());
        });
    }

    [SqlServerFact]
    public async Task InactiveConflictUsesSqlEqualityWithoutReactivationAndStaysHiddenFromList()
    {
        await WithDatabase(async db =>
        {
            var brand = new Marca("ZEEKR", null); brand.Desativar();
            db.Add(brand); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var page = Page(db, " \u00a0zeekr\t ");
            Assert.IsType<PageResult>(await page.OnPostAsync(default));
            Assert.Equal(InactiveMessage, Error(page));
            Assert.False(page.TempData.ContainsKey("SuccessMessage"));
            db.ChangeTracker.Clear();
            Assert.False((await db.Marcas.SingleAsync()).Ativo);
            var listing = new IndexModel(db) { PageContext = Page(db).PageContext };
            await listing.OnGetAsync(default);
            Assert.Empty(listing.Marcas);
        });
    }

    [SqlServerFact]
    public async Task EditToActiveOrInactiveDuplicatePreservesOriginalStoredName()
    {
        await WithDatabase(async db =>
        {
            var active = new Marca("ZEEKR", null);
            var inactive = new Marca("Inativa", null); inactive.Desativar();
            var original = new Marca("Original", null);
            db.AddRange(active, inactive, original); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            foreach (var (name, error) in new[] { (" zeekr ", DuplicateMessage), (" inativa ", InactiveMessage) })
            {
                var page = Page(db, name, original.Id);
                Assert.IsType<PageResult>(await page.OnPostAsync(default));
                Assert.Equal(error, Error(page));
                Assert.Equal("Editar marca", page.PageTitle);
                Assert.False(page.TempData.ContainsKey("SuccessMessage"));
                db.ChangeTracker.Clear();
                Assert.Equal("Original", (await db.Marcas.SingleAsync(x => x.Id == original.Id)).Nome);
                Assert.Equal(3, await db.Marcas.CountAsync());
            }
            // Saving the same record with an unchanged name must not conflict with itself.
            Assert.IsType<RedirectToPageResult>(await Page(db, " Original ", original.Id).OnPostAsync(default));
        });
    }

    [SqlServerFact]
    public async Task TestTransactionRollsBackSuccessfulSaveAndFailedDuplicateKeepsPriorWork()
    {
        await WithDatabase(async db =>
        {
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                Assert.IsType<RedirectToPageResult>(await Page(db).OnPostAsync(default));
                var duplicate = Page(db, "zeekr");
                Assert.IsType<PageResult>(await duplicate.OnPostAsync(default));
                Assert.Equal(DuplicateMessage, Error(duplicate));
                db.ChangeTracker.Clear();
                Assert.Equal(1, await db.Marcas.CountAsync());
                await transaction.RollbackAsync();
            }
            db.ChangeTracker.Clear();
            Assert.Empty(await db.Marcas.ToListAsync());
        });
    }

    private static async Task WithDatabase(Func<ApplicationDbContext, Task> action)
    {
        // ANDERSON_TEST_SQL is supplied only for the owned disposable SQL container.
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ANDERSON_TEST_SQL"))
        {
            InitialCatalog = "AndersonMarca_" + Guid.NewGuid().ToString("N")
        };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            // Require the same comparison rules as the verified production column.
            Assert.Equal("SQL_Latin1_General_CP1_CI_AS", await db.Database.SqlQueryRaw<string>(
                "SELECT collation_name AS [Value] FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Marca') AND name=N'Nome'").SingleAsync());
            await action(db);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
