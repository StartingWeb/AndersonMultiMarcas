using Core.Storage;
using Data;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Project.Features.Veiculos.Handlers;
using Project.Features.Veiculos.Services;
using Project.Infrastructure.Storage;
using CatalogPage = Project.Pages.Veiculos.IndexModel;
using Xunit;

namespace Project.Tests.Recovery;

public sealed class CatalogRecoveryTests
{
    [Fact]
    public async Task PagesAndFeaturedSectionsHaveNoMissingOrRepeatedVehicles()
    {
        await using var db = CreateDb();
        await Seed(db, 42);
        using var services = Services(db);
        var first = Page(db, services, "?page=1");
        await first.OnGetAsync(default);
        var second = Page(db, services, "?page=2");
        await second.OnGetAsync(default);
        var firstIds = first.Vm.DestaquesRecentes.Concat(first.Vm.OutrosVeiculos).Select(x => x.Id).ToArray();
        var secondIds = second.Vm.DestaquesRecentes.Concat(second.Vm.OutrosVeiculos).Select(x => x.Id).ToArray();
        Assert.Equal(36, firstIds.Length);
        Assert.Equal(6, secondIds.Length);
        Assert.Equal(42, firstIds.Concat(secondIds).Distinct().Count());
        Assert.Equal(2, first.Vm.TotalPages);
        Assert.Equal(2, second.PaginaAtual);
    }

    [Theory]
    [InlineData("zero-km", false)]
    [InlineData("seminovos", true)]
    public async Task ConditionLandingUsesCommercialFlagRatherThanModelYear(string segment, bool used)
    {
        await using var db = CreateDb();
        await Seed(db, 4);
        using var services = Services(db);
        var page = Page(db, services, "");
        page.Segmento = segment;
        await page.OnGetAsync(default);
        var items = page.Vm.DestaquesRecentes.Concat(page.Vm.OutrosVeiculos).ToArray();
        Assert.Equal(2, items.Length);
        Assert.All(items, item => Assert.Equal(used, item.Seminovo));
        Assert.Null(page.Vm.Filtro.AnoMinimo);
        Assert.Null(page.Vm.Filtro.AnoMaximo);
    }

    [Fact]
    public async Task PublicCatalogExcludesSoldAndInactiveVehicles()
    {
        await using var db = CreateDb();
        await Seed(db, 4);
        var all = await db.Veiculos.OrderBy(x => x.Id).ToListAsync();
        all[0].MarcarComoVendido(DateTime.UtcNow);
        all[1].Desativar();
        await db.SaveChangesAsync();
        using var services = Services(db);
        var page = Page(db, services, "");
        await page.OnGetAsync(default);
        Assert.Equal(all.Skip(2).Select(x => x.Id).Order(), page.Vm.DestaquesRecentes.Concat(page.Vm.OutrosVeiculos).Select(x => x.Id).Order());
    }

    [Fact]
    public async Task CatalogHidesActiveSellerWhoseStoreIsInactive()
    {
        await using var db = CreateDb();
        await Seed(db, 1);
        var store = await db.Lojas.SingleAsync();
        var hidden = new Loja("Loja inativa", "Loja LTDA", null, new Email("hidden@example.test"), new Telefone("16999990000"),
            new Endereco("Rua teste", "1", null, "Centro", "Taquaritinga", Uf.SP, "15900000"));
        hidden.Desativar(); db.Add(hidden); await db.SaveChangesAsync();
        db.AddRange(new Vendedor(store.Id, "Visible seller"), new Vendedor(hidden.Id, "Hidden seller"));
        await db.SaveChangesAsync();
        using var services = Services(db);
        var page = Page(db, services, "");
        await page.OnGetAsync(default);
        Assert.Equal("Visible seller", Assert.Single(page.Vm.Vendedores).Nome);
    }

    private static ServiceProvider Services(ApplicationDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IStorageImageResolver>(new StorageImageResolver(Options.Create(new StorageOptions())));
        services.AddSingleton<IVeiculoSlugService, VeiculoSlugService>();
        services.AddMediatR(typeof(BuscarVeiculosQueryHandler).Assembly);
        return services.BuildServiceProvider();
    }

    private static CatalogPage Page(ApplicationDbContext db, IServiceProvider services, string query)
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "http";
        http.Request.Host = new HostString("localhost");
        http.Request.QueryString = new QueryString(query);
        return new(services.GetRequiredService<ISender>(), db, services.GetRequiredService<IStorageImageResolver>())
        {
            PageContext = new PageContext
            {
                HttpContext = http,
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            }
        };
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task Seed(ApplicationDbContext db, int count)
    {
        var store = new Loja("Loja teste", "Loja LTDA", null, new Email("loja@example.test"), new Telefone("16999990000"),
            new Endereco("Rua teste", "1", null, "Centro", "Taquaritinga", Uf.SP, "15900000"));
        var brand = new Marca("Marca teste", null);
        db.AddRange(store, brand);
        await db.SaveChangesAsync();
        for (var i = 0; i < count; i++)
        {
            var used = i % 2 == 0;
            var vehicle = new Veiculo(store.Id, brand.Id, "Veiculo", "Modelo " + i, used ? DateTime.UtcNow.Year : 2020, new Dinheiro(50000));
            vehicle.AtualizarComercial(true, true, i % 3 == 0, used, null, null, null);
            db.Add(vehicle);
        }
        await db.SaveChangesAsync();
    }
}
