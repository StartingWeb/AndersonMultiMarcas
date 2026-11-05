using Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Storage;
using Project.Pages.ViewModels;
using Project.Shared;

namespace Project.Pages;

public class ContatoModel(ApplicationDbContext db, IStorageImageResolver imageResolver) : PageModel
{
    public IReadOnlyList<ContactStoreViewModel> Lojas { get; private set; } = [];
    public IReadOnlyList<HomeSellerViewModel> Vendedores { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var lojas = await PublicStoreQueries.GetContactStoresAsync(db, ct);

        var lojasResolvidas = new List<ContactStoreViewModel>();
        foreach (var loja in lojas)
        {
            var vendedoresLoja = new List<ContactSellerViewModel>();
            foreach (var vendedor in loja.Vendedores.Where(v => !string.IsNullOrWhiteSpace(v.Telefone)))
            {
                vendedoresLoja.Add(new ContactSellerViewModel
                {
                    Nome = vendedor.Nome,
                    Telefone = vendedor.Telefone,
                    TelefoneExibicao = vendedor.TelefoneExibicao,
                    Cargo = vendedor.Cargo,
                    FotoUrl = imageResolver.ResolveSellerPhoto(vendedor.FotoUrl)
                });
            }

            lojasResolvidas.Add(new ContactStoreViewModel
            {
                Nome = loja.Nome,
                EnderecoCompleto = loja.EnderecoCompleto,
                Telefone = loja.Telefone,
                TelefoneExibicao = loja.TelefoneExibicao,
                Email = loja.Email,
                MapsQuery = loja.MapsQuery,
                Vendedores = vendedoresLoja
            });
        }

        Lojas = lojasResolvidas;

        var vendedores = await db.Vendedores
            .AsNoTracking()
            .Where(x => x.Ativo)
            .OrderBy(x => x.Nome)
            .Select(x => new HomeSellerViewModel
            {
                Nome = x.Nome,
                Telefone = x.Whatsapp.HasValue ? x.Whatsapp.Value.Valor : (x.Telefone.HasValue ? x.Telefone.Value.Valor : string.Empty),
                FotoUrl = x.FotoUrl
            })
            .ToListAsync(ct);

        var vendedoresResolvidos = new List<HomeSellerViewModel>();
        foreach (var vendedor in vendedores.Where(x => !string.IsNullOrWhiteSpace(x.Telefone)))
        {
            vendedoresResolvidos.Add(new HomeSellerViewModel
            {
                Nome = vendedor.Nome,
                Telefone = vendedor.Telefone,
                FotoUrl = imageResolver.ResolveSellerPhoto(vendedor.FotoUrl)
            });
        }

        Vendedores = vendedoresResolvidos;

        ViewData["SeoTitle"] = "Contato e lojas em Taquaritinga/SP | Anderson Multimarcas";
        ViewData["MetaDescription"] = "Veja contatos, endereco, telefone, e-mail, mapa e vendedores das lojas Anderson Multimarcas em Taquaritinga/SP.";
        ViewData["CanonicalUrl"] = $"{baseUrl}/Contato";
        ViewData["BreadcrumbSchema"] = SeoJsonLd.Breadcrumb(baseUrl, ("Inicio", "/"), ("Contato", "/Contato"));
        ViewData["FaqSchema"] = SeoJsonLd.Faq(
            ("Como falar com a Anderson Multimarcas?", "Voce pode ligar para a loja, chamar um vendedor no WhatsApp ou abrir a rota para atendimento presencial."),
            ("Posso ver os carros presencialmente?", "Sim. A pagina de contato mostra as lojas, mapas e vendedores por unidade para facilitar a visita."));
    }

    private static string BuildEnderecoCompleto(
        string logradouro,
        string numero,
        string? complemento,
        string bairro,
        string cidade,
        string uf,
        string cep)
    {
        var partes = new[]
        {
            logradouro,
            numero,
            complemento,
            bairro,
            $"{cidade} - {uf}",
            cep
        };

        return string.Join(", ", partes.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string FormatPhone(string phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());

        return digits.Length switch
        {
            10 => $"({digits[..2]}) {digits[2..6]}-{digits[6..]}",
            11 => $"({digits[..2]}) {digits[2..7]}-{digits[7..]}",
            12 when digits.StartsWith("55") => $"({digits[2..4]}) {digits[4..8]}-{digits[8..]}",
            13 when digits.StartsWith("55") => $"({digits[2..4]}) {digits[4..9]}-{digits[9..]}",
            _ => digits
        };
    }

}
