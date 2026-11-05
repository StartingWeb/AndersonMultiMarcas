using Core.Storage;
using Data;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Project.Features.Veiculos.Commands;
using Project.Features.Veiculos.DTOs;
using Project.Features.Veiculos.Handlers;
using Project.Features.Veiculos.Queries;
using Project.Features.Veiculos.Services;
using Project.Infrastructure.Storage;
using Xunit;

namespace Project.Tests.Database;

public sealed class VehicleSqlRegressionTests
{
    [SqlServerFact]
    public async Task PriceOrderingFiltersAndPaginationExecuteOnSqlServer()
    {
        await WithDatabase(async db =>
        {
            var (loja, marca) = await SeedAsync(db);
            var vehicles = new[] { 30000m, 10000m, 20000m, 20000m, 40000m }.Select((price, i) => new Veiculo(loja.Id, marca.Id, "SQL teste", $"Modelo {i}", 2026, new Dinheiro(price))).ToArray();
            db.AddRange(vehicles);
            var sold = new Veiculo(loja.Id, marca.Id, "Vendido", "Vendido", 2026, new Dinheiro(15000));
            sold.MarcarComoVendido(DateTime.UtcNow);
            var inactive = new Veiculo(loja.Id, marca.Id, "Inativo", "Inativo", 2026, new Dinheiro(16000));
            inactive.Desativar();
            db.AddRange(sold, inactive);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var handler = new BuscarVeiculosQueryHandler(db, new VeiculoSlugService(), new StorageImageResolver(Options.Create(new StorageOptions())));
            var ascending = await handler.Handle(new BuscarVeiculosQuery(new BuscarVeiculosFiltroDto { OrdenarPor = "preco-asc", PageSize = 2 }), CancellationToken.None);
            Assert.True(ascending.IsSuccess);
            Assert.Equal(5, ascending.Value!.TotalItems);
            Assert.Equal([10000m, 20000m], ascending.Value!.Items.Select(x => x.PrecoVenda));
            Assert.Equal(vehicles[2].Id, ascending.Value!.Items.Last().Id);
            var second = await handler.Handle(new BuscarVeiculosQuery(new BuscarVeiculosFiltroDto { OrdenarPor = "preco-asc", PageSize = 2, Page = 2 }), CancellationToken.None);
            Assert.Equal([20000m, 30000m], second.Value!.Items.Select(x => x.PrecoVenda));
            Assert.Equal(vehicles[3].Id, second.Value!.Items.First().Id);
            Assert.Empty(ascending.Value!.Items.Select(x => x.Id).Intersect(second.Value!.Items.Select(x => x.Id)));
            var descending = await handler.Handle(new BuscarVeiculosQuery(new BuscarVeiculosFiltroDto { OrdenarPor = "preco-desc", PageSize = 100 }), CancellationToken.None);
            Assert.Equal([40000m, 30000m, 20000m, 20000m, 10000m], descending.Value!.Items.Select(x => x.PrecoVenda));
            Assert.Equal([vehicles[2].Id, vehicles[3].Id], descending.Value!.Items.Where(x => x.PrecoVenda == 20000m).Select(x => x.Id));
            var range = await handler.Handle(new BuscarVeiculosQuery(new BuscarVeiculosFiltroDto { OrdenarPor = "preco-asc", PrecoMinimo = 20000m, PrecoMaximo = 30000m }), CancellationToken.None);
            Assert.Equal([20000m, 20000m, 30000m], range.Value!.Items.Select(x => x.PrecoVenda));
            var minimum = await handler.Handle(new BuscarVeiculosQuery(new BuscarVeiculosFiltroDto { OrdenarPor = "preco-asc", PrecoMinimo = 30000m }), CancellationToken.None);
            Assert.Equal([30000m, 40000m], minimum.Value!.Items.Select(x => x.PrecoVenda));
            var maximum = await handler.Handle(new BuscarVeiculosQuery(new BuscarVeiculosFiltroDto { OrdenarPor = "preco-asc", PrecoMaximo = 10000m }), CancellationToken.None);
            Assert.Equal([10000m], maximum.Value!.Items.Select(x => x.PrecoVenda));
        });
    }

    [SqlServerFact]
    public async Task CommercialEditPreservesOmittedFieldsAndClearsExplicitNullsInSqlServer()
    {
        await WithDatabase(async db =>
        {
            var (loja, marca) = await SeedAsync(db);
            var seller = new Vendedor(loja.Id, "Vendedor sintético");
            db.Add(seller);
            await db.SaveChangesAsync();
            var vehicle = new Veiculo(loja.Id, marca.Id, "Teste", "Modelo", 2026, new Dinheiro(10000));
            vehicle.AtualizarComercial(true, true, true, true, "https://example.test/video", "Observacao privada sintetica", seller.Id);
            var soldAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
            vehicle.MarcarComoVendido(soldAt);
            db.Add(vehicle);
            await db.SaveChangesAsync();
            var id = vehicle.Id;
            db.ChangeTracker.Clear();
            var handler = new AtualizarVeiculoCommandHandler(db);
            Assert.True((await handler.Handle(new AtualizarVeiculoCommand(new VeiculoUpdateDto
            {
                Id = id, LojaId = loja.Id, MarcaId = marca.Id, Titulo = "Editado", Modelo = "Modelo", AnoModelo = 2026, PrecoVenda = 12000
            }), CancellationToken.None)).IsSuccess);
            db.ChangeTracker.Clear();
            var persisted = await db.Veiculos.SingleAsync(x => x.Id == id);
            Assert.Equal("https://example.test/video", persisted.UrlVideo);
            Assert.Equal("Observacao privada sintetica", persisted.ObservacoesInternas);
            Assert.Equal(seller.Id, persisted.VendedorId);
            Assert.Equal(soldAt, persisted.DataVenda);
            Assert.True(persisted.Vendido);
            Assert.True((await handler.Handle(new AtualizarVeiculoCommand(new VeiculoUpdateDto
            {
                Id = id, LojaId = loja.Id, MarcaId = marca.Id, Titulo = "Editado", Modelo = "Modelo", AnoModelo = 2026, PrecoVenda = 12000,
                UrlVideo = null, ObservacoesInternas = null, VendedorId = null
            }), CancellationToken.None)).IsSuccess);
            db.ChangeTracker.Clear();
            persisted = await db.Veiculos.SingleAsync(x => x.Id == id);
            Assert.Null(persisted.UrlVideo); Assert.Null(persisted.ObservacoesInternas); Assert.Null(persisted.VendedorId);
            Assert.Equal(soldAt, persisted.DataVenda);
        });
    }

    [SqlServerFact]
    public async Task UploadReplacesExistingCoverAtomicallyWithoutUniqueIndexViolation()
    {
        await WithDatabase(async db =>
        {
            var (loja, marca) = await SeedAsync(db);
            var vehicle = new Veiculo(loja.Id, marca.Id, "Teste", "Modelo", 2026, new Dinheiro(10000));
            db.Add(vehicle); await db.SaveChangesAsync();
            var first = new VeiculoMidia(vehicle.Id, "a.webp", "https://example.test/a.webp", TipoMidia.Imagem, 0);
            var second = new VeiculoMidia(vehicle.Id, "b.webp", "https://example.test/b.webp", TipoMidia.Imagem, 1);
            second.DefinirComoCapa();
            db.AddRange(first, second); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var mediaService = new UploadProbeService(new VeiculoMediaUploadResult("https://example.test/new.webp", "new.webp", "uploads/veiculos/new.webp", "synthetic", "image/webp", 1));
            var result = await new UploadMidiaVeiculoCommandHandler(db, mediaService).Handle(new UploadMidiaVeiculoCommand(new VeiculoMidiaUploadDto { VeiculoId = vehicle.Id }, []), default);
            Assert.True(result.IsSuccess);
            db.ChangeTracker.Clear();
            Assert.Equal(3, await db.VeiculoMidias.CountAsync(x => x.VeiculoId == vehicle.Id));
            Assert.Equal(first.Id, (await db.VeiculoMidias.SingleAsync(x => x.VeiculoId == vehicle.Id && x.Capa)).Id);
            Assert.Empty(mediaService.Removed);
        });
    }

    [SqlServerFact]
    public async Task DatabaseUploadFailureRemovesOnlyUnreferencedNewObjectsAndRollsBackCover()
    {
        await WithDatabase(async db =>
        {
            var (loja, marca) = await SeedAsync(db);
            var vehicle = new Veiculo(loja.Id, marca.Id, "Teste", "Modelo", 2026, new Dinheiro(10000));
            db.Add(vehicle); await db.SaveChangesAsync();
            var existing = new VeiculoMidia(vehicle.Id, "existing.webp", "https://example.test/existing.webp", TipoMidia.Imagem, 0);
            existing.UpdateStorage("uploads/veiculos/existing.webp", "synthetic", "image/webp", 1); existing.DefinirComoCapa();
            db.Add(existing); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var service = new UploadProbeService(
                new VeiculoMediaUploadResult("https://example.test/new.webp", new string('x', 201), "uploads/veiculos/new.webp", "synthetic", "image/webp", 1),
                new VeiculoMediaUploadResult("https://example.test/existing.webp", "reused.webp", "uploads/veiculos/existing.webp", "synthetic", "image/webp", 1));
            var result = await new UploadMidiaVeiculoCommandHandler(db, service).Handle(new UploadMidiaVeiculoCommand(new VeiculoMidiaUploadDto { VeiculoId = vehicle.Id }, []), default);
            Assert.False(result.IsSuccess);
            Assert.Equal(["uploads/veiculos/new.webp"], service.Removed);
            db.ChangeTracker.Clear();
            Assert.Single(await db.VeiculoMidias.Where(x => x.VeiculoId == vehicle.Id).ToListAsync());
            Assert.True((await db.VeiculoMidias.SingleAsync(x => x.Id == existing.Id)).Capa);
        });
    }

    private sealed class UploadProbeService(params VeiculoMediaUploadResult[] results) : IVeiculoMediaService
    {
        public List<string?> Removed { get; } = [];
        public Task<IReadOnlyCollection<VeiculoMediaUploadResult>> ProcessarUploadAsync(int veiculoId, IReadOnlyCollection<Microsoft.AspNetCore.Http.IFormFile> files, CancellationToken ct)
            => Task.FromResult<IReadOnlyCollection<VeiculoMediaUploadResult>>(results);
        public Task RemoverArquivoAsync(string? source, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoverArquivoAsync(StorageImageReference source, CancellationToken ct) { Removed.Add(source.BlobName); return Task.CompletedTask; }
    }

    private static async Task<(Loja Loja, Marca Marca)> SeedAsync(ApplicationDbContext db)
    {
        var loja = new Loja("Loja SQL sintética", "Teste", null, new Email("loja@example.test"), new Telefone("16999990000"), new Endereco("Rua A", "1", null, "Centro", "Taquaritinga", Uf.SP, "15900000"));
        var marca = new Marca("Marca SQL sintética", null);
        db.AddRange(loja, marca);
        await db.SaveChangesAsync();
        return (loja, marca);
    }

    private static async Task WithDatabase(Func<ApplicationDbContext, Task> action)
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ANDERSON_TEST_SQL")) { InitialCatalog = "AndersonVehicle_" + Guid.NewGuid().ToString("N") };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try { await db.Database.MigrateAsync(); await action(db); }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
