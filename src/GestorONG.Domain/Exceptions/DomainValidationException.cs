namespace GestorONG.Domain.Exceptions;

/// <summary>
/// Uma violação de regra de domínio, identificando a propriedade envolvida.
/// </summary>
public sealed record Violacao(string Propriedade, string Mensagem);

/// <summary>
/// Lançada quando uma regra de negócio é violada. Carrega a lista completa de
/// violações para que a borda consiga devolver todas de uma vez, em vez de
/// obrigar o cliente a descobrir os erros um a um.
/// </summary>
public sealed class DomainValidationException : Exception
{
    public IReadOnlyList<Violacao> Violacoes { get; }

    public DomainValidationException(string propriedade, string mensagem)
        : this([new Violacao(propriedade, mensagem)])
    {
    }

    public DomainValidationException(IReadOnlyList<Violacao> violacoes)
        : base(Descrever(violacoes))
    {
        Violacoes = violacoes;
    }

    private static string Descrever(IReadOnlyList<Violacao> violacoes) =>
        violacoes.Count == 0
            ? "Violação de regra de domínio."
            : string.Join(" ", violacoes.Select(v => $"{v.Propriedade}: {v.Mensagem}"));
}
