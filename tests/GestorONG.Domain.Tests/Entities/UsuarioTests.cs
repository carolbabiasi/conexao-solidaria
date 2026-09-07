using GestorONG.Domain.Entities;
using GestorONG.Domain.Enums;
using GestorONG.Domain.Exceptions;
using GestorONG.Domain.ValueObjects;

namespace GestorONG.Domain.Tests.Entities;

public class UsuarioTests
{
    private static Usuario CriarValido(string? nome = "Ana Silva", string? senhaHash = "$2a$12$hash") =>
        Usuario.Criar(
            nome,
            Email.Criar("ana@x.com"),
            Cpf.Criar("111.444.777-35"),
            senhaHash,
            Role.Doador,
            DateTimeOffset.UtcNow);

    [Fact]
    public void Cria_usuario_valido()
    {
        var usuario = CriarValido();

        Assert.NotEqual(Guid.Empty, usuario.Id);
        Assert.Equal("Ana Silva", usuario.NomeCompleto);
        Assert.Equal(Role.Doador, usuario.Role);
    }

    [Fact]
    public void Remove_espacos_das_pontas_do_nome() =>
        Assert.Equal("Ana Silva", CriarValido("  Ana Silva  ").NomeCompleto);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejeita_nome_vazio(string? nome) =>
        Assert.Throws<DomainValidationException>(() => CriarValido(nome));

    [Fact]
    public void Rejeita_nome_longo_demais() =>
        Assert.Throws<DomainValidationException>(() => CriarValido(new string('a', 201)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Rejeita_hash_de_senha_vazio(string? hash) =>
        Assert.Throws<DomainValidationException>(() => CriarValido(senhaHash: hash));

    [Fact]
    public void Acumula_todas_as_violacoes_de_uma_vez()
    {
        var excecao = Assert.Throws<DomainValidationException>(
            () => CriarValido(nome: null, senhaHash: null));

        Assert.Equal(2, excecao.Violacoes.Count);
    }

    [Fact]
    public void ToString_nao_vaza_dados_pessoais()
    {
        var texto = CriarValido().ToString();

        Assert.DoesNotContain("11144477735", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("ana@x.com", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("$2a$12$hash", texto, StringComparison.Ordinal);
    }
}
