using GestorONG.Domain.Exceptions;
using GestorONG.Domain.ValueObjects;

namespace GestorONG.Domain.Tests.ValueObjects;

public class EmailTests
{
    [Theory]
    [InlineData("ana@x.com")]
    [InlineData("ana.silva+tag@sub.dominio.com.br")]
    [InlineData("a@bc.de")]
    public void Aceita_email_valido(string entrada) =>
        Assert.True(Email.TryParse(entrada, out _));

    [Theory]
    [InlineData("ana@localhost")]
    [InlineData("ana@x.c")]
    [InlineData("ana@@x.com")]
    [InlineData("@x.com")]
    [InlineData("ana@")]
    [InlineData("ana@x..com")]
    [InlineData("an a@x.com")]
    [InlineData("ana")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejeita_email_invalido(string? entrada) =>
        Assert.False(Email.TryParse(entrada, out _));

    [Fact]
    public void Normaliza_caixa_e_espacos_nas_pontas()
    {
        var email = Email.Criar("  Ana@X.COM  ");
        Assert.Equal("ana@x.com", email.Valor);
    }

    [Fact]
    public void Compara_por_valor_normalizado() =>
        Assert.Equal(Email.Criar("  Ana@X.com "), Email.Criar("ana@x.com"));

    [Fact]
    public void Criar_lanca_excecao_de_dominio_quando_invalido() =>
        Assert.Throws<DomainValidationException>(() => Email.Criar("ana"));
}
