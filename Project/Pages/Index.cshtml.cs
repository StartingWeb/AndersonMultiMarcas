using Data;
using Core.Storage;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Storage;
using Project.Pages.ViewModels;
using Project.Shared;

namespace Project.Pages;

public class IndexModel(ApplicationDbContext db, IStorageImageResolver imageResolver) : PageModel
{
    public IReadOnlyCollection<HomeVehicleCardViewModel> HeroDestaques { get; private set; } = [];
    public IReadOnlyCollection<HomeVehicleCardViewModel> PremiumZeroKm { get; private set; } = [];
    public IReadOnlyCollection<HomeVehicleCardViewModel> Eletrificados { get; private set; } = [];
    public IReadOnlyCollection<HomeVehicleCardViewModel> MotosEletricas { get; private set; } = [];
    public IReadOnlyCollection<HomeStoreViewModel> Lojas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var query = db.Veiculos
            .AsNoTracking()
            .Where(x => x.Ativo && !x.Vendido)
            .Select(x => new VehicleCardProjection
            {
                Id = x.Id,
                MarcaNome = x.Marca.Nome,
                Modelo = x.Modelo,
                Versao = x.Versao,
                Cor = x.Cor,
                AnoFabricacao = x.AnoFabricacao,
                AnoModelo = x.AnoModelo,
                Cambio = x.Cambio.ToString(),
                Combustivel = x.Combustivel.ToString(),
                Destaque = x.Destaque,
                Seminovo = x.Seminovo,
                Disponivel = !x.Vendido,
                Preco = x.PrecoVenda.Valor,
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
                    .ToList()
            });

        HeroDestaques = ToHomeVehicleCards(
            await query.Where(x => x.Destaque).OrderByDescending(x => x.Id).Take(12).ToListAsync(ct));
        PremiumZeroKm = ToHomeVehicleCards(
            await query.Where(x => !x.Seminovo).OrderByDescending(x => x.Id).Take(4).ToListAsync(ct));
        Eletrificados = ToHomeVehicleCards(
            await db.Veiculos.AsNoTracking()
            .Where(x => x.Ativo && !x.Vendido && (x.Combustivel == Combustivel.Eletrico || x.Combustivel == Combustivel.Hibrido))
            .Select(x => new VehicleCardProjection
            {
                Id = x.Id,
                MarcaNome = x.Marca.Nome,
                Modelo = x.Modelo,
                Versao = x.Versao,
                Cor = x.Cor,
                AnoFabricacao = x.AnoFabricacao,
                AnoModelo = x.AnoModelo,
                Cambio = x.Cambio.ToString(),
                Combustivel = x.Combustivel.ToString(),
                Destaque = x.Destaque,
                Seminovo = x.Seminovo,
                Disponivel = !x.Vendido,
                Preco = x.PrecoVenda.Valor,
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
                    .ToList()
            })
            .OrderByDescending(x => x.Id)
            .Take(4)
            .ToListAsync(ct));
        MotosEletricas = ToHomeVehicleCards(
            await db.Veiculos.AsNoTracking()
            .Where(x => x.Ativo && !x.Vendido && x.MotoEletrica)
            .Select(x => new VehicleCardProjection
            {
                Id = x.Id,
                MarcaNome = x.Marca.Nome,
                Modelo = x.Modelo,
                Versao = x.Versao,
                Cor = x.Cor,
                AnoFabricacao = x.AnoFabricacao,
                AnoModelo = x.AnoModelo,
                Cambio = x.Cambio.ToString(),
                Combustivel = x.Combustivel.ToString(),
                Destaque = x.Destaque,
                Seminovo = x.Seminovo,
                Disponivel = !x.Vendido,
                Preco = x.PrecoVenda.Valor,
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
                    .ToList()
            })
            .OrderByDescending(x => x.Id)
            .Take(4)
            .ToListAsync(ct));

        Lojas = await PublicStoreQueries.GetHomeStoresAsync(db, 3, ct);

        ViewData["SeoTitle"] = "Carros seminovos e 0 km em Taquaritinga/SP | Anderson Multimarcas";
        ViewData["MetaDescription"] = "Compre carros seminovos, 0 km, híbridos, elétricos e motos elétricas em Taquaritinga/SP com atendimento consultivo, troca e financiamento.";
        ViewData["CanonicalUrl"] = $"{baseUrl}/";
        ViewData["BreadcrumbSchema"] = SeoJsonLd.Breadcrumb(baseUrl, ("Início", "/"));
        ViewData["FaqSchema"] = SeoJsonLd.Faq(
            ("Quais veículos encontro no estoque?", "Você encontra veículos seminovos, 0 km, híbridos, elétricos e motos elétricas."),
            ("Posso trocar meu carro?", "Sim. Fale com um vendedor para avaliação e proposta de troca."),
            ("A loja atende em Taquaritinga/SP?", "Sim. A Anderson Multimarcas atende Taquaritinga e região com lojas e vendedores locais."));
    }

    public async Task<IActionResult> OnGetSearchSuggestionsAsync([FromQuery] string? term, CancellationToken ct)
    {
        var query = (term ?? string.Empty).Trim();
        if (query.Length < 2)
        {
            return new JsonResult(new { groups = Array.Empty<object>() });
        }

        var marcas = await db.Veiculos
            .AsNoTracking()
            .Where(x => x.Ativo && !x.Vendido && EF.Functions.Like(x.Marca.Nome, $"%{query}%"))
            .Select(x => x.Marca.Nome)
            .Distinct()
            .OrderBy(x => x)
            .Take(5)
            .ToListAsync(ct);

        var modelos = await db.Veiculos
            .AsNoTracking()
            .Where(x => x.Ativo && !x.Vendido && (EF.Functions.Like(x.Titulo, $"%{query}%") || EF.Functions.Like(x.Modelo, $"%{query}%")))
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                Label = (x.Titulo ?? string.Empty) + " " + (x.Modelo ?? string.Empty),
                MarcaNome = x.Marca.Nome,
                x.AnoModelo
            })
            .Take(7)
            .ToListAsync(ct);

        var groups = new List<object>();

        if (marcas.Count > 0)
        {
            groups.Add(new
            {
                title = "Marca",
                items = marcas.Select(m => new
                {
                    label = m,
                    meta = "Filtrar veículos por marca",
                    url = $"/veiculos?marca={Uri.EscapeDataString(m)}"
                })
            });
        }

        if (modelos.Count > 0)
        {
            groups.Add(new
            {
                title = "Nome",
                items = modelos.Select(m => new
                {
                    label = m.Label.Trim(),
                    meta = string.IsNullOrWhiteSpace(m.MarcaNome) ? $"Ano {m.AnoModelo}" : $"{m.MarcaNome} - {m.AnoModelo}",
                    url = $"/veiculos?busca={Uri.EscapeDataString(m.Label.Trim())}"
                })
            });
        }

        return new JsonResult(new { groups });
    }

    private IReadOnlyList<HomeVehicleCardViewModel> ToHomeVehicleCards(IEnumerable<VehicleCardProjection> vehicles)
    {
        var result = new List<HomeVehicleCardViewModel>();
        foreach (var vehicle in vehicles)
        {
            result.Add(new HomeVehicleCardViewModel
            {
                Id = vehicle.Id,
                Titulo = VehicleTitleHelper.Format(vehicle.MarcaNome, vehicle.Modelo, vehicle.Versao),
                Cor = vehicle.Cor,
                AnoFabricacao = vehicle.AnoFabricacao,
                AnoModelo = vehicle.AnoModelo,
                Cambio = vehicle.Cambio,
                Combustivel = vehicle.Combustivel,
                Destaque = vehicle.Destaque,
                Seminovo = vehicle.Seminovo,
                Disponivel = vehicle.Disponivel,
                Preco = vehicle.Preco,
                MidiaUrl = imageResolver.SelectVehicleCover(vehicle.Midias.Select(ToStorageReference))
            });
        }

        return result;
    }

    private static StorageImageReference ToStorageReference(MediaProjection media)
        => new(media.Url, media.BlobName, media.Container, media.NomeArquivo, media.ContentType, media.TamanhoBytes);

    private sealed class MediaProjection
    {
        public string? Url { get; init; }
        public string? BlobName { get; init; }
        public string? Container { get; init; }
        public string? NomeArquivo { get; init; }
        public string? ContentType { get; init; }
        public long? TamanhoBytes { get; init; }
    }

    private sealed class VehicleCardProjection
    {
        public int Id { get; init; }
        public string MarcaNome { get; init; } = string.Empty;
        public string? Modelo { get; init; }
        public string? Versao { get; init; }
        public string? Cor { get; init; }
        public int? AnoFabricacao { get; init; }
        public int AnoModelo { get; init; }
        public string Cambio { get; init; } = string.Empty;
        public string Combustivel { get; init; } = string.Empty;
        public bool Destaque { get; init; }
        public bool Seminovo { get; init; }
        public bool Disponivel { get; init; }
        public decimal Preco { get; init; }
        public IReadOnlyList<MediaProjection> Midias { get; init; } = [];
    }
}
