using GestorONG.Domain.Entities;
using GestorONG.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorONG.Infrastructure.Persistencia.Configuracoes;

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.NomeCompleto)
            .HasColumnName("nome_completo")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired()
            .HasConversion(email => email.Valor, valor => Email.Criar(valor));

        builder.Property(u => u.Cpf)
            .HasColumnName("cpf")
            .HasMaxLength(11)
            .IsFixedLength()
            .IsRequired()
            .HasConversion(cpf => cpf.Valor, valor => Cpf.Criar(valor));

        builder.Property(u => u.SenhaHash)
            .HasColumnName("senha_hash")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .IsRequired();

        builder.Property(u => u.CriadoEm)
            .HasColumnName("criado_em")
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ix_usuarios_email");

        builder.HasIndex(u => u.Cpf)
            .IsUnique()
            .HasDatabaseName("ix_usuarios_cpf");
    }
}
