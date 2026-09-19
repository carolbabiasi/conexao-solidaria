using GestorONG.Application.Abstracoes;
using GestorONG.Application.Excecoes;
using GestorONG.Contracts.V1;
using GestorONG.Domain.Entities;
using GestorONG.Infrastructure.Observabilidade;
using GestorONG.Worker.Consumidores;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace GestorONG.Worker.Tests.Consumidores;

/// <summary>
/// Prova automatizada do risco R2: reentrega nao infla o valor arrecadado.
///
/// O harness do MassTransit sobe o transporte em memoria e roda o consumer de
/// verdade - o mesmo codigo que roda em producao, com as dependencias trocadas
/// por dubles. O que muda e so o transporte.
/// </summary>
public sealed class DoacaoRecebidaConsumerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid IdCampanha = Guid.Parse("01a09d03-bd42-7fb1-abe4-17987d98a8b8");
    private static readonly Guid IdDoador = Guid.Parse("01a09d04-9d8c-7356-ba32-5320bef05ee6");

    private static DoacaoRecebidaEvent Evento(Guid idDoacao, decimal valor = 125.50m) => new()
    {
        IdDoacao = idDoacao,
        IdCampanha = IdCampanha,
        IdDoador = IdDoador,
        Valor = valor,
        OcorridoEm = Agora
    };

    private static ServiceProvider Montar(
        LedgerFake ledger,
        CampanhaRepositorioFake campanhas,
        DoacaoRepositorioFake doacoes) =>
        new ServiceCollection()
            .AddMetrics()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(Agora))
            .AddSingleton<MetricasDeNegocio>()
            .AddSingleton<IDoacaoLedgerRepository>(ledger)
            .AddSingleton<ICampanhaRepository>(campanhas)
            .AddSingleton<IDoacaoRepository>(doacoes)
            .AddLogging()
            .AddMassTransitTestHarness(x => x.AddConsumer<DoacaoRecebidaConsumer>())
            .BuildServiceProvider(true);

    [Fact]
    public async Task Processa_a_primeira_entrega()
    {
        var ledger = new LedgerFake();
        var campanhas = new CampanhaRepositorioFake();
        var doacoes = new DoacaoRepositorioFake();

        await using var provedor = Montar(ledger, campanhas, doacoes);
        var harness = provedor.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(Evento(Guid.CreateVersion7()));

        Assert.True(await harness.Consumed.Any<DoacaoRecebidaEvent>());
        Assert.Equal(1, campanhas.ChamadasDeIncremento);
        Assert.Equal(125.50m, campanhas.TotalIncrementado);
        Assert.Equal(1, doacoes.MarcadasComoProcessadas);
    }

    /// <summary>
    /// O teste que a TEST-02 pede: a mesma doacao chega duas vezes e o
    /// incremento acontece uma vez so.
    ///
    /// Se a checagem de idempotencia for removida do consumer, o segundo
    /// consumo passa direto para o incremento e este teste falha - que e
    /// exatamente o criterio de aceite da issue.
    /// </summary>
    [Fact]
    public async Task Redelivery_da_mesma_doacao_nao_incrementa_duas_vezes()
    {
        var ledger = new LedgerFake();
        var campanhas = new CampanhaRepositorioFake();
        var doacoes = new DoacaoRepositorioFake();

        await using var provedor = Montar(ledger, campanhas, doacoes);
        var harness = provedor.GetRequiredService<ITestHarness>();
        await harness.Start();

        var idDoacao = Guid.CreateVersion7();

        await harness.Bus.Publish(Evento(idDoacao));
        Assert.True(await harness.Consumed.Any<DoacaoRecebidaEvent>());

        await harness.Bus.Publish(Evento(idDoacao));

        Assert.Equal(2, await harness.Consumed.SelectAsync<DoacaoRecebidaEvent>().Count());

        Assert.Equal(2, ledger.TentativasDeRegistro);
        Assert.Equal(1, campanhas.ChamadasDeIncremento);
        Assert.Equal(125.50m, campanhas.TotalIncrementado);
        Assert.Equal(1, doacoes.MarcadasComoProcessadas);
    }

    [Fact]
    public async Task Doacoes_diferentes_incrementam_cada_uma()
    {
        var ledger = new LedgerFake();
        var campanhas = new CampanhaRepositorioFake();
        var doacoes = new DoacaoRepositorioFake();

        await using var provedor = Montar(ledger, campanhas, doacoes);
        var harness = provedor.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(Evento(Guid.CreateVersion7(), 10m));
        await harness.Bus.Publish(Evento(Guid.CreateVersion7(), 15m));

        Assert.Equal(2, await harness.Consumed.SelectAsync<DoacaoRecebidaEvent>().Count());
        Assert.Equal(2, campanhas.ChamadasDeIncremento);
        Assert.Equal(25m, campanhas.TotalIncrementado);
    }

    /// <summary>
    /// MSG-07: campanha inexistente é falha permanente, não transitória.
    /// </summary>
    [Fact]
    public async Task Campanha_inexistente_falha_como_permanente()
    {
        var ledger = new LedgerFake();
        var campanhas = new CampanhaRepositorioFake { LinhasAfetadasPorIncremento = 0 };
        var doacoes = new DoacaoRepositorioFake();

        await using var provedor = Montar(ledger, campanhas, doacoes);
        var harness = provedor.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(Evento(Guid.CreateVersion7()));

        Assert.True(await harness.Consumed.Any<DoacaoRecebidaEvent>(
            x => x.Context.ReceiveContext.IsFaulted));

        var consumido = harness.Consumed.Select<DoacaoRecebidaEvent>().First();
        Assert.IsType<FalhaPermanenteException>(consumido.Exception);

        Assert.Equal(0, doacoes.MarcadasComoProcessadas);
    }
}
