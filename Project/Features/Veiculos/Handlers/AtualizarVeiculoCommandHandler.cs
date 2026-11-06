using Data;
using Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Project.Features.Veiculos.Commands;
using Project.Shared;

namespace Project.Features.Veiculos.Handlers;

public sealed class AtualizarVeiculoCommandHandler(ApplicationDbContext db) : IRequestHandler<AtualizarVeiculoCommand, Result>
{
    public async Task<Result> Handle(AtualizarVeiculoCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        var veiculo = await db.Veiculos.Include(x => x.Caracteristicas).FirstOrDefaultAsync(x => x.Id == dto.Id, cancellationToken);
        if (veiculo is null) return Result.Failure("Veiculo nao encontrado.");

        veiculo.Update(dto.Titulo, dto.Modelo, dto.Versao, dto.AnoFabricacao, dto.AnoModelo, dto.Combustivel, dto.Cambio, dto.Quilometragem, dto.Placa, dto.Cor, dto.Descricao);
        veiculo.AtualizarVinculos(dto.LojaId, dto.MarcaId);
        veiculo.AtualizarPreco(new Dinheiro(dto.PrecoVenda));
        veiculo.AtualizarComercial(dto.AceitaTroca, dto.Financiavel, dto.Destaque, dto.Seminovo,
            dto.UrlVideoSpecified ? dto.UrlVideo : veiculo.UrlVideo,
            dto.ObservacoesInternasSpecified ? dto.ObservacoesInternas : veiculo.ObservacoesInternas,
            dto.VendedorIdSpecified ? dto.VendedorId : veiculo.VendedorId);

        var caracteristicas = veiculo.Caracteristicas;
        if (caracteristicas is null)
        {
            caracteristicas = new Domain.Entities.VeiculoCaracteristica(veiculo.Id);
            db.VeiculoCaracteristicas.Add(caracteristicas);
        }

        foreach (var opcional in Enum.GetValues<Domain.Enums.TipoVeiculoOpcional>())
        {
            caracteristicas.RemoverOpcional(opcional);
        }
        foreach (var opcional in dto.Opcionais)
        {
            caracteristicas.AdicionarOpcional(opcional);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
