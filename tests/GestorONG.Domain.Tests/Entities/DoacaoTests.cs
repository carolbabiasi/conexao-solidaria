using GestorONG.Domain.Entities;
using GestorONG.Domain.Enums;
using GestorONG.Domain.Exceptions;
using Microsoft.Extensions.Time.Testing;

namespace GestorONG.Domain.Tests.Entities;

public class DoacaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Doador = Guid.CreateVersion7();

    private static FakeTimeProvider Relogio() => new(Agora);

    private static Campanha CampanhaAtiva(FakeTimeProvider tempo) => Campanha.Criar(
        "Cestas básicas",
        "Arrecadação de cestas",
        Agora,
        Agora.AddDays(30),
        5000m,
        tempo);

    private static Campanha CampanhaCom(StatusCampanha status, FakeTimeProvider tempo) => Campanha.Criar(
        "Cestas básicas",
        "Arrecadação de cestas",
        Agora,
        Agora.AddDays(30),
        5000m,
        tempo,
        status);

    [Fact]
    public void Nasce_pendente()
    {
        var tempo = Relogio();
        var doacao = Doacao.Criar(CampanhaAtiva(tempo), Doador, 100m, tempo);

        Assert.Equal(StatusDoacao.Pendente, doacao.Status);
    }

    [Fact]
    public void Guarda_campanha_doador_e_valor()
    {
        var tempo = Relogio();
        var campanha = CampanhaAtiva(tempo);

        var doacao = Doacao.Criar(campanha, Doador, 100m, tempo);

        Assert.Equal(campanha.Id, doacao.IdCampanha);
        Assert.Equal(Doador, doacao.IdDoador);
        Assert.Equal(100m, doacao.Valor);
        Assert.Equal(Agora, doacao.DataCriacao);
        Assert.NotEqual(Guid.Empty, doacao.Id);
    }

    [Fact]
    public void Marcar_como_processada_e_idempotente()
    {
        var tempo = Relogio();
        var doacao = Doacao.Criar(CampanhaAtiva(tempo), Doador, 100m, tempo);

        doacao.MarcarComoProcessada();
        doacao.MarcarComoProcessada();

        Assert.Equal(StatusDoacao.Processada, doacao.Status);
    }

    [Fact]
    public void Rejeita_doacao_para_campanha_cancelada()
    {
        var tempo = Relogio();

        var excecao = Assert.Throws<DomainValidationException>(
            () => Doacao.Criar(CampanhaCom(StatusCampanha.Cancelada, tempo), Doador, 100m, tempo));

        Assert.Contains(excecao.Violacoes, v => v.Mensagem.Contains("cancelada", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejeita_doacao_para_campanha_concluida()
    {
        var tempo = Relogio();

        var excecao = Assert.Throws<DomainValidationException>(
            () => Doacao.Criar(CampanhaCom(StatusCampanha.Concluida, tempo), Doador, 100m, tempo));

        Assert.Contains(excecao.Violacoes, v => v.Mensagem.Contains("concluída", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejeita_doacao_para_campanha_vencida_ainda_marcada_como_ativa()
    {
        var tempo = Relogio();
        var campanha = CampanhaAtiva(tempo);

        tempo.SetUtcNow(Agora.AddDays(31));

        var excecao = Assert.Throws<DomainValidationException>(
            () => Doacao.Criar(campanha, Doador, 100m, tempo));

        Assert.Equal(StatusCampanha.Ativa, campanha.Status);
        Assert.Contains(excecao.Violacoes, v => v.Mensagem.Contains("encerrada", StringComparison.Ordinal));
    }

    [Fact]
    public void Aceita_doacao_no_ultimo_instante_da_vigencia()
    {
        var tempo = Relogio();
        var campanha = CampanhaAtiva(tempo);

        tempo.SetUtcNow(campanha.DataFim);

        var doacao = Doacao.Criar(campanha, Doador, 100m, tempo);

        Assert.Equal(StatusDoacao.Pendente, doacao.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.50)]
    public void Rejeita_valor_nao_positivo(decimal valor)
    {
        var tempo = Relogio();

        Assert.Throws<DomainValidationException>(
            () => Doacao.Criar(CampanhaAtiva(tempo), Doador, valor, tempo));
    }

    [Fact]
    public void Rejeita_doador_vazio()
    {
        var tempo = Relogio();

        Assert.Throws<DomainValidationException>(
            () => Doacao.Criar(CampanhaAtiva(tempo), Guid.Empty, 100m, tempo));
    }

    [Fact]
    public void Acumula_violacoes_de_valor_e_de_campanha()
    {
        var tempo = Relogio();

        var excecao = Assert.Throws<DomainValidationException>(
            () => Doacao.Criar(CampanhaCom(StatusCampanha.Cancelada, tempo), Doador, 0m, tempo));

        Assert.Equal(2, excecao.Violacoes.Count);
    }
}
