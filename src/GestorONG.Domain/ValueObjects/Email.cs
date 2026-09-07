using System.Diagnostics.CodeAnalysis;
using GestorONG.Domain.Exceptions;

namespace GestorONG.Domain.ValueObjects;

public sealed record Email
{
    private const int TamanhoMaximo = 254;
    private const int TamanhoMaximoParteLocal = 64;

    public string Valor { get; }

    private Email(string valor) => Valor = valor;

    public static Email Criar(string? entrada)
    {
        if (!TryParse(entrada, out var email))
        {
            throw new DomainValidationException(nameof(Email), "E-mail inválido.");
        }

        return email;
    }

    public static bool TryParse(string? entrada, [NotNullWhen(true)] out Email? email)
    {
        email = null;

        if (string.IsNullOrWhiteSpace(entrada))
        {
            return false;
        }

        var normalizado = entrada.Trim().ToLowerInvariant();
        if (!EhValido(normalizado))
        {
            return false;
        }

        email = new Email(normalizado);
        return true;
    }

    public override string ToString() => Valor;

    private static bool EhValido(string valor)
    {
        if (valor.Length > TamanhoMaximo)
        {
            return false;
        }

        if (valor.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (valor.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        var arroba = valor.IndexOf('@', StringComparison.Ordinal);

        if (arroba <= 0 || arroba != valor.LastIndexOf('@'))
        {
            return false;
        }

        var parteLocal = valor[..arroba];
        var dominio = valor[(arroba + 1)..];

        if (parteLocal.Length > TamanhoMaximoParteLocal)
        {
            return false;
        }

        if (parteLocal.StartsWith('.') || parteLocal.EndsWith('.'))
        {
            return false;
        }

        if (dominio.StartsWith('.') || dominio.EndsWith('.'))
        {
            return false;
        }

        var ultimoPonto = dominio.LastIndexOf('.');
        if (ultimoPonto <= 0)
        {
            return false;
        }

        return dominio.Length - ultimoPonto - 1 >= 2;
    }
}
