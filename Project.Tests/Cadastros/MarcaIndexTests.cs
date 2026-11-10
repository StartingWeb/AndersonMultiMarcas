using Data;
using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Pages.Admin.Cadastros.Marcas;
using Xunit;
using static Project.Tests.Cadastros.MarcaTestSupport;

namespace Project.Tests.Cadastros;

public sealed class MarcaIndexTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public async Task CountsVehiclesByBrandIncludingZeroWithoutTrackingEntities(int count)
    {
        await using var db = new ApplicationDbContext(MemoryOptions());
        var brand = new Marca("Marca teste", null);
        db.Add(brand);
        await db.SaveChangesAsync();
        db.AddRange(Enumerable.Range(0, count).Select(i => Vehicle(brand.Id, $"Modelo {i}")));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var page = Listing(db);
        await page.OnGetAsync(default);

        Assert.Equal(count, Assert.Single(page.Marcas).QuantidadeVeiculos);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task SeparatesBrandsAndPreservesNameFilterOrderingAndInactiveBrandVisibility()
    {
        await using var db = new ApplicationDbContext(MemoryOptions());
        var first = new Marca("Alfa", null);
        var second = new Marca("Beta", null);
        var inactive = new Marca("Oculta", null);
        inactive.Desativar();
        db.AddRange(second, first, inactive);
        await db.SaveChangesAsync();
        db.AddRange(Vehicle(first.Id), Vehicle(second.Id), Vehicle(second.Id), Vehicle(inactive.Id));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var page = Listing(db);
        await page.OnGetAsync(default);
        Assert.Equal(["Alfa", "Beta"], page.Marcas.Select(x => x.Nome));
        Assert.Equal([1, 2], page.Marcas.Select(x => x.QuantidadeVeiculos));
        page.Filtro = " Beta ";
        await page.OnGetAsync(default);
        Assert.Equal(2, Assert.Single(page.Marcas).QuantidadeVeiculos);
        page.Filtro = "Inexistente";
        await page.OnGetAsync(default);
        Assert.Empty(page.Marcas);
    }

    [Fact]
    public async Task CountsSoldDeactivatedVehiclesButExcludesSoftDeletedUnsoldVehicles()
    {
        await using var db = new ApplicationDbContext(MemoryOptions());
        var brand = new Marca("Comercial", null);
        db.Add(brand);
        await db.SaveChangesAsync();
        var available = Vehicle(brand.Id);
        var sold = Vehicle(brand.Id);
        sold.MarcarComoVendido(DateTime.UtcNow);
        var deleted = Vehicle(brand.Id);
        deleted.Desativar();
        db.AddRange(available, sold, deleted);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var page = Listing(db);
        await page.OnGetAsync(default);
        Assert.Equal(2, Assert.Single(page.Marcas).QuantidadeVeiculos);
        Assert.False((await db.Veiculos.SingleAsync(x => x.Id == sold.Id)).Ativo);
        Assert.True((await db.Veiculos.SingleAsync(x => x.Id == sold.Id)).Vendido);
        Assert.False((await db.Veiculos.SingleAsync(x => x.Id == deleted.Id)).Ativo);
    }

    [Fact]
    public async Task NavigationCountRespectsVehicleQueryFiltersEvenForSoldVehicles()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new VisibilityContext(options);
        var brand = new Marca("Visível", null);
        db.Add(brand);
        await db.SaveChangesAsync();
        var hiddenSold = Vehicle(brand.Id, "Oculto");
        hiddenSold.MarcarComoVendido(DateTime.UtcNow);
        db.AddRange(Vehicle(brand.Id), Vehicle(brand.Id, "Oculto"), hiddenSold);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var page = Listing(db);
        await page.OnGetAsync(default);
        Assert.Equal(1, Assert.Single(page.Marcas).QuantidadeVeiculos);
    }

    [Fact]
    public async Task DeleteMessagesUsePortugueseAccents()
    {
        await using var db = new ApplicationDbContext(MemoryOptions());
        var brand = new Marca("Teste isolado", null);
        db.Add(brand);
        await db.SaveChangesAsync();
        var support = Page(db);
        var page = Listing(db);
        page.TempData = support.TempData;
        Assert.IsType<RedirectToPageResult>(await page.OnPostDeleteAsync(-1, null, default));
        Assert.Equal("Marca não encontrada.", page.TempData["ErrorMessage"]);
        Assert.IsType<RedirectToPageResult>(await page.OnPostDeleteAsync(brand.Id, null, default));
        Assert.Equal("Marca excluída com sucesso.", page.TempData["SuccessMessage"]);
    }

    internal static IndexModel Listing(ApplicationDbContext db) => new(db) { PageContext = Page(db).PageContext };
    internal static Veiculo Vehicle(int brandId, string model = "Modelo teste", int storeId = 1)
        => new(storeId, brandId, "Veículo teste", model, 2026, new Dinheiro(10000));

    private sealed class VisibilityContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Veiculo>().HasQueryFilter(x => x.Modelo != "Oculto");
        }
    }
}
