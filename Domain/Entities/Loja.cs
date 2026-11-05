using Domain.Common;
using Domain.ValueObjects;

namespace Domain.Entities;

public sealed class Loja : AuditableEntity
{
    public string Nome { get; private set; } = null!;
    public string RazaoSocial { get; private set; } = null!;
    public Documento? Cnpj { get; private set; }
    public Email Email { get; private set; }
    public Telefone Telefone { get; private set; }
    public Endereco Endereco { get; private set; } = null!;

    public ICollection<Veiculo> Veiculos { get; private set; } = new List<Veiculo>();
    public ICollection<Vendedor> Vendedores { get; private set; } = new List<Vendedor>();

    private Loja() { }

    public Loja(string nome, string razaoSocial, Documento? cnpj, Email email, Telefone telefone, Endereco endereco)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome obrigatorio.") : nome.Trim();
        RazaoSocial = string.IsNullOrWhiteSpace(razaoSocial) ? throw new ArgumentException("Razao social obrigatoria.") : razaoSocial.Trim();
        Cnpj = cnpj;
        Email = email;
        Telefone = telefone;
        Endereco = endereco;
    }

    public void Update(string nome, string razaoSocial, Documento? cnpj, Email email, Telefone telefone, Endereco endereco)
    {
        Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome obrigatorio.") : nome.Trim();
        RazaoSocial = string.IsNullOrWhiteSpace(razaoSocial) ? throw new ArgumentException("Razao social obrigatoria.") : razaoSocial.Trim();
        Cnpj = cnpj;
        Email = email;
        Telefone = telefone;
        Endereco = endereco;
        MarcarAtualizacao();
    }
}
