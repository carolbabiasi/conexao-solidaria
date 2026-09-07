using GestorONG.Domain.Entities;
using GestorONG.Domain.Enums;
using GestorONG.Domain.Exceptions;
using Microsoft.Extensions.Time.Testing;

namespace GestorONG.Domain.Tests.Entities;

public class CampanhaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private static FakeTimeProvider Relogio() => new(Agora);

    private static Campanha CriarValida(FakeTimeProvider? tempo = null)
    {
        tempo ??= Relogio();
        return Campanha.Criar(
            "Cestas básicas",
            "Arrecadação de cestas para 100 famílias",
            Agora,
            Agora.AddDays(30),
            5000m,
            tempo);
    }

    [Fact]
    public void Nasce_com_valor_arrecadado_zerado() =>
        Assert.Equal(decimal.Zero, CriarValida().ValorArrecadado);

    [Fact]
    public void Nasce_ativa_por_padrao() =>
        Assert.Equal(StatusCampanha.Ativa, CriarValida().Status);

    [Fact]
    public void Gera_identificador() =>
        Assert.NotEqual(Guid.Empty, CriarValida().Id);

    [Fact]
    public void ValorArrecadado_nao_expoe_setter_publico()
    {
        var setter = typeof(Campanha).GetProperty(nameof(Campanha.ValorArrecadado))!.SetMethod;
        Assert.False(setter is not null && setter.IsPublic);
    }

    [Fact]
    public void Rejeita_data_de_termino_no_passado()
    {
        var tempo = Relogio();
        tempo.SetUtcNow(Agora.AddDays(60));

        var excecao = Assert.Throws<DomainValidationException>(() => Campanha.Criar(
            "Cestas básicas",
            "Descrição",
            Agora,
            Agora.AddDays(30),
            5000m,
            tempo));

        Assert.Contains(excecao.Violacoes, v => v.Mensagem.Contains("passado", StringComparison.Ordinal));
    }

    [Fact]
    public void Aceita_data_de_termino_no_futuro() =>
        Assert.Equal(StatusCampanha.Ativa, CriarValida().Status);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5000)]
    public void Rejeita_meta_nao_positiva(decimal meta) =>
        Assert.Throws<DomainValidationException>(() => Campanha.Criar(
            "Cestas básicas", "Descrição", Agora, Agora.AddDays(30), meta, Relogio()));

    [Fact]
    public void Rejeita_data_de_termino_anterior_ao_inicio() =>
        Assert.Throws<DomainValidationException>(() => Campanha.Criar(
            "Cestas básicas", "Descrição", Agora.AddDays(10), Agora.AddDays(5), 5000m, Relogio()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejeita_titulo_vazio(string? titulo) =>
        Assert.Throws<DomainValidationException>(() => Campanha.Criar(
            titulo, "Descrição", Agora, Agora.AddDays(30), 5000m, Relogio()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejeita_descricao_vazia(string? descricao) =>
        Assert.Throws<DomainValidationException>(() => Campanha.Criar(
            "Cestas básicas", descricao, Agora, Agora.AddDays(30), 5000m, Relogio()));

    [Fact]
    public void Acumula_todas_as_violacoes_de_uma_vez()
    {
        var excecao = Assert.Throws<DomainValidationException>(() => Campanha.Criar(
            titulo: null,
            descricao: null,
            dataInicio: Agora.AddDays(10),
            dataFim: Agora.AddDays(5),
            metaFinanceira: 0m,
            tempo: Relogio()));

        Assert.Equal(4, excecao.Violacoes.Count);
    }

    [Fact]
    public void Esta_aberta_quando_ativa_e_vigente() =>
        Assert.True(CriarValida().EstaAbertaParaDoacao(Relogio()));

    [Fact]
    public void Nao_esta_aberta_depois_da_data_de_termino()
    {
        var tempo = Relogio();
        var campanha = CriarValida(tempo);

        tempo.SetUtcNow(Agora.AddDays(31));

        Assert.False(campanha.EstaAbertaParaDoacao(tempo));
    }

    [Fact]
    public void Registrar_arrecadacao_soma_ao_total()
    {
        var campanha = CriarValida();

        campanha.RegistrarArrecadacao(100m);
        campanha.RegistrarArrecadacao(250.50m);

        Assert.Equal(350.50m, campanha.ValorArrecadado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Registrar_arrecadacao_rejeita_valor_nao_positivo(decimal valor) =>
        Assert.Throws<DomainValidationException>(() => CriarValida().RegistrarArrecadacao(valor));

    [Fact]
    public void Nao_conclui_sozinha_ao_atingir_a_meta()
    {
        var campanha = CriarValida();

        campanha.RegistrarArrecadacao(5000m);

        Assert.Equal(StatusCampanha.Ativa, campanha.Status);
    }

    [Fact]
    public void Permite_ultrapassar_a_meta()
    {
        var campanha = CriarValida();

        campanha.RegistrarArrecadacao(6000m);

        Assert.Equal(6000m, campanha.ValorArrecadado);
        Assert.Equal(120m, campanha.PercentualAtingido());
    }

    [Fact]
    public void Percentual_atingido_comeca_em_zero() =>
        Assert.Equal(decimal.Zero, CriarValida().PercentualAtingido());
}
