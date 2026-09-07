using GestorONG.Domain.Exceptions;
using GestorONG.Domain.ValueObjects;

namespace GestorONG.Domain.Tests.ValueObjects;

public class CpfTests
{
    [Theory]
    [InlineData("111.444.777-35")]
    [InlineData("11144477735")]
    [InlineData("529.982.247-25")]
    [InlineData("  111 444 777 35  ")]
    public void Aceita_cpf_valido(string entrada) =>
        Assert.True(Cpf.TryParse(entrada, out _));

    [Theory]
    [InlineData("111.444.777-36")]
    [InlineData("111.444.777-45")]
    [InlineData("1114447773")]
    [InlineData("111444777351")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejeita_cpf_invalido(string? entrada) =>
        Assert.False(Cpf.TryParse(entrada, out _));

    [Theory]
    [InlineData("000.000.000-00")]
    [InlineData("111.111.111-11")]
    [InlineData("999.999.999-99")]
    public void Rejeita_sequencia_repetida(string entrada) =>
        Assert.False(Cpf.TryParse(entrada, out _));

    [Fact]
    public void Normaliza_para_somente_digitos()
    {
        var cpf = Cpf.Criar("111.444.777-35");
        Assert.Equal("11144477735", cpf.Valor);
    }

    [Fact]
    public void Formata_para_exibicao()
    {
        var cpf = Cpf.Criar("11144477735");
        Assert.Equal("111.444.777-35", cpf.Formatado());
    }

    [Fact]
    public void ToString_mascara_o_valor()
    {
        var cpf = Cpf.Criar("111.444.777-35");
        Assert.Equal("***.***.***-35", cpf.ToString());
        Assert.DoesNotContain("11144477", cpf.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Criar_lanca_excecao_de_dominio_quando_invalido() =>
        Assert.Throws<DomainValidationException>(() => Cpf.Criar("123"));

    [Fact]
    public void Compara_por_valor_ignorando_pontuacao() =>
        Assert.Equal(Cpf.Criar("111.444.777-35"), Cpf.Criar("11144477735"));
}
