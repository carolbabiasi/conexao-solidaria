using System.Diagnostics.Metrics;

namespace GestorONG.Infrastructure.Observabilidade;

public sealed class MetricasDeNegocio : IDisposable
{
    public const string NomeDoMeter = "GestorONG.Negocio";

    private readonly Meter _meter;

    public MetricasDeNegocio(IMeterFactory fabrica)
    {
        _meter = fabrica.Create(NomeDoMeter);

        DoacoesRecebidas = _meter.CreateCounter<long>(
            "doacoes_recebidas_total",
            description: "Intencoes de doacao aceitas pela API.");

        DoacoesProcessadas = _meter.CreateCounter<long>(
            "doacoes_processadas_total",
            description: "Doacoes efetivamente somadas a campanha pelo Worker.");

        DoacoesDuplicadasDescartadas = _meter.CreateCounter<long>(
            "doacoes_duplicadas_descartadas_total",
            description: "Redeliveries descartadas pela verificacao de idempotencia.");

        ValorDoado = _meter.CreateCounter<double>(
            "doacoes_valor_total",
            unit: "BRL",
            description: "Soma dos valores doados.");

        DuracaoDoProcessamento = _meter.CreateHistogram<double>(
            "doacao_processamento_duracao_seconds",
            unit: "s",
            description: "Tempo de processamento de uma doacao pelo Worker.");
    }

    public Counter<long> DoacoesRecebidas { get; }

    public Counter<long> DoacoesProcessadas { get; }

    public Counter<long> DoacoesDuplicadasDescartadas { get; }

    public Counter<double> ValorDoado { get; }

    public Histogram<double> DuracaoDoProcessamento { get; }

    public void Dispose() => _meter.Dispose();
}
