using Domain.Common;

namespace Domain.Entities;

public sealed class Marca : BaseEntity
{
    public string Nome { get; private set; } = null!;
    public string? LogoUrl { get; private set; }

    public ICollection<Veiculo> Veiculos { get; private set; } = new List<Veiculo>();

    private Marca() { }

    public Marca(string nome, string? logoUrl)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome da marca obrigatório.") : nome.Trim();
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
    }

    public void Update(string nome, string? logoUrl)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome da marca obrigatório.") : nome.Trim();
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
    }
}
