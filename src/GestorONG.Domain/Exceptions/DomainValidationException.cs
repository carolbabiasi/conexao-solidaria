namespace GestorONG.Domain.Exceptions;

public sealed record Violacao(string Propriedade, string Mensagem);

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
