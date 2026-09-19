using GestorONG.Domain.Entities;
using GestorONG.Domain.Enums;
using GestorONG.Domain.Exceptions;
using Microsoft.Extensions.Time.Testing;

namespace GestorONG.Domain.Tests.Entities;

/// <summary>
/// Edicao (CAMP-02) e transicao de status (CAMP-03).
/// </summary>
public class CampanhaEdicaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private static FakeTimeProvider Relogio() => new(Agora);

    private static Campanha CriarValida(FakeTimeProvider? tempo = null) =>
        Campanha.Criar(
            "Cestas básicas",
            "Arrecadação de cestas para 100 famílias",
            Agora,
            Agora.AddDays(30),
            5000m,
            tempo ?? Relogio());

    // ---------------------------------------------------------------- edicao

    [Fact]
    public void Atualiza_campos_editaveis()
    {
        var tempo = Relogio();
        var campanha = CriarValida(tempo);

        campanha.Atualizar(
            "Cestas de inverno",
            "Texto revisado",
            Agora,
            Agora.AddDays(60),
            5000m,
            tempo);

        Assert.Equal("Cestas de inverno", campanha.Titulo);
        Assert.Equal("Texto revisado", campanha.Descricao);
        Assert.Equal(Agora.AddDays(60), campanha.DataFim);
    }

    [Fact]
    public void Edicao_nao_mexe_no_valor_arrecadado()
    {
        var tempo = Relogio();
        var campanha = CriarValida(tempo);
        campanha.RegistrarArrecadacao(250m);

        campanha.Atualizar(
            "Outro título",
            "Outra descrição",
            Agora,
            Agora.AddDays(60),
            5000m,
            tempo);

        Assert.Equal(250m, campanha.ValorArrecadado);
    }

    [Fact]
    public void Meta_pode_mudar_enquanto_nao_houve_doacao()
    {
        var tempo = Relogio();
        var campanha = CriarValida(tempo);

        campanha.Atualizar(
            "Cestas básicas",
            "Arrecadação de cestas para 100 famílias",
            Agora,
            Agora.AddDays(30),
            9000m,
            tempo);

        Assert.Equal(9000m, campanha.MetaFinanceira);
    }

    [Fact]
    public void Meta_congela_depois_da_primeira_doacao()
    {
        var tempo = Relogio();
        var campanha = CriarValida(tempo);
        campanha.RegistrarArrecadacao(10m);

        var excecao = Assert.Throws<DomainValidationException>(() => campanha.Atualizar(
            "Cestas básicas",
            "Arrecadação de cestas para 100 famílias",
            Agora,
            Agora.AddDays(30),
            9000m,
            tempo));

        Assert.Contains(excecao.Violacoes, v => v.Propriedade == nameof(Campanha.MetaFinanceira));
        Assert.Equal(5000m, campanha.MetaFinanceira);
    }

    [Fact]
    public void Data_de_inicio_congela_depois_da_primeira_doacao()
    {
        var tempo = Relogio();
        var campanha = CriarValida(tempo);
        campanha.RegistrarArrecadacao(10m);

        var excecao = Assert.Throws<DomainValidationException>(() => campanha.Atualizar(
            "Cestas básicas",
            "Arrecadação de cestas para 100 famílias",
            Agora.AddDays(-5),
            Agora.AddDays(30),
            5000m,
            tempo));

        Assert.Contains(excecao.Violacoes, v => v.Propriedade == nameof(Campanha.DataInicio));
    }

    [Fact]
    public void Prorrogar_continua_permitido_depois_de_doacao()
    {
        var tempo = Relogio();
        var campanha = CriarValida(tempo);
        campanha.RegistrarArrecadacao(10m);

        campanha.Atualizar(
            "Cestas básicas",
            "Arrecadação de cestas para 100 famílias",
            Agora,
            Agora.AddDays(90),
            5000m,
            tempo);

        Assert.Equal(Agora.AddDays(90), campanha.DataFim);
    }

    [Fact]
    public void Edicao_revalida_as_regras_da_criacao()
    {
        var tempo = Relogio();
        var campanha = CriarValida(tempo);

        var excecao = Assert.Throws<DomainValidationException>(() => campanha.Atualizar(
            "   ",
            "Arrecadação de cestas para 100 famílias",
            Agora,
            Agora.AddDays(-1),
            5000m,
            tempo));

        Assert.Contains(excecao.Violacoes, v => v.Propriedade == nameof(Campanha.Titulo));
        Assert.Contains(excecao.Violacoes, v => v.Propriedade == nameof(Campanha.DataFim));
    }

    [Fact]
    public void Campanha_cancelada_nao_pode_ser_editada()
    {
        var tempo = Relogio();
        var campanha = CriarValida(tempo);
        campanha.AlterarStatus(StatusCampanha.Cancelada);

        var excecao = Assert.Throws<DomainValidationException>(() => campanha.Atualizar(
            "Cestas básicas",
            "Arrecadação de cestas para 100 famílias",
            Agora,
            Agora.AddDays(30),
            5000m,
            tempo));

        Assert.Contains(excecao.Violacoes, v => v.Propriedade == nameof(Campanha.Status));
    }

    // ------------------------------------------------------ transicao de status

    [Theory]
    [InlineData(StatusCampanha.Concluida)]
    [InlineData(StatusCampanha.Cancelada)]
    public void Ativa_transita_para_concluida_ou_cancelada(StatusCampanha destino)
    {
        var campanha = CriarValida();

        campanha.AlterarStatus(destino);

        Assert.Equal(destino, campanha.Status);
    }

    [Theory]
    [InlineData(StatusCampanha.Cancelada, StatusCampanha.Ativa)]
    [InlineData(StatusCampanha.Concluida, StatusCampanha.Ativa)]
    [InlineData(StatusCampanha.Cancelada, StatusCampanha.Concluida)]
    [InlineData(StatusCampanha.Concluida, StatusCampanha.Cancelada)]
    public void Estado_final_nao_transita(StatusCampanha inicial, StatusCampanha destino)
    {
        var campanha = CriarValida();
        campanha.AlterarStatus(inicial);

        var excecao = Assert.Throws<TransicaoDeStatusInvalidaException>(
            () => campanha.AlterarStatus(destino));

        Assert.Equal(inicial, excecao.Atual);
        Assert.Equal(destino, excecao.Pretendido);
        Assert.Equal(inicial, campanha.Status);
    }

    [Fact]
    public void Transitar_para_o_mesmo_status_e_conflito()
    {
        var campanha = CriarValida();

        Assert.Throws<TransicaoDeStatusInvalidaException>(
            () => campanha.AlterarStatus(StatusCampanha.Ativa));
    }

    [Fact]
    public void Status_fora_do_enum_e_erro_de_validacao()
    {
        var campanha = CriarValida();

        Assert.Throws<DomainValidationException>(
            () => campanha.AlterarStatus((StatusCampanha)99));
    }

    [Fact]
    public void Cancelada_deixa_de_aceitar_doacao()
    {
        var tempo = Relogio();
        var campanha = CriarValida(tempo);

        campanha.AlterarStatus(StatusCampanha.Cancelada);

        Assert.False(campanha.EstaAbertaParaDoacao(tempo));
    }
}
