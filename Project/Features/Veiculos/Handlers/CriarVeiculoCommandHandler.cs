using Data;
using Domain.Entities;
using Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Project.Features.Veiculos.Commands;
using Project.Shared;

namespace Project.Features.Veiculos.Handlers;

public sealed class CriarVeiculoCommandHandler(ApplicationDbContext db) : IRequestHandler<CriarVeiculoCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CriarVeiculoCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        var lojaExiste = await db.Lojas.AsNoTracking().AnyAsync(x => x.Id == dto.LojaId, cancellationToken);
        var marcaExiste = await db.Marcas.AsNoTracking().AnyAsync(x => x.Id == dto.MarcaId, cancellationToken);
        if (!lojaExiste || !marcaExiste) return Result<int>.Failure("Loja ou marca nao encontrada.");

        var veiculo = new Veiculo(dto.LojaId, dto.MarcaId, dto.Titulo, dto.Modelo, dto.AnoModelo, new Dinheiro(dto.PrecoVenda));
        veiculo.Update(dto.Titulo, dto.Modelo, dto.Versao, dto.AnoFabricacao, dto.AnoModelo, dto.Combustivel, dto.Cambio, dto.Quilometragem, dto.Placa, dto.Cor, dto.Descricao);
        veiculo.AtualizarComercial(dto.AceitaTroca, dto.Financiavel, dto.Destaque, dto.Seminovo, dto.UrlVideo, dto.ObservacoesInternas, dto.VendedorId);

        db.Veiculos.Add(veiculo);
        await db.SaveChangesAsync(cancellationToken);

        var caracteristicaFinal = new VeiculoCaracteristica(veiculo.Id);
        foreach (var opcional in dto.Opcionais)
        {
            caracteristicaFinal.AdicionarOpcional(opcional);
        }

        db.VeiculoCaracteristicas.Add(caracteristicaFinal);
        await db.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(veiculo.Id);
    }
}
