using Core.Storage;
using Data;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Project.Features.Veiculos.DTOs;
using Project.Features.Veiculos.Queries;
using Project.Features.Veiculos.Services;
using Project.Infrastructure.Storage;
using Project.Shared;

namespace Project.Features.Veiculos.Handlers;

public sealed class BuscarVeiculosQueryHandler(
    ApplicationDbContext db,
    IVeiculoSlugService slugService,
    IStorageImageResolver imageResolver) : IRequestHandler<BuscarVeiculosQuery, Result<PagedResult<VeiculoListItemDto>>>
{
    public async Task<Result<PagedResult<VeiculoListItemDto>>> Handle(BuscarVeiculosQuery request, CancellationToken cancellationToken)
    {
        var filtro = request.Filtro;

        var query = db.Veiculos
            .AsNoTracking()
            .Where(x => x.Ativo && !x.Vendido)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var term = filtro.Busca.Trim();
            query = query.Where(x => x.Titulo.Contains(term) || x.Modelo.Contains(term) || (x.Versao != null && x.Versao.Contains(term)));
        }

        if (filtro.MarcaId.HasValue) query = query.Where(x => x.MarcaId == filtro.MarcaId.Value);
        if (!string.IsNullOrWhiteSpace(filtro.Marca)) query = query.Where(x => x.Marca.Nome == filtro.Marca);
        if (!string.IsNullOrWhiteSpace(filtro.Modelo)) query = query.Where(x => x.Modelo.Contains(filtro.Modelo));
        if (filtro.AnoModelo.HasValue) query = query.Where(x => x.AnoModelo == filtro.AnoModelo.Value);
        if (filtro.AnoMinimo.HasValue) query = query.Where(x => x.AnoModelo >= filtro.AnoMinimo.Value);
        if (filtro.AnoMaximo.HasValue) query = query.Where(x => x.AnoModelo <= filtro.AnoMaximo.Value);
        if (filtro.PrecoMinimo.HasValue && filtro.PrecoMaximo.HasValue)
        {
            var min = filtro.PrecoMinimo.Value;
            var max = filtro.PrecoMaximo.Value;
            var ids = db.Database.SqlQuery<int>($"SELECT [Id] AS [Value] FROM [Veiculo] WHERE [PrecoVenda] >= {min} AND [PrecoVenda] <= {max}");
            query = query.Where(x => ids.Contains(x.Id));
        }
        else if (filtro.PrecoMinimo.HasValue)
        {
            var min = filtro.PrecoMinimo.Value;
            var ids = db.Database.SqlQuery<int>($"SELECT [Id] AS [Value] FROM [Veiculo] WHERE [PrecoVenda] >= {min}");
            query = query.Where(x => ids.Contains(x.Id));
        }
        else if (filtro.PrecoMaximo.HasValue)
        {
            var max = filtro.PrecoMaximo.Value;
            var ids = db.Database.SqlQuery<int>($"SELECT [Id] AS [Value] FROM [Veiculo] WHERE [PrecoVenda] <= {max}");
            query = query.Where(x => ids.Contains(x.Id));
        }
        if (filtro.Combustivel.HasValue) query = query.Where(x => x.Combustivel == filtro.Combustivel.Value);
        if (filtro.Cambio.HasValue) query = query.Where(x => x.Cambio == filtro.Cambio.Value);
        if (filtro.Destaque.HasValue) query = query.Where(x => x.Destaque == filtro.Destaque.Value);
        if (filtro.Disponivel == false) query = query.Where(x => false);
        if (filtro.Seminovo.HasValue) query = query.Where(x => x.Seminovo == filtro.Seminovo.Value);
        if (filtro.Financiavel.HasValue) query = query.Where(x => x.Financiavel == filtro.Financiavel.Value);
        if (filtro.AceitaTroca.HasValue) query = query.Where(x => x.AceitaTroca == filtro.AceitaTroca.Value);

        query = filtro.OrdenarPor.ToLowerInvariant() switch
        {
            "destaque" => query.OrderByDescending(x => x.Destaque).ThenByDescending(x => x.DataCadastro).ThenBy(x => x.Id),
            "preco-asc" => query.OrderBy(x => x.PrecoVenda).ThenBy(x => x.Id),
            "preco-desc" => query.OrderByDescending(x => x.PrecoVenda).ThenBy(x => x.Id),
            "ano-desc" => query.OrderByDescending(x => x.AnoModelo).ThenBy(x => x.Id),
            "ano-asc" => query.OrderBy(x => x.AnoModelo).ThenBy(x => x.Id),
            _ => query.OrderByDescending(x => x.DataCadastro).ThenBy(x => x.Id)
        };

        var totalItems = await query.CountAsync(cancellationToken);
        var projections = await query.Skip(filtro.Skip).Take(filtro.PageSize)
            .Select(x => new VeiculoListItemProjection
            {
                Id = x.Id,
                Slug = slugService.CriarSlug(x.Titulo, x.Modelo, x.Versao, x.Id),
                Titulo = x.Titulo,
                Modelo = x.Modelo,
                Versao = x.Versao,
                AnoFabricacao = x.AnoFabricacao,
                AnoModelo = x.AnoModelo,
                Cor = x.Cor,
                Combustivel = x.Combustivel,
                Cambio = x.Cambio,
                PrecoVenda = x.PrecoVenda.Valor,
                Destaque = x.Destaque,
                Seminovo = x.Seminovo,
                EstaDisponivel = x.Ativo && !x.Vendido,
                Midias = x.Midias
                    .Where(m => m.Ativo && m.Tipo == TipoMidia.Imagem)
                    .OrderByDescending(m => m.Capa)
                    .ThenBy(m => m.Ordem)
                    .ThenBy(m => m.Id)
                    .Select(m => new MediaProjection
                    {
                        Url = m.Url,
                        BlobName = m.BlobName,
                        Container = m.Container,
                        NomeArquivo = m.NomeArquivo,
                        ContentType = m.ContentType,
                        TamanhoBytes = m.TamanhoBytes
                    })
                    .ToList(),
                MarcaNome = x.Marca.Nome,
                LojaNome = x.Loja.Nome
            })
            .ToListAsync(cancellationToken);

        var items = new List<VeiculoListItemDto>();
        foreach (var x in projections)
        {
            items.Add(new VeiculoListItemDto
            {
                Id = x.Id,
                Slug = x.Slug,
                Titulo = x.Titulo,
                Modelo = x.Modelo,
                Versao = x.Versao,
                AnoFabricacao = x.AnoFabricacao,
                AnoModelo = x.AnoModelo,
                Cor = x.Cor,
                Combustivel = x.Combustivel,
                Cambio = x.Cambio,
                PrecoVenda = x.PrecoVenda,
                Destaque = x.Destaque,
                Seminovo = x.Seminovo,
                EstaDisponivel = x.EstaDisponivel,
                MidiaCapaUrl = imageResolver.SelectVehicleCover(x.Midias.Select(ToStorageReference)),
                MarcaNome = x.MarcaNome,
                LojaNome = x.LojaNome
            });
        }

        var result = new PagedResult<VeiculoListItemDto>
        {
            Items = items,
            Page = filtro.Page,
            PageSize = filtro.PageSize,
            TotalItems = totalItems
        };

        return Result<PagedResult<VeiculoListItemDto>>.Success(result);
    }

    private sealed class VeiculoListItemProjection
    {
        public int Id { get; init; }
        public string Slug { get; init; } = string.Empty;
        public string Titulo { get; init; } = string.Empty;
        public string Modelo { get; init; } = string.Empty;
        public string? Versao { get; init; }
        public int? AnoFabricacao { get; init; }
        public int AnoModelo { get; init; }
        public string? Cor { get; init; }
        public Combustivel Combustivel { get; init; }
        public Cambio Cambio { get; init; }
        public decimal PrecoVenda { get; init; }
        public bool Destaque { get; init; }
        public bool Seminovo { get; init; }
        public bool EstaDisponivel { get; init; }
        public IReadOnlyList<MediaProjection> Midias { get; init; } = [];
        public string MarcaNome { get; init; } = string.Empty;
        public string LojaNome { get; init; } = string.Empty;
    }

    private sealed class MediaProjection
    {
        public string? Url { get; init; }
        public string? BlobName { get; init; }
        public string? Container { get; init; }
        public string? NomeArquivo { get; init; }
        public string? ContentType { get; init; }
        public long? TamanhoBytes { get; init; }
    }

    private static StorageImageReference ToStorageReference(MediaProjection media)
        => new(media.Url, media.BlobName, media.Container, media.NomeArquivo, media.ContentType, media.TamanhoBytes);
}
