using GestorONG.Application.Abstracoes;
using GestorONG.Contracts.V1;
using GestorONG.Infrastructure.Observabilidade;
using MassTransit;

namespace GestorONG.Worker.Consumidores;

public sealed class DoacaoRecebidaConsumer(
    IDoacaoLedgerRepository ledger,
    ICampanhaRepository campanhas,
    IDoacaoRepository doacoes,
    TimeProvider tempo,
    MetricasDeNegocio metricas,
    ILogger<DoacaoRecebidaConsumer> logger) : IConsumer<DoacaoRecebidaEvent>
{
    public async Task Consume(ConsumeContext<DoacaoRecebidaEvent> contexto)
    {
        var evento = contexto.Message;
        var cancellationToken = contexto.CancellationToken;
        var inicio = tempo.GetTimestamp();

        var primeiraVez = await ledger.TentarRegistrarAsync(
            evento.IdDoacao,
            evento.IdCampanha,
            evento.IdDoador,
            evento.Valor,
            tempo.GetUtcNow(),
            cancellationToken);

        if (!primeiraVez)
        {
            metricas.DoacoesDuplicadasDescartadas.Add(1);

            logger.LogInformation(
                "Doacao {IdDoacao} ja processada. Redelivery descartada sem incrementar.",
                evento.IdDoacao);
            return;
        }

        var linhasAfetadas = await campanhas.IncrementarArrecadadoAsync(
            evento.IdCampanha,
            evento.Valor,
            cancellationToken);

        if (linhasAfetadas == 0)
        {
            logger.LogError(
                "Campanha {IdCampanha} nao encontrada para a doacao {IdDoacao}.",
                evento.IdCampanha,
                evento.IdDoacao);

            throw new InvalidOperationException(
                $"Campanha {evento.IdCampanha} não encontrada.");
        }

        await doacoes.MarcarComoProcessadaAsync(evento.IdDoacao, cancellationToken);

        var campanha = await campanhas.ObterPorIdAsync(evento.IdCampanha, cancellationToken);

        metricas.DoacoesProcessadas.Add(1);
        metricas.ValorDoado.Add((double)evento.Valor);
        metricas.DuracaoDoProcessamento.Record(
            tempo.GetElapsedTime(inicio).TotalSeconds);

        logger.LogInformation(
            "Doacao {IdDoacao} processada. Campanha {IdCampanha} recebeu {Valor}. Total agora: {Total}.",
            evento.IdDoacao,
            evento.IdCampanha,
            evento.Valor,
            campanha?.ValorArrecadado);
    }
}
