using Domain.Enums;
using System.Text.Json.Serialization;

namespace Project.Features.Veiculos.DTOs;

public class VeiculoCreateDto
{
    public int LojaId { get; init; }
    public int MarcaId { get; init; }
    private int? vendedorId;
    public int? VendedorId { get => vendedorId; init { vendedorId = value; VendedorIdSpecified = true; } }
    [JsonIgnore] public bool VendedorIdSpecified { get; private set; }
    public string Titulo { get; init; } = string.Empty;
    public string Modelo { get; init; } = string.Empty;
    public string? Versao { get; init; }
    public int? AnoFabricacao { get; init; }
    public int AnoModelo { get; init; }
    public string? Cor { get; init; }
    public Combustivel Combustivel { get; init; }
    public Cambio Cambio { get; init; }
    public decimal PrecoVenda { get; init; }
    public int? Quilometragem { get; init; }
    public string? Placa { get; init; }
    public string? Descricao { get; init; }
    public bool Destaque { get; init; }
    public bool Seminovo { get; init; }
    public bool Financiavel { get; init; }
    public bool AceitaTroca { get; init; }
    private string? urlVideo;
    public string? UrlVideo { get => urlVideo; init { urlVideo = value; UrlVideoSpecified = true; } }
    [JsonIgnore] public bool UrlVideoSpecified { get; private set; }
    private string? observacoesInternas;
    public string? ObservacoesInternas { get => observacoesInternas; init { observacoesInternas = value; ObservacoesInternasSpecified = true; } }
    [JsonIgnore] public bool ObservacoesInternasSpecified { get; private set; }
    public IReadOnlyCollection<TipoVeiculoOpcional> Opcionais { get; init; } = [];
}
