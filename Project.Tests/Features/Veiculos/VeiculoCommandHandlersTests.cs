using Data;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Project.Features.Veiculos.Commands;
using Project.Features.Veiculos.DTOs;
using Project.Features.Veiculos.Handlers;
using Xunit;

namespace Project.Tests.Features.Veiculos;

public sealed class VeiculoCommandHandlersTests
{
    [Fact]
    public async Task CriarVeiculo_DevePersistirDadosBasicosECaracteristicas()
    {
        await using var db = CreateDbContext();
        var (lojaId, marcaId) = await SeedLojaMarcaAsync(db);
        var handler = new CriarVeiculoCommandHandler(db);

        var dto = new VeiculoCreateDto
        {
            LojaId = lojaId,
            MarcaId = marcaId,
            Titulo = "Chevrolet",
            Modelo = "Onix",
            Versao = "LT",
            AnoFabricacao = 2023,
            AnoModelo = 2024,
            Cor = "Branco",
            Combustivel = Combustivel.Flex,
            Cambio = Cambio.Automatico,
            PrecoVenda = 78990m,
            Quilometragem = 12000,
            Placa = "ABC1234",
            Descricao = "Veiculo de teste",
            Destaque = true,
            Seminovo = true,
            Financiavel = true,
            AceitaTroca = true,
            Opcionais = [TipoVeiculoOpcional.ArCondicionado, TipoVeiculoOpcional.FreiosAbs]
        };

        var result = await handler.Handle(new CriarVeiculoCommand(dto), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value > 0);

        var salvo = await db.Veiculos
            .Include(x => x.Caracteristicas)
            .FirstAsync(x => x.Id == result.Value);

        Assert.Equal("Chevrolet", salvo.Titulo);
        Assert.Equal("Onix", salvo.Modelo);
        Assert.Equal("LT", salvo.Versao);
        Assert.Equal(2024, salvo.AnoModelo);
        Assert.Equal(78990m, salvo.PrecoVenda.Valor);
        Assert.True(salvo.Seminovo);
        Assert.NotNull(salvo.Caracteristicas);
        Assert.True(salvo.Caracteristicas!.ArCondicionado);
        Assert.True(salvo.Caracteristicas.FreiosAbs);
    }

    [Fact]
    public async Task AtualizarVeiculo_DeveAtualizarDadosEReescreverOpcionais()
    {
        await using var db = CreateDbContext();
        var (lojaId, marcaId) = await SeedLojaMarcaAsync(db);
        var novaMarca = new Marca("Nova Marca Teste", null);
        db.Marcas.Add(novaMarca);
        await db.SaveChangesAsync();

        var criarHandler = new CriarVeiculoCommandHandler(db);
        var criar = await criarHandler.Handle(new CriarVeiculoCommand(new VeiculoCreateDto
        {
            LojaId = lojaId,
            MarcaId = marcaId,
            Titulo = "Volkswagen",
            Modelo = "T-Cross",
            Versao = "Comfortline",
            AnoFabricacao = 2022,
            AnoModelo = 2023,
            Cor = "Prata",
            Combustivel = Combustivel.Flex,
            Cambio = Cambio.Automatico,
            PrecoVenda = 119900m,
            Quilometragem = 25000,
            Destaque = false,
            Seminovo = true,
            Financiavel = true,
            AceitaTroca = false,
            Opcionais = [TipoVeiculoOpcional.ArCondicionado, TipoVeiculoOpcional.FreiosAbs]
        }), CancellationToken.None);

        Assert.True(criar.IsSuccess);
        var veiculoId = criar.Value;

        var atualizarHandler = new AtualizarVeiculoCommandHandler(db);
        var atualizar = await atualizarHandler.Handle(new AtualizarVeiculoCommand(new VeiculoUpdateDto
        {
            Id = veiculoId,
            LojaId = lojaId,
            MarcaId = novaMarca.Id,
            Titulo = "Volkswagen",
            Modelo = "T-Cross",
            Versao = "Highline",
            AnoFabricacao = 2023,
            AnoModelo = 2024,
            Cor = "Preto",
            Combustivel = Combustivel.Flex,
            Cambio = Cambio.Automatico,
            PrecoVenda = 134900m,
            Quilometragem = 9000,
            Placa = "XYZ9988",
            Descricao = "Atualizado no teste",
            Destaque = true,
            Seminovo = true,
            Financiavel = true,
            AceitaTroca = true,
            Opcionais = [TipoVeiculoOpcional.CameraDeRe, TipoVeiculoOpcional.Bluetooth]
        }), CancellationToken.None);

        Assert.True(atualizar.IsSuccess);

        var salvo = await db.Veiculos
            .Include(x => x.Caracteristicas)
            .FirstAsync(x => x.Id == veiculoId);

        Assert.Equal("Highline", salvo.Versao);
        Assert.Equal(2024, salvo.AnoModelo);
        Assert.Equal(novaMarca.Id, salvo.MarcaId);
        Assert.Equal(134900m, salvo.PrecoVenda.Valor);
        Assert.True(salvo.AceitaTroca);

        Assert.NotNull(salvo.Caracteristicas);
        Assert.False(salvo.Caracteristicas!.ArCondicionado);
        Assert.False(salvo.Caracteristicas.FreiosAbs);
        Assert.True(salvo.Caracteristicas.CameraDeRe);
        Assert.True(salvo.Caracteristicas.Bluetooth);
    }

    [Fact]
    public async Task AtualizarVeiculo_DevePreservarCamposComerciaisOmitidosEVenda()
    {
        await using var db = CreateDbContext();
        var (lojaId, marcaId) = await SeedLojaMarcaAsync(db);
        var vehicle = new Veiculo(lojaId, marcaId, "Teste", "Modelo", 2026, new Dinheiro(10000));
        vehicle.AtualizarComercial(true, true, false, true, "https://example.test/video", "Anotacao privada", 42);
        var soldAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        vehicle.MarcarComoVendido(soldAt);
        db.Add(vehicle);
        await db.SaveChangesAsync();
        var result = await new AtualizarVeiculoCommandHandler(db).Handle(new AtualizarVeiculoCommand(new VeiculoUpdateDto
        {
            Id = vehicle.Id, LojaId = lojaId, MarcaId = marcaId, Titulo = "Editado", Modelo = "Modelo", AnoModelo = 2026, PrecoVenda = 12000,
            AceitaTroca = true, Financiavel = true, Seminovo = true
        }), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("https://example.test/video", vehicle.UrlVideo);
        Assert.Equal("Anotacao privada", vehicle.ObservacoesInternas);
        Assert.Equal(42, vehicle.VendedorId);
        Assert.Equal(soldAt, vehicle.DataVenda);
        Assert.True(vehicle.Vendido);
    }

    [Fact]
    public async Task AtualizarVeiculo_DevePermitirLimparCamposExplicitamente()
    {
        await using var db = CreateDbContext();
        var (lojaId, marcaId) = await SeedLojaMarcaAsync(db);
        var vehicle = new Veiculo(lojaId, marcaId, "Teste", "Modelo", 2026, new Dinheiro(10000));
        vehicle.AtualizarComercial(true, true, false, true, "https://example.test/video", "Anotacao", 42);
        db.Add(vehicle);
        await db.SaveChangesAsync();
        var dto = System.Text.Json.JsonSerializer.Deserialize<VeiculoUpdateDto>(
            $$"""{"Id":{{vehicle.Id}},"LojaId":{{lojaId}},"MarcaId":{{marcaId}},"Titulo":"Editado","Modelo":"Modelo","AnoModelo":2026,"PrecoVenda":12000,"UrlVideo":null,"ObservacoesInternas":null,"VendedorId":null}""")!;
        var result = await new AtualizarVeiculoCommandHandler(db).Handle(new AtualizarVeiculoCommand(dto), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Null(vehicle.UrlVideo);
        Assert.Null(vehicle.ObservacoesInternas);
        Assert.Null(vehicle.VendedorId);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"veiculo-tests-{Guid.NewGuid():N}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<(int lojaId, int marcaId)> SeedLojaMarcaAsync(ApplicationDbContext db)
    {
        var loja = new Loja(
            "Loja Teste",
            "Loja Teste LTDA",
            new Documento("12345678000100"),
            new Email("loja@teste.com"),
            new Telefone("16999990000"),
            new Endereco("Rua A", "100", null, "Centro", "Taquaritinga", Uf.SP, "15900000"));

        var marca = new Marca("Marca Teste", null);

        db.Lojas.Add(loja);
        db.Marcas.Add(marca);
        await db.SaveChangesAsync();

        return (loja.Id, marca.Id);
    }
}
