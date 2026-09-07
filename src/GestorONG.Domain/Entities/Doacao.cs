using GestorONG.Domain.Enums;
using GestorONG.Domain.Exceptions;

namespace GestorONG.Domain.Entities;

/// <summary>
/// Intenção de doação registrada pela API.
/// <para>
/// Nasce <see cref="StatusDoacao.Pendente"/>: o valor só entra na campanha
/// depois que o Worker consome o evento. É por isso que o endpoint responde
/// 202 Accepted, e não 201.
/// </para>
/// </summary>
public sealed class Doacao
{
    public Guid Id { get; private set; }
    public Guid IdCampanha { get; private set; }
    public Guid IdDoador { get; private set; }
    public decimal Valor { get; private set; }
    public DateTimeOffset DataCriacao { get; private set; }
    public StatusDoacao Status { get; private set; }

    private Doacao(
        Guid id,
        Guid idCampanha,
        Guid idDoador,
        decimal valor,
        DateTimeOffset dataCriacao)
    {
        Id = id;
        IdCampanha = idCampanha;
        IdDoador = idDoador;
        Valor = valor;
        DataCriacao = dataCriacao;
        Status = StatusDoacao.Pendente;
    }

    /// <summary>
    /// Registra a intenção de doação, validando a campanha de destino.
    /// <para>
    /// As mensagens distinguem cancelada, concluída e vencida de propósito:
    /// para quem está doando, "não foi possível doar" não explica nada.
    /// </para>
    /// </summary>
    public static Doacao Criar(Campanha campanha, Guid idDoador, decimal valor, TimeProvider tempo)
    {
        ArgumentNullException.ThrowIfNull(campanha);
        ArgumentNullException.ThrowIfNull(tempo);

        var violacoes = new List<Violacao>();

        if (idDoador == Guid.Empty)
        {
            violacoes.Add(new Violacao(nameof(IdDoador), "Doador é obrigatório."));
        }

        if (valor <= decimal.Zero)
        {
            violacoes.Add(new Violacao(nameof(Valor), "Valor da doação deve ser maior que zero."));
        }

        switch (campanha.Status)
        {
            case StatusCampanha.Cancelada:
                violacoes.Add(new Violacao(
                    nameof(IdCampanha),
                    "Campanha cancelada não aceita doações."));
                break;

            case StatusCampanha.Concluida:
                violacoes.Add(new Violacao(
                    nameof(IdCampanha),
                    "Campanha concluída não aceita doações."));
                break;

            case StatusCampanha.Ativa:
                // Uma campanha pode continuar marcada como Ativa mesmo depois da
                // data de término, porque nada a transiciona sozinha. A data é a
                // verdade, não o status.
                if (campanha.DataFim < tempo.GetUtcNow())
                {
                    violacoes.Add(new Violacao(
                        nameof(IdCampanha),
                        "Campanha encerrada não aceita doações."));
                }

                break;

            default:
                violacoes.Add(new Violacao(nameof(IdCampanha), "Status de campanha inválido."));
                break;
        }

        if (violacoes.Count > 0)
        {
            throw new DomainValidationException(violacoes);
        }

        return new Doacao(
            Guid.CreateVersion7(),
            campanha.Id,
            idDoador,
            valor,
            tempo.GetUtcNow());
    }

    /// <summary>
    /// Marca a doação como processada. Idempotente: o Worker consome de uma fila
    /// <i>at-least-once</i>, então a mesma doação pode chegar mais de uma vez e
    /// isso não pode ser um erro. Ver risco R2.
    /// </summary>
    public void MarcarComoProcessada() => Status = StatusDoacao.Processada;

    public override string ToString() => $"Doacao({Id}, {Status})";
}
