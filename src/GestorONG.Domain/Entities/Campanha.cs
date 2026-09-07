using GestorONG.Domain.Enums;
using GestorONG.Domain.Exceptions;

namespace GestorONG.Domain.Entities;

public sealed class Campanha
{
    private const int TamanhoMaximoTitulo = 200;
    private const int TamanhoMaximoDescricao = 2000;

    public Guid Id { get; private set; }
    public string Titulo { get; private set; }
    public string Descricao { get; private set; }
    public DateTimeOffset DataInicio { get; private set; }
    public DateTimeOffset DataFim { get; private set; }
    public decimal MetaFinanceira { get; private set; }
    public StatusCampanha Status { get; private set; }
    public decimal ValorArrecadado { get; private set; }

    private Campanha(
        Guid id,
        string titulo,
        string descricao,
        DateTimeOffset dataInicio,
        DateTimeOffset dataFim,
        decimal metaFinanceira,
        StatusCampanha status)
    {
        Id = id;
        Titulo = titulo;
        Descricao = descricao;
        DataInicio = dataInicio;
        DataFim = dataFim;
        MetaFinanceira = metaFinanceira;
        Status = status;
        ValorArrecadado = decimal.Zero;
    }

    public static Campanha Criar(
        string? titulo,
        string? descricao,
        DateTimeOffset dataInicio,
        DateTimeOffset dataFim,
        decimal metaFinanceira,
        TimeProvider tempo,
        StatusCampanha status = StatusCampanha.Ativa)
    {
        ArgumentNullException.ThrowIfNull(tempo);

        var violacoes = new List<Violacao>();

        if (string.IsNullOrWhiteSpace(titulo))
        {
            violacoes.Add(new Violacao(nameof(Titulo), "Título é obrigatório."));
        }
        else if (titulo.Trim().Length > TamanhoMaximoTitulo)
        {
            violacoes.Add(new Violacao(
                nameof(Titulo),
                $"Título deve ter no máximo {TamanhoMaximoTitulo} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            violacoes.Add(new Violacao(nameof(Descricao), "Descrição é obrigatória."));
        }
        else if (descricao.Trim().Length > TamanhoMaximoDescricao)
        {
            violacoes.Add(new Violacao(
                nameof(Descricao),
                $"Descrição deve ter no máximo {TamanhoMaximoDescricao} caracteres."));
        }

        if (metaFinanceira <= decimal.Zero)
        {
            violacoes.Add(new Violacao(
                nameof(MetaFinanceira),
                "Meta financeira deve ser maior que zero."));
        }

        if (dataFim <= dataInicio)
        {
            violacoes.Add(new Violacao(
                nameof(DataFim),
                "Data de término deve ser posterior à data de início."));
        }

        if (dataFim < tempo.GetUtcNow())
        {
            violacoes.Add(new Violacao(
                nameof(DataFim),
                "Data de término não pode estar no passado."));
        }

        if (!Enum.IsDefined(status))
        {
            violacoes.Add(new Violacao(nameof(Status), "Status inválido."));
        }

        if (violacoes.Count > 0)
        {
            throw new DomainValidationException(violacoes);
        }

        return new Campanha(
            Guid.CreateVersion7(),
            titulo!.Trim(),
            descricao!.Trim(),
            dataInicio,
            dataFim,
            metaFinanceira,
            status);
    }

    public bool EstaAbertaParaDoacao(TimeProvider tempo)
    {
        ArgumentNullException.ThrowIfNull(tempo);
        return Status == StatusCampanha.Ativa && DataFim >= tempo.GetUtcNow();
    }

    public void RegistrarArrecadacao(decimal valor)
    {
        if (valor <= decimal.Zero)
        {
            throw new DomainValidationException(
                nameof(valor),
                "Valor da arrecadação deve ser maior que zero.");
        }

        ValorArrecadado += valor;
    }

    public decimal PercentualAtingido() =>
        MetaFinanceira <= decimal.Zero
            ? decimal.Zero
            : ValorArrecadado / MetaFinanceira * 100m;

    public override string ToString() => $"Campanha({Id}, {Status})";
}
