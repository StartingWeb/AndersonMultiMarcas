using Data;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Project.Pages.Admin.Ferramentas;
using Project.Pages.ViewModels;
using Xunit;

namespace Project.Tests.Recovery;

public sealed class CoverReviewTests
{
    [Theory]
    [InlineData(2026, true, false)]
    [InlineData(2027, true, false)]
    [InlineData(2020, false, true)]
    public void ZeroKmTagUsesConditionIndependentlyOfYear(int year, bool used, bool expected)
        => Assert.Equal(expected, new HomeVehicleCardViewModel { AnoModelo = year, Seminovo = used }.ZeroKm);

    [Fact]
    public async Task SavingCoverClearsOtherFlagsAndKeepsEveryImage()
    {
        await using var db = CreateDb();
        var (vehicle, images) = await Seed(db);
        images[0].DefinirComoCapa();
        await db.SaveChangesAsync();
        var page = new RevisarCapasVeiculosModel(db, NullLogger<RevisarCapasVeiculosModel>.Instance);
        var response = Assert.IsType<JsonResult>(await page.OnPostSaveCoverAsync(new()
        { VehicleId = vehicle.Id, MediaId = images[1].Id, Position = 1, Mode = "Todos" }, default));
        var result = Assert.IsType<RevisarCapasVeiculosModel.CoverReviewSaveResult>(response.Value);
        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        var saved = await db.VeiculoMidias.OrderBy(x => x.Ordem).ToListAsync();
        Assert.Equal(3, saved.Count);
        Assert.Equal(images[1].Id, Assert.Single(saved.Where(x => x.Capa)).Id);
        Assert.Equal(images[1].Id, saved.First().Id);
        Assert.Equal(new[] { 1, 2, 3 }, saved.Select(x => x.Ordem));
    }

    [Fact]
    public async Task CannotChooseInactiveImageOrImageFromAnotherVehicle()
    {
        await using var db = CreateDb();
        var (vehicle, images) = await Seed(db);
        var (another, otherImages) = await Seed(db);
        images[0].Desativar();
        await db.SaveChangesAsync();
        var page = new RevisarCapasVeiculosModel(db, NullLogger<RevisarCapasVeiculosModel>.Instance);
        foreach (var id in new[] { images[0].Id, otherImages[0].Id })
        {
            var response = Assert.IsType<JsonResult>(await page.OnPostSaveCoverAsync(new()
            { VehicleId = vehicle.Id, MediaId = id, Position = 1 }, default));
            Assert.Equal(400, response.StatusCode);
        }
        Assert.DoesNotContain(await db.VeiculoMidias.ToListAsync(), x => x.Capa);
    }

    [Fact]
    public async Task WithoutCoverFilterAdvancesWithoutSkippingAfterSave()
    {
        await using var db = CreateDb();
        var (first, images) = await Seed(db);
        var (second, _) = await Seed(db);
        var page = new RevisarCapasVeiculosModel(db, NullLogger<RevisarCapasVeiculosModel>.Instance);
        var response = Assert.IsType<JsonResult>(await page.OnPostSaveCoverAsync(new()
        { VehicleId = first.Id, MediaId = images[0].Id, Position = 1, Mode = "SemCapa" }, default));
        var result = Assert.IsType<RevisarCapasVeiculosModel.CoverReviewSaveResult>(response.Value);
        Assert.True(result.Success);
        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.NextPosition);
        Assert.Equal(second.Id, result.NextReview!.Current!.Id);
    }

    [Fact]
    public async Task ReviewFiltersNormalizeAndSearchAndIgnoreInactiveVehicles()
    {
        await using var db = CreateDb();
        var (first, _) = await Seed(db);
        var (inactive, _) = await Seed(db);
        inactive.Desativar();
        await db.SaveChangesAsync();
        var page = new RevisarCapasVeiculosModel(db, NullLogger<RevisarCapasVeiculosModel>.Instance);
        var response = Assert.IsType<JsonResult>(await page.OnGetVehicleAsync(99, "MaisQue", first.Id.ToString(), 2, default));
        var data = Assert.IsType<RevisarCapasVeiculosModel.CoverReviewPageData>(response.Value);
        Assert.Equal(1, data.Total);
        Assert.Equal(1, data.Position);
        Assert.Equal(first.Id, data.Current!.Id);
        Assert.Equal(3, data.Images.Count);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    internal static async Task<(Veiculo vehicle, VeiculoMidia[] images)> Seed(ApplicationDbContext db)
    {
        var store = new Loja("Loja teste", "Loja LTDA", null, new Email("loja@example.test"), new Telefone("16999990000"),
            new Endereco("Rua teste", "1", null, "Centro", "Taquaritinga", Uf.SP, "15900000"));
        var brand = new Marca("Marca teste", null);
        db.AddRange(store, brand);
        await db.SaveChangesAsync();
        var vehicle = new Veiculo(store.Id, brand.Id, "Carro", "Modelo", 2024, new Dinheiro(70000));
        db.Add(vehicle);
        await db.SaveChangesAsync();
        var images = Enumerable.Range(1, 3).Select(i => new VeiculoMidia(vehicle.Id, $"image-{i}.jpg", $"/uploads/veiculos/{vehicle.Id}/{i}.jpg", TipoMidia.Imagem, i)).ToArray();
        db.AddRange(images);
        await db.SaveChangesAsync();
        return (vehicle, images);
    }
}
