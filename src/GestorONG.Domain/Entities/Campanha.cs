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

    private Campanha()
    {
        Titulo = null!;
        Descricao = null!;
    }

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

        var violacoes = ValidarCampos(titulo, descricao, dataInicio, dataFim, metaFinanceira, tempo);

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

    /// <summary>
    /// Edição feita pelo gestor. Revalida as mesmas regras da criação, e mais
    /// duas que só existem aqui.
    ///
    /// Campanha que já recebeu doação tem <see cref="MetaFinanceira"/> e
    /// <see cref="DataInicio"/> congelados: quem doou decidiu com base na meta
    /// e no período anunciados, e mexer neles depois reescreveria o combinado
    /// retroativamente. O título, a descrição e a data de término continuam
    /// editáveis — prorrogar uma campanha e corrigir um texto não traem quem
    /// já doou. Ver ADR 0006.
    ///
    /// <see cref="ValorArrecadado"/> não é parâmetro de propósito: não existe
    /// caminho pela API que o altere. Só o Worker escreve esse valor, via
    /// <see cref="RegistrarArrecadacao"/>.
    /// </summary>
    public void Atualizar(
        string? titulo,
        string? descricao,
        DateTimeOffset dataInicio,
        DateTimeOffset dataFim,
        decimal metaFinanceira,
        TimeProvider tempo)
    {
        ArgumentNullException.ThrowIfNull(tempo);

        var violacoes = ValidarCampos(titulo, descricao, dataInicio, dataFim, metaFinanceira, tempo);

        if (Status != StatusCampanha.Ativa)
        {
            violacoes.Add(new Violacao(
                nameof(Status),
                "Só campanhas ativas podem ser editadas."));
        }

        if (JaRecebeuDoacao)
        {
            if (metaFinanceira != MetaFinanceira)
            {
                violacoes.Add(new Violacao(
                    nameof(MetaFinanceira),
                    "Meta financeira não pode mudar depois da primeira doação."));
            }

            if (dataInicio != DataInicio)
            {
                violacoes.Add(new Violacao(
                    nameof(DataInicio),
                    "Data de início não pode mudar depois da primeira doação."));
            }
        }

        if (violacoes.Count > 0)
        {
            throw new DomainValidationException(violacoes);
        }

        Titulo = titulo!.Trim();
        Descricao = descricao!.Trim();
        DataInicio = dataInicio;
        DataFim = dataFim;
        MetaFinanceira = metaFinanceira;
    }

    /// <summary>
    /// Transições permitidas: <c>Ativa → Concluida</c> e <c>Ativa → Cancelada</c>.
    ///
    /// Concluída e cancelada são estados finais. Reabrir uma campanha cancelada
    /// faria o painel público voltar a exibi-la e voltaria a aceitar doações —
    /// e quem tentou doar enquanto ela estava fechada já recebeu a recusa.
    /// </summary>
    public void AlterarStatus(StatusCampanha novoStatus)
    {
        if (!Enum.IsDefined(novoStatus))
        {
            throw new DomainValidationException(nameof(Status), "Status inválido.");
        }

        if (novoStatus == Status)
        {
            throw new TransicaoDeStatusInvalidaException(Status, novoStatus);
        }

        var permitida =
            Status == StatusCampanha.Ativa &&
            novoStatus is StatusCampanha.Concluida or StatusCampanha.Cancelada;

        if (!permitida)
        {
            throw new TransicaoDeStatusInvalidaException(Status, novoStatus);
        }

        Status = novoStatus;
    }

    public bool JaRecebeuDoacao => ValorArrecadado > decimal.Zero;

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

    private static List<Violacao> ValidarCampos(
        string? titulo,
        string? descricao,
        DateTimeOffset dataInicio,
        DateTimeOffset dataFim,
        decimal metaFinanceira,
        TimeProvider tempo)
    {
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

        return violacoes;
    }
}
