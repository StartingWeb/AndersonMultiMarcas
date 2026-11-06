using Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Storage;
using Project.Pages.ViewModels;
using Project.Shared;

namespace Project.Pages;

public class EmpresaModel(ApplicationDbContext db, IStorageImageResolver imageResolver) : PageModel
{
    public int TotalVeiculos { get; private set; }
    public IReadOnlyList<HomeStoreViewModel> Lojas { get; private set; } = [];
    public IReadOnlyList<HomeSellerViewModel> Vendedores { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        TotalVeiculos = await db.Veiculos
            .AsNoTracking()
            .CountAsync(x => x.Ativo && !x.Vendido, ct);

        Lojas = await PublicStoreQueries.GetHomeStoresAsync(db, 3, ct);

        var vendedores = await db.Vendedores
            .AsNoTracking()
            .Where(x => x.Ativo && x.Loja.Ativo)
            .OrderBy(x => x.Nome)
            .Select(x => new SellerProjection
            {
                Nome = x.Nome,
                Telefone = x.Whatsapp.HasValue ? x.Whatsapp.Value.Valor : (x.Telefone.HasValue ? x.Telefone.Value.Valor : string.Empty),
                FotoUrl = x.FotoUrl
            })
            .Take(12)
            .ToListAsync(ct);

        Vendedores = ToSellerViewModels(vendedores);

        ViewData["SeoTitle"] = "Quem somos | Anderson Multimarcas em Taquaritinga/SP";
        ViewData["MetaDescription"] = "Conheça a história da Anderson Multimarcas, nossas lojas em Taquaritinga/SP e o atendimento pensado para gerar confiança em cada negócio.";
        ViewData["CanonicalUrl"] = $"{baseUrl}/Empresa";
        ViewData["BreadcrumbSchema"] = SeoJsonLd.Breadcrumb(baseUrl, ("Início", "/"), ("Quem somos", "/Empresa"));
        ViewData["FaqSchema"] = SeoJsonLd.Faq(
            ("Onde fica a Anderson Multimarcas?", "A Anderson Multimarcas atende em Taquaritinga/SP e região, com unidades presenciais e vendedores especializados."),
            ("A loja trabalha com quais tipos de veículo?", "O estoque inclui carros seminovos, opções 0 km, modelos eletrificados e motos elétricas conforme disponibilidade."));
    }

    private IReadOnlyList<HomeSellerViewModel> ToSellerViewModels(IEnumerable<SellerProjection> sellers)
    {
        var result = new List<HomeSellerViewModel>();
        foreach (var seller in sellers)
        {
            result.Add(new HomeSellerViewModel
            {
                Nome = seller.Nome,
                Telefone = seller.Telefone,
                FotoUrl = imageResolver.ResolveSellerPhoto(seller.FotoUrl)
            });
        }

        return result;
    }

    private sealed class SellerProjection
    {
        public string Nome { get; init; } = string.Empty;
        public string Telefone { get; init; } = string.Empty;
        public string? FotoUrl { get; init; }
    }
}
