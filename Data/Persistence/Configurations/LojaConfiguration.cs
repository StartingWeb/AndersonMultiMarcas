using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Persistence.Configurations;

public sealed class LojaConfiguration : IEntityTypeConfiguration<Loja>
{
    public void Configure(EntityTypeBuilder<Loja> builder)
    {
        builder.ToTable("Loja");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nome).IsRequired().HasMaxLength(150);
        builder.Property(x => x.RazaoSocial).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Ativo).IsRequired();
        builder.Property(x => x.DataCadastro).IsRequired();
        builder.Property(x => x.DataAtualizacao);

        builder.Property(x => x.Cnpj)
            .HasConversion(x => x.HasValue ? x.Value.Valor : null, x => string.IsNullOrWhiteSpace(x) ? (Documento?)null : new Documento(x))
            .HasColumnName("Cnpj")
            .IsRequired(false)
            .HasMaxLength(14);

        builder.Property(x => x.Email)
            .HasConversion(x => x.Valor, x => new Email(x))
            .HasColumnName("Email")
            .IsRequired()
            .HasMaxLength(180);

        builder.Property(x => x.Telefone)
            .HasConversion(x => x.Valor, x => new Telefone(x))
            .HasColumnName("Telefone")
            .IsRequired()
            .HasMaxLength(20);

        builder.OwnsOne(x => x.Endereco, endereco =>
        {
            endereco.Property(x => x.Logradouro).HasColumnName("Endereco").IsRequired().HasMaxLength(180);
            endereco.Property(x => x.Numero).HasColumnName("Numero").IsRequired().HasMaxLength(20);
            endereco.Property(x => x.Complemento).HasColumnName("Complemento").HasMaxLength(100);
            endereco.Property(x => x.Bairro).HasColumnName("Bairro").IsRequired().HasMaxLength(100);
            endereco.Property(x => x.Cidade).HasColumnName("Cidade").IsRequired().HasMaxLength(100);
            endereco.Property(x => x.Uf).HasColumnName("Uf").HasConversion<string>().IsRequired().HasMaxLength(2);
            endereco.Property(x => x.Cep).HasColumnName("Cep").IsRequired().HasMaxLength(8);
        });

        builder.HasIndex(x => x.Nome);
        builder.HasIndex("Cnpj")
            .IsUnique()
            .HasFilter("[Cnpj] IS NOT NULL");
    }
}
