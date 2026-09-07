using GestorONG.Domain.Enums;
using GestorONG.Domain.Exceptions;

namespace GestorONG.Domain.Entities;

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

    public void MarcarComoProcessada() => Status = StatusDoacao.Processada;

    public override string ToString() => $"Doacao({Id}, {Status})";
}
