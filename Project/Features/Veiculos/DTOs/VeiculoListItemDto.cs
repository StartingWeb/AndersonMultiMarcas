using Domain.Enums;

namespace Project.Features.Veiculos.DTOs;

public sealed class VeiculoListItemDto
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
    public string? MidiaCapaUrl { get; init; }
    public string MarcaNome { get; init; } = string.Empty;
    public string LojaNome { get; init; } = string.Empty;
}
