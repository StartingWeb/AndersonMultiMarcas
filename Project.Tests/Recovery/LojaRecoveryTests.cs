using Core.Storage;
using Data;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Project.Pages.Admin.Cadastros.Lojas;
using Xunit;

namespace Project.Tests.Recovery;

public sealed class LojaRecoveryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("..//-")]
    public void CnpjOptionalAcceptsEmptyValuesAndMasks(string? value)
    {
        Assert.True(LojaCnpj.TryNormalizeOptional(value, out var normalized, out var error));
        Assert.Null(normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("04.252.011/0001-10", "04252011000110")]
    [InlineData("11222333000181", "11222333000181")]
    public void CnpjNormalizesAndValidatesCheckDigits(string value, string expected)
    {
        Assert.True(LojaCnpj.TryNormalizeOptional(value, out var normalized, out var error));
        Assert.Equal(expected, normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("00000000000000")]
    [InlineData("04.252.011/0001-11")]
    [InlineData("123")]
    [InlineData("invalid")]
    [InlineData("１１１１１１１１１１１１１１")]
    public void CnpjRejectsInvalidValues(string value)
    {
        Assert.False(LojaCnpj.TryNormalizeOptional(value, out var normalized, out var error));
        Assert.Null(normalized);
        Assert.Equal(LojaCnpj.InvalidMessage, error);
    }

    [Theory]
    [InlineData(2601, "IX_Loja_Cnpj", true)]
    [InlineData(2627, "Unique Cnpj", true)]
    [InlineData(2601, "IX_Loja_Nome", false)]
    [InlineData(515, "Cnpj may not be NULL", false)]
    [InlineData(207, "Cnpj missing column", false)]
    public void DuplicateClassificationDoesNotHideOtherDatabaseErrors(int number, string message, bool expected)
        => Assert.Equal(expected, LojaDatabaseErrors.IsCnpjDuplicateSqlError(number, message));

    [Fact]
    public async Task CreateAndEditCanPersistAndClearOptionalCnpj()
    {
        await using var db = CreateDb();
        var create = Page(db);
        create.Loja = Input("04.252.011/0001-10");
        Assert.IsType<RedirectToPageResult>(await create.OnPostAsync(default));
        var saved = await db.Lojas.SingleAsync();
        Assert.Equal("04252011000110", saved.Cnpj?.Valor);

        var edit = Page(db);
        edit.Loja = Input(null);
        edit.Loja.Id = saved.Id;
        edit.Loja.Nome = "Loja alterada";
        Assert.IsType<RedirectToPageResult>(await edit.OnPostAsync(default));
        db.ChangeTracker.Clear();
        saved = await db.Lojas.SingleAsync();
        Assert.Null(saved.Cnpj);
        Assert.Equal("Loja alterada", saved.Nome);
        Assert.NotNull(saved.DataAtualizacao);
        Assert.Equal("Loja atualizada com sucesso.", edit.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task DuplicateCnpjAddsFieldErrorAndPreservesExistingStore()
    {
        await using var db = CreateDb();
        var first = Page(db);
        first.Loja = Input("04252011000110");
        await first.OnPostAsync(default);
        var duplicate = Page(db);
        duplicate.Loja = Input("04.252.011/0001-10");
        Assert.IsType<PageResult>(await duplicate.OnPostAsync(default));
        Assert.Contains("Ja existe", duplicate.ModelState["Loja.Cnpj"]!.Errors.Single().ErrorMessage);
        Assert.Equal(1, await db.Lojas.CountAsync());
        Assert.False(duplicate.TempData.ContainsKey("SuccessMessage"));
    }

    [Fact]
    public async Task InvalidCnpjNeverPersistsStore()
    {
        await using var db = CreateDb();
        var page = Page(db);
        page.Loja = Input("123");
        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.False(page.ModelState.IsValid);
        Assert.Empty(await db.Lojas.ToListAsync());
    }

    [Fact]
    public async Task GetCanOpenStoreWithNullCnpj()
    {
        await using var db = CreateDb();
        var create = Page(db);
        create.Loja = Input(null);
        await create.OnPostAsync(default);
        var page = Page(db);
        page.Id = (await db.Lojas.SingleAsync()).Id;
        Assert.IsType<PageResult>(await page.OnGetAsync(default));
        Assert.Null(page.Loja.Cnpj);
        Assert.Equal("Editar loja", page.PageTitle);
    }

    [Fact]
    public async Task DatabaseFailureDoesNotAnnounceSuccessfulSave()
    {
        await using var db = new FailedSaveContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var page = Page(db);
        page.Loja = Input(null);
        await Assert.ThrowsAsync<DbUpdateException>(() => page.OnPostAsync(default));
        Assert.False(page.TempData.ContainsKey("SuccessMessage"));
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static UpsertModel Page(ApplicationDbContext db)
    {
        var http = new DefaultHttpContext();
        return new(db)
        {
            PageContext = new PageContext { HttpContext = http, ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary()) },
            TempData = new TempDataDictionary(http, new EmptyTempDataProvider())
        };
    }

    private static UpsertModel.LojaInputModel Input(string? cnpj) => new()
    {
        Nome = "Loja sintetica", RazaoSocial = "Loja sintetica LTDA", Cnpj = cnpj,
        Email = "loja@example.test", Telefone = "16999990000", Logradouro = "Rua teste", Numero = "1",
        Bairro = "Centro", Cidade = "Taquaritinga", Uf = Uf.SP, Cep = "15900000"
    };

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class FailedSaveContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
            => throw new DbUpdateException("Synthetic persistence failure");
    }
}
