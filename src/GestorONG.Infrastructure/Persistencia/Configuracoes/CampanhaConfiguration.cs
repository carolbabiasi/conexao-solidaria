using GestorONG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorONG.Infrastructure.Persistencia.Configuracoes;

internal sealed class CampanhaConfiguration : IEntityTypeConfiguration<Campanha>
{
    public void Configure(EntityTypeBuilder<Campanha> builder)
    {
        builder.ToTable("campanhas");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Titulo)
            .HasColumnName("titulo")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(c => c.DataInicio)
            .HasColumnName("data_inicio")
            .IsRequired();

        builder.Property(c => c.DataFim)
            .HasColumnName("data_fim")
            .IsRequired();

        builder.Property(c => c.MetaFinanceira)
            .HasColumnName("meta_financeira")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.ValorArrecadado)
            .HasColumnName("valor_arrecadado")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.HasIndex(c => c.Status)
            .HasDatabaseName("ix_campanhas_status");
    }
}
