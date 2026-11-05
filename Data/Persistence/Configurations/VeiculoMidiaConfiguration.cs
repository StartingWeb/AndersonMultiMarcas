using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Persistence.Configurations;

public sealed class VeiculoMidiaConfiguration : IEntityTypeConfiguration<VeiculoMidia>
{
    public void Configure(EntityTypeBuilder<VeiculoMidia> builder)
    {
        builder.ToTable("VeiculoMidia");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.VeiculoId).IsRequired();
        builder.Property(x => x.NomeArquivo).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(500);
        builder.Property(x => x.BlobName).HasMaxLength(250);
        builder.Property(x => x.Container).HasMaxLength(250);
        builder.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120);
        builder.Property(x => x.TamanhoBytes);
        builder.Property(x => x.Capa).IsRequired();
        builder.Property(x => x.Ordem).IsRequired();
        builder.Property(x => x.Ativo).IsRequired();
        builder.Property(x => x.DataCadastro).IsRequired();

        builder.HasIndex(x => new { x.VeiculoId, x.Capa }).IsUnique().HasFilter("[Capa] = 1");
        builder.HasIndex(x => new { x.VeiculoId, x.Ordem });
    }
}
