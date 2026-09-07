using GestorONG.Domain.Enums;
using GestorONG.Domain.Exceptions;

namespace GestorONG.Domain.Entities;

/// <summary>
/// Campanha de arrecadação.
/// <para>
/// <see cref="ValorArrecadado"/> é materializado, não calculado a cada leitura,
/// e o Worker é o único componente que o escreve — a API publica o evento e não
/// toca no valor. Ver <c>docs/BACKLOG.md</c>, risco R4.
/// </para>
/// </summary>
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

    /// <summary>
    /// Total já arrecadado. Sem setter público: a única forma de alterá-lo é
    /// <see cref="RegistrarArrecadacao"/>, chamado exclusivamente pelo Worker.
    /// </summary>
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

    /// <summary>
    /// Cria uma campanha aplicando as regras do edital.
    /// <para>
    /// O <paramref name="tempo"/> é injetado de propósito. Comparar contra
    /// <c>DateTime.Now</c> dentro de um contêiner em UTC produz um bug que só
    /// aparece em produção, e que teste nenhum pega. Ver risco R5.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Indica se a campanha aceita doações neste momento: precisa estar
    /// <see cref="StatusCampanha.Ativa"/> e ainda vigente.
    /// </summary>
    public bool EstaAbertaParaDoacao(TimeProvider tempo)
    {
        ArgumentNullException.ThrowIfNull(tempo);
        return Status == StatusCampanha.Ativa && DataFim >= tempo.GetUtcNow();
    }

    /// <summary>
    /// Soma um valor ao total arrecadado.
    /// <para>
    /// <b>Chamado apenas pelo Worker</b>, ao consumir um
    /// <c>DoacaoRecebidaEvent</c>. A API não invoca este método — ela publica o
    /// evento e devolve 202. Ver risco R4.
    /// </para>
    /// <para>
    /// A campanha <b>não</b> transita para <see cref="StatusCampanha.Concluida"/>
    /// ao atingir a meta: encerrar sozinha recusaria doações que a ONG quer
    /// receber. Ver <c>docs/adr/0001-transicao-ao-atingir-a-meta.md</c>.
    /// </para>
    /// </summary>
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

    /// <summary>Percentual da meta já atingido. Usado pelo painel público (PUB-03).</summary>
    public decimal PercentualAtingido() =>
        MetaFinanceira <= decimal.Zero
            ? decimal.Zero
            : ValorArrecadado / MetaFinanceira * 100m;

    public override string ToString() => $"Campanha({Id}, {Status})";
}
