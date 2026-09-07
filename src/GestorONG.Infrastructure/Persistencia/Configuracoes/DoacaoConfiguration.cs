using GestorONG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorONG.Infrastructure.Persistencia.Configuracoes;

internal sealed class DoacaoConfiguration : IEntityTypeConfiguration<Doacao>
{
    public void Configure(EntityTypeBuilder<Doacao> builder)
    {
        builder.ToTable("doacoes");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.IdCampanha)
            .HasColumnName("id_campanha")
            .IsRequired();

        builder.Property(d => d.IdDoador)
            .HasColumnName("id_doador")
            .IsRequired();

        builder.Property(d => d.Valor)
            .HasColumnName("valor")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(d => d.DataCriacao)
            .HasColumnName("data_criacao")
            .IsRequired();

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.HasOne<Campanha>()
            .WithMany()
            .HasForeignKey(d => d.IdCampanha)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(d => d.IdDoador)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.IdCampanha)
            .HasDatabaseName("ix_doacoes_id_campanha");
    }
}
