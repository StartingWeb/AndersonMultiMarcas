namespace Project.Pages.ViewModels;

public sealed class HomeVehicleCardViewModel
{
    public int Id { get; init; }
    public string Titulo { get; init; } = string.Empty;
    public string? Cor { get; init; }
    public int? AnoFabricacao { get; init; }
    public int AnoModelo { get; init; }
    public string AnoExibicao => AnoFabricacao.HasValue ? $"{AnoFabricacao} / {AnoModelo}" : AnoModelo.ToString();
    public string Cambio { get; init; } = string.Empty;
    public string Combustivel { get; init; } = string.Empty;
    public bool Destaque { get; init; }
    public bool Disponivel { get; init; }
    public bool Seminovo { get; init; }
    public bool ZeroKm => !Seminovo;
    public decimal Preco { get; init; }
    public string? MidiaUrl { get; init; }
    public int Cliques { get; init; }
    public int Visualizacoes { get; init; }
}

public sealed class HomeStoreViewModel
{
    public string Nome { get; init; } = string.Empty;
    public string EnderecoCompleto { get; init; } = string.Empty;
    public string MapsQuery { get; init; } = string.Empty;
}

public sealed class HomeSellerViewModel
{
    public string Nome { get; init; } = string.Empty;
    public string Telefone { get; init; } = string.Empty;
    public string? FotoUrl { get; init; }
}

public sealed class HomeRankingSectionViewModel
{
    public string Kicker { get; init; } = string.Empty;
    public string Titulo { get; init; } = string.Empty;
    public string Descricao { get; init; } = string.Empty;
    public string MetricLabel { get; init; } = string.Empty;
    public string MetricClass { get; init; } = string.Empty;
    public string Link { get; init; } = string.Empty;
    public IReadOnlyCollection<HomeVehicleCardViewModel> Itens { get; init; } = [];
}

public sealed class ContactStoreViewModel
{
    public string Nome { get; init; } = string.Empty;
    public string EnderecoCompleto { get; init; } = string.Empty;
    public string Telefone { get; init; } = string.Empty;
    public string TelefoneExibicao { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string MapsQuery { get; init; } = string.Empty;
    public IReadOnlyList<ContactSellerViewModel> Vendedores { get; init; } = [];
}

public sealed class ContactSellerViewModel
{
    public string Nome { get; init; } = string.Empty;
    public string Telefone { get; init; } = string.Empty;
    public string TelefoneExibicao { get; init; } = string.Empty;
    public string? Cargo { get; init; }
    public string? FotoUrl { get; init; }
}

public sealed class SearchAutocompleteViewModel
{
    public string Action { get; init; } = "/veiculos";
    public string Endpoint { get; init; } = "/Index?handler=SearchSuggestions";
    public string Placeholder { get; init; } = "Busque por marca, modelo, versão ou ano";
    public string InputName { get; init; } = "busca";
    public string AriaLabel { get; init; } = "Pesquisar veículos";
    public string? Value { get; init; }
}
