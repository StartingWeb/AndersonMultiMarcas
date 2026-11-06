using Data;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Core.Storage;
using Project.Infrastructure.Storage;
using Project.ViewComponents;
using Xunit;

namespace Project.Tests.ViewComponents;

public sealed class SellerContactModalViewComponentTests
{
    [Fact]
    public async Task InvokeAsync_DeveUsarModeloCacheado()
    {
        await using var db = CreateDbContext();
        var loja = await SeedLojaAsync(db);
        AddVendedor(db, loja.Id, "Ana", "16999990000", "https://cdn.example.com/uploads/vendedores/ana.webp");
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var component = new SellerContactModalViewComponent(db, CreateResolver(), cache);

        var firstModel = GetModel(await component.InvokeAsync());

        AddVendedor(db, loja.Id, "Bruno", "16999990001", "https://cdn.example.com/uploads/vendedores/bruno.webp");
        await db.SaveChangesAsync();

        var secondModel = GetModel(await component.InvokeAsync());

        Assert.Single(firstModel.SelectMany(x => x.Vendedores));
        Assert.Single(secondModel.SelectMany(x => x.Vendedores));
        Assert.Equal("Ana", secondModel.SelectMany(x => x.Vendedores).Single().Nome);
    }

    [Fact]
    public async Task InvokeAsync_HidesActiveSellerWhoseStoreIsInactive()
    {
        await using var db = CreateDbContext();
        var active = await SeedLojaAsync(db);
        var hidden = await SeedLojaAsync(db);
        hidden.Desativar();
        AddVendedor(db, active.Id, "Visible seller", "16999990000", "");
        AddVendedor(db, hidden.Id, "Hidden seller", "16999990001", "");
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var component = new SellerContactModalViewComponent(db, CreateResolver(), cache);
        var model = GetModel(await component.InvokeAsync());
        Assert.Equal("Visible seller", Assert.Single(model.SelectMany(x => x.Vendedores)).Nome);
    }

    private static IReadOnlyCollection<SellerContactModalStoreGroupViewModel> GetModel(IViewComponentResult result)
    {
        var view = Assert.IsType<ViewViewComponentResult>(result);
        return Assert.IsAssignableFrom<IReadOnlyCollection<SellerContactModalStoreGroupViewModel>>(view.ViewData?.Model);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"seller-modal-tests-{Guid.NewGuid():N}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Loja> SeedLojaAsync(ApplicationDbContext db)
    {
        var loja = new Loja(
            "Loja Teste",
            "Loja Teste LTDA",
            new Documento("12345678000100"),
            new Email("loja@teste.com"),
            new Telefone("16999990000"),
            new Endereco("Rua A", "100", null, "Centro", "Taquaritinga", Uf.SP, "15900000"));

        db.Lojas.Add(loja);
        await db.SaveChangesAsync();
        return loja;
    }

    private static void AddVendedor(ApplicationDbContext db, int lojaId, string nome, string whatsapp, string fotoUrl)
    {
        var vendedor = new Vendedor(lojaId, nome);
        vendedor.Update(nome, null, null, new Telefone(whatsapp), null, fotoUrl, "Consultor");
        db.Vendedores.Add(vendedor);
    }

    private static StorageImageResolver CreateResolver()
        => new(Options.Create(new StorageOptions
        {
            Provider = StorageProviders.R2,
            R2 = new R2StorageOptions
            {
                PublicBaseUrl = "https://cdn.example.com"
            }
        }));
}
