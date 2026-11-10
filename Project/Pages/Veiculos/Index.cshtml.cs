using Data;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project.Features.Veiculos.DTOs;
using Project.Features.Veiculos.Queries;
using Project.Features.Veiculos.Queries.Facets;
using Project.Infrastructure.Storage;
using Project.Pages.ViewModels;
using Project.Pages.Veiculos.ViewModels;
using Project.Shared;

namespace Project.Pages.Veiculos;

public class IndexModel(ISender sender, ApplicationDbContext db, IStorageImageResolver imageResolver) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Segmento { get; set; }
    [BindProperty(SupportsGet = true)] public string? Busca { get; set; }
    [BindProperty(SupportsGet = true)] public string? Marca { get; set; }
    [BindProperty(SupportsGet = true)] public string? Condicao { get; set; }
    [BindProperty(SupportsGet = true)] public string? Modelo { get; set; }
    [BindProperty(SupportsGet = true)] public int? AnoMinimo { get; set; }
    [BindProperty(SupportsGet = true)] public int? AnoMaximo { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? PrecoMinimo { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? PrecoMaximo { get; set; }
    [BindProperty(SupportsGet = true)] public Combustivel? Combustivel { get; set; }
    [BindProperty(SupportsGet = true)] public Cambio? Cambio { get; set; }
    [BindProperty(SupportsGet = true)] public bool? Destaque { get; set; }
    [BindProperty(SupportsGet = true)] public bool? Disponivel { get; set; }
    [BindProperty(SupportsGet = true)] public string OrdenarPor { get; set; } = "recentes";
    [BindProperty(Name = "page", SupportsGet = true)] public int PaginaAtual { get; set; } = 1;

    public CatalogoPageViewModel Vm { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        PaginaAtual = ReadCurrentPage();

        var anoMinimo = AnoMinimo;
        var anoMaximo = AnoMaximo;
        var busca = Busca;
        var combustivel = Combustivel;
        var cambio = Cambio;
        var precoMaximo = PrecoMaximo;
        bool? seminovo = null;
        bool? financiavel = null;
        bool? aceitaTroca = null;
        var segmento = NormalizeSegment(Segmento);
        var landing = BuildLandingContent(segmento);

        if (segmento == "zero-km")
        {
            Condicao = "zerokm";
        }
        else if (segmento is "seminovos" or "seminovos-taquaritinga")
        {
            Condicao = "seminovo";
            seminovo = true;
        }
        else if (segmento == "hibridos")
        {
            combustivel ??= Domain.Enums.Combustivel.Hibrido;
        }
        else if (segmento == "eletricos")
        {
            combustivel ??= Domain.Enums.Combustivel.Eletrico;
        }
        else if (segmento == "motos-eletricas")
        {
            busca = string.IsNullOrWhiteSpace(busca) ? "moto eletrica" : busca;
        }
        else if (segmento == "carros-automaticos-taquaritinga")
        {
            cambio ??= Domain.Enums.Cambio.Automatico;
        }
        else if (segmento == "suvs-seminovos-taquaritinga")
        {
            Condicao = "seminovo";
            seminovo = true;
            busca = string.IsNullOrWhiteSpace(busca) ? "suv" : busca;
        }
        else if (segmento == "carros-ate-50-mil")
        {
            precoMaximo ??= 50000;
        }
        else if (segmento == "financiamento")
        {
            financiavel = true;
        }
        else if (segmento == "troca-de-veiculos")
        {
            aceitaTroca = true;
        }

        if (string.Equals(Condicao, "zerokm", StringComparison.OrdinalIgnoreCase))
        {
            seminovo = false;
        }
        else if (string.Equals(Condicao, "seminovo", StringComparison.OrdinalIgnoreCase))
        {
            seminovo = true;
        }

        var filtro = new BuscarVeiculosFiltroDto
        {
            Busca = busca,
            Marca = Marca,
            Modelo = Modelo,
            AnoMinimo = anoMinimo,
            AnoMaximo = anoMaximo,
            PrecoMinimo = PrecoMinimo,
            PrecoMaximo = precoMaximo,
            Combustivel = combustivel,
            Cambio = cambio,
            Destaque = Destaque,
            Disponivel = Disponivel,
            Seminovo = seminovo,
            Financiavel = financiavel,
            AceitaTroca = aceitaTroca,
            OrdenarPor = string.IsNullOrWhiteSpace(OrdenarPor) ? "recentes" : OrdenarPor,
            Page = PaginaAtual <= 0 ? 1 : PaginaAtual,
            PageSize = 36
        };

        var listaResult = await sender.Send(new BuscarVeiculosQuery(filtro), ct);
        var facetasResult = await sender.Send(new ObterCatalogoFacetasQuery(), ct);
        var pageResult = listaResult.Value;
        var currentPage = pageResult?.Page > 0 ? pageResult.Page : filtro.Page;

        PaginaAtual = currentPage;
        var items = pageResult?.Items?.ToList() ?? [];
        var destaquesRecentes = string.Equals(filtro.OrdenarPor, "recentes", StringComparison.OrdinalIgnoreCase)
            ? items.OrderByDescending(x => x.Destaque).ThenByDescending(x => x.Id).Take(3).ToList()
            : [];
        var featuredIds = destaquesRecentes.Select(x => x.Id).ToHashSet();
        var outros = items.Where(x => !featuredIds.Contains(x.Id)).ToList();

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

        Vm = new CatalogoPageViewModel
        {
            Filtro = filtro,
            CondicaoSelecionada = Condicao,
            Vendedores = ToSellerViewModels(vendedores),
            DestaquesRecentes = destaquesRecentes,
            OutrosVeiculos = outros,
            Marcas = facetasResult.Value?.Marcas ?? [],
            Modelos = facetasResult.Value?.Modelos ?? [],
            Anos = facetasResult.Value?.Anos ?? [],
            TotalItems = pageResult?.TotalItems ?? 0,
            CurrentPage = currentPage,
            TotalPages = pageResult?.TotalPages ?? 0,
            HeaderKicker = landing?.Title ?? GetSegmentTitle(segmento),
            HeaderTitle = landing?.Title ?? GetSegmentTitle(segmento),
            HeaderSubtitle = landing is null
                ? "Pesquise por modelo, marca ou oportunidade e refine o restante nos filtros ao lado."
                : "Veja opcoes do estoque e fale com a equipe para confirmar disponibilidade, troca ou financiamento.",
            SeoLanding = landing
        };

        ConfigureSeo(segmento, landing);
    }

    private void ConfigureSeo(string? segmento, SeoLandingContentViewModel? landing)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var path = segmento is null ? "/veiculos" : $"/veiculos/{segmento}";
        var canonicalQuery = QueryString.Create(Request.Query.Where(x => x.Key != "page" && x.Key != "Segmento"));
        var canonical = canonicalQuery.HasValue ? $"{baseUrl}{path}{canonicalQuery}" : $"{baseUrl}{path}";
        var segmentTitle = landing?.Title ?? GetSegmentTitle(segmento);

        ViewData["SeoTitle"] = landing is null
            ? $"{segmentTitle} em Taquaritinga/SP ({Vm.TotalItems}) | Anderson Multimarcas"
            : $"{landing.Title} | Anderson Multimarcas";
        ViewData["MetaDescription"] = GetSegmentDescription(segmento, Vm.TotalItems);
        ViewData["CanonicalUrl"] = canonical;
        ViewData["Robots"] = "index,follow";
        ViewData["BreadcrumbSchema"] = SeoJsonLd.Breadcrumb(baseUrl, ("Inicio", "/"), ("Veiculos", "/veiculos"), (segmentTitle, canonical));

        var visibleFaqs = landing?.Faqs ?? [
            new SeoLandingFaqViewModel("Posso financiar os veiculos?", "Sim. Consulte as opcoes de financiamento com nossa equipe."),
            new SeoLandingFaqViewModel("Voces aceitam troca?", "Sim. Avaliamos seu usado e apresentamos proposta de troca."),
            new SeoLandingFaqViewModel("Os veiculos tem procedencia?", "Trabalhamos com veiculos selecionados e avaliacao tecnica antes da oferta.")
        ];

        ViewData["FaqSchema"] = SeoJsonLd.Faq(visibleFaqs.Select(x => (x.Question, x.Answer)).ToArray());

        ViewData["ItemListSchema"] = SeoJsonLd.ItemList(Vm.TotalItems,
            Vm.DestaquesRecentes.Concat(Vm.OutrosVeiculos).Select(x => (x.Id, x.Titulo)), baseUrl);
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

    private static string? NormalizeSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return null;
        }

        return segment.Trim().ToLowerInvariant() switch
        {
            "0km" or "0-km" or "zero" or "zero-km" => "zero-km",
            "seminovo" or "seminovos" or "usados" => "seminovos",
            "hibrido" or "hibridos" => "hibridos",
            "eletrico" or "eletricos" => "eletricos",
            "moto-eletrica" or "motos-eletricas" => "motos-eletricas",
            "taquaritinga" or "taquaritinga-sp" => "taquaritinga",
            "seminovos-taquaritinga" => "seminovos-taquaritinga",
            "carros-automaticos-taquaritinga" => "carros-automaticos-taquaritinga",
            "suvs-seminovos-taquaritinga" => "suvs-seminovos-taquaritinga",
            "carros-ate-50-mil" => "carros-ate-50-mil",
            "financiamento" => "financiamento",
            "troca-de-veiculos" => "troca-de-veiculos",
            _ => null
        };
    }

    private int ReadCurrentPage()
    {
        var pageValue = Request.Query.TryGetValue("page", out var lowerPage)
            ? lowerPage.ToString()
            : Request.Query.TryGetValue("Page", out var upperPage)
                ? upperPage.ToString()
                : null;

        return int.TryParse(pageValue, out var page) && page > 0 ? page : 1;
    }

    private static SeoLandingContentViewModel? BuildLandingContent(string? segment) => segment switch
    {
        "seminovos-taquaritinga" => new()
        {
            Slug = segment,
            Title = "Carros seminovos em Taquaritinga",
            IntroTitle = "Seminovos selecionados para comprar com mais seguranca",
            Paragraphs =
            [
                "Esta selecao reune veiculos seminovos disponiveis para quem quer comprar em Taquaritinga e prefere comparar modelos, anos, cambios e faixas de preco antes de falar com um vendedor.",
                "A disponibilidade pode mudar ao longo do dia. Se algum modelo chamar sua atencao, nossa equipe confirma detalhes, aceita troca quando aplicavel e orienta os proximos passos."
            ],
            Links = DefaultLinks("carros-automaticos-taquaritinga", "Carros automaticos", "troca-de-veiculos", "Troca de veiculos"),
            Faqs =
            [
                new("Como escolher um seminovo em Taquaritinga?", "Compare ano, quilometragem, historico, estado geral e custo de uso. A equipe pode ajudar a filtrar opcoes conforme seu perfil."),
                new("A loja aceita meu usado na troca?", "Alguns veiculos aceitam troca. O ideal e falar com um vendedor para avaliar seu carro e montar uma proposta."),
                new("Posso simular financiamento de um seminovo?", "Sim. A equipe orienta sobre alternativas de financiamento conforme o veiculo escolhido e seu cadastro.")
            ]
        },
        "carros-automaticos-taquaritinga" => new()
        {
            Slug = segment,
            Title = "Carros automaticos em Taquaritinga",
            IntroTitle = "Opcoes automaticas para dirigir com mais conforto",
            Paragraphs =
            [
                "Aqui voce encontra veiculos com cambio automatico para uso urbano, estrada ou rotina familiar, mantendo a busca simples dentro do estoque da Anderson Multimarcas.",
                "Use os filtros para ajustar marca, ano e preco. Para confirmar itens, disponibilidade e condicoes comerciais, fale com a equipe."
            ],
            Links = DefaultLinks("seminovos-taquaritinga", "Seminovos", "financiamento", "Financiamento"),
            Faqs =
            [
                new("Carro automatico costuma consumir mais?", "Depende do modelo, motor e uso. Veiculos mais novos podem ter cambio eficiente e bom consumo no dia a dia."),
                new("Posso fazer test drive?", "Consulte a equipe para verificar disponibilidade do veiculo e agendar atendimento."),
                new("Ha carros automaticos financiaveis?", "Quando o veiculo e elegivel, a equipe pode orientar a simulacao de financiamento.")
            ]
        },
        "suvs-seminovos-taquaritinga" => new()
        {
            Slug = segment,
            Title = "SUVs seminovos em Taquaritinga",
            IntroTitle = "SUVs para familia, estrada e rotina urbana",
            Paragraphs =
            [
                "Esta pagina destaca SUVs seminovos para quem procura posicao de dirigir elevada, bom espaco interno e versatilidade para diferentes rotinas.",
                "Quando nao houver um SUV exato no estoque, nossa equipe pode indicar modelos proximos e avisar sobre novas entradas."
            ],
            Links = DefaultLinks("seminovos-taquaritinga", "Seminovos", "carros-automaticos-taquaritinga", "Automaticos"),
            Faqs =
            [
                new("O que avaliar em um SUV seminovo?", "Observe espaco interno, pneus, revisoes, consumo, porta-malas e equipamentos de seguranca."),
                new("SUV seminovo aceita troca?", "A possibilidade de troca depende do veiculo e da avaliacao do usado oferecido."),
                new("Posso receber aviso de novas entradas?", "Sim. Fale com a equipe e informe o tipo de SUV que voce procura.")
            ]
        },
        "carros-ate-50-mil" => new()
        {
            Slug = segment,
            Title = "Carros ate 50 mil",
            IntroTitle = "Modelos para comprar com orcamento definido",
            Paragraphs =
            [
                "Esta selecao ajuda quem quer pesquisar carros ate R$ 50 mil sem perder tempo com opcoes fora do orcamento inicial.",
                "Os valores podem variar conforme disponibilidade e negociacao. Confirme preco, condicao e possibilidade de troca antes de visitar a loja."
            ],
            Links = DefaultLinks("seminovos-taquaritinga", "Seminovos", "financiamento", "Financiamento"),
            Faqs =
            [
                new("O preco anunciado pode mudar?", "Pode haver alteracoes por atualizacao de estoque, promocao ou negociacao. Confirme sempre com a equipe."),
                new("Ha carros ate 50 mil com financiamento?", "Quando o veiculo e financiavel, a equipe pode orientar a simulacao."),
                new("Consigo usar meu carro como entrada?", "Em muitos casos e possivel avaliar o usado para composicao da proposta.")
            ]
        },
        "financiamento" => new()
        {
            Slug = segment,
            Title = "Financiamento de veiculos em Taquaritinga",
            IntroTitle = "Atendimento para avaliar opcoes de financiamento",
            Paragraphs =
            [
                "A pagina reune veiculos que podem se encaixar em uma conversa sobre financiamento, com filtros para comparar ano, preco e caracteristicas.",
                "A aprovacao e as condicoes dependem de analise cadastral e da instituicao financeira. A equipe ajuda a organizar as informacoes para a simulacao."
            ],
            Links = DefaultLinks("carros-ate-50-mil", "Carros ate 50 mil", "troca-de-veiculos", "Troca de veiculos"),
            Faqs =
            [
                new("O financiamento e aprovado na hora?", "A aprovacao depende de analise cadastral. A equipe orienta a simulacao e os documentos necessarios."),
                new("Posso financiar com entrada?", "Sim. A entrada pode ajudar a ajustar parcelas, conforme a proposta e a analise."),
                new("Consigo financiar um seminovo?", "Sim, quando o veiculo e elegivel e a analise cadastral e aprovada.")
            ]
        },
        "troca-de-veiculos" => new()
        {
            Slug = segment,
            Title = "Troca de carro usado em Taquaritinga",
            IntroTitle = "Use seu usado em uma nova negociacao",
            Paragraphs =
            [
                "Se voce pretende trocar de carro, esta selecao destaca oportunidades em que a conversa sobre avaliacao do usado pode fazer parte da negociacao.",
                "A avaliacao considera estado, documentacao, mercado e interesse comercial. Para uma proposta real, fale com um vendedor e envie os dados do veiculo."
            ],
            Links = DefaultLinks("seminovos-taquaritinga", "Seminovos", "financiamento", "Financiamento"),
            Faqs =
            [
                new("Como funciona a avaliacao do meu usado?", "A equipe analisa informacoes do veiculo, estado geral, documentacao e referencias de mercado."),
                new("Posso trocar por um carro de maior valor?", "Sim. O usado pode compor a negociacao e a diferenca pode ser ajustada conforme proposta."),
                new("Preciso levar o carro ate a loja?", "Para uma avaliacao precisa, a equipe pode orientar o melhor formato de atendimento.")
            ]
        },
        _ => null
    };

    private static IReadOnlyList<SeoLandingLinkViewModel> DefaultLinks(string relatedSlugOne, string relatedLabelOne, string relatedSlugTwo, string relatedLabelTwo) =>
    [
        new("Ver catalogo completo", "/veiculos"),
        new("Falar no WhatsApp", "#"),
        new("Entrar em contato", "/Contato"),
        new(relatedLabelOne, $"/veiculos/{relatedSlugOne}"),
        new(relatedLabelTwo, $"/veiculos/{relatedSlugTwo}")
    ];

    private static string GetSegmentTitle(string? segment) => segment switch
    {
        "zero-km" => "Carros 0 km",
        "seminovos" => "Carros seminovos",
        "hibridos" => "Carros hibridos",
        "eletricos" => "Carros eletricos",
        "motos-eletricas" => "Motos eletricas",
        "taquaritinga" => "Veiculos em Taquaritinga",
        _ => "Estoque de veiculos"
    };

    private static string GetSegmentDescription(string? segment, int totalItems) => segment switch
    {
        "seminovos-taquaritinga" => $"Veja {totalItems} carros seminovos em Taquaritinga na Anderson Multimarcas, com atendimento local, troca e orientacao para financiamento.",
        "carros-automaticos-taquaritinga" => $"Confira carros automaticos em Taquaritinga com filtros por marca, ano e preco. Fale com a Anderson Multimarcas para confirmar disponibilidade.",
        "suvs-seminovos-taquaritinga" => $"Pesquise SUVs seminovos em Taquaritinga com atendimento Anderson Multimarcas, opcoes de troca e suporte comercial.",
        "carros-ate-50-mil" => $"Encontre carros ate 50 mil no estoque da Anderson Multimarcas e fale com a equipe para confirmar preco, troca e financiamento.",
        "financiamento" => "Consulte opcoes de financiamento de veiculos em Taquaritinga com a Anderson Multimarcas e compare modelos disponiveis no estoque.",
        "troca-de-veiculos" => "Troque seu carro usado em Taquaritinga com atendimento Anderson Multimarcas, avaliacao comercial e opcoes de estoque.",
        "zero-km" => $"Confira {totalItems} opcoes de carros 0 km na Anderson Multimarcas em Taquaritinga/SP, com atendimento consultivo, troca e financiamento.",
        "seminovos" => $"Veja {totalItems} carros seminovos selecionados em Taquaritinga/SP, com procedencia, avaliacao tecnica e atendimento Anderson Multimarcas.",
        "hibridos" => "Encontre carros hibridos em Taquaritinga/SP com eficiencia, tecnologia e suporte de vendedores especializados.",
        "eletricos" => "Compare carros eletricos disponiveis em Taquaritinga/SP, incluindo modelos para mobilidade urbana e uso diario.",
        "motos-eletricas" => "Conheca motos eletricas em Taquaritinga/SP para mobilidade economica, pratica e sustentavel.",
        "taquaritinga" => "Estoque local da Anderson Multimarcas em Taquaritinga/SP com carros, motos eletricas, atendimento presencial e rota para as lojas.",
        _ => $"Catalogo com {totalItems} veiculos seminovos, 0 km, hibridos e eletricos em Taquaritinga/SP, com filtros por marca, preco, ano e combustivel."
    };
}
