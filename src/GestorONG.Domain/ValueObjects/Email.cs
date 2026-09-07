using System.Diagnostics.CodeAnalysis;
using GestorONG.Domain.Exceptions;

namespace GestorONG.Domain.ValueObjects;

/// <summary>
/// Endereço de e-mail normalizado.
/// <para>
/// A normalização não é cosmética: o e-mail é único no banco (DATA-02) e, sem
/// ela, <c>Ana@X.com</c> e <c>ana@x.com</c> viram dois usuários distintos.
/// </para>
/// </summary>
public sealed record Email
{
    private const int TamanhoMaximo = 254;
    private const int TamanhoMaximoParteLocal = 64;

    /// <summary>Valor normalizado: sem espaços nas pontas e em minúsculas.</summary>
    public string Valor { get; }

    private Email(string valor) => Valor = valor;

    /// <summary>Cria um e-mail ou lança <see cref="DomainValidationException"/>.</summary>
    public static Email Criar(string? entrada)
    {
        if (!TryParse(entrada, out var email))
        {
            throw new DomainValidationException(nameof(Email), "E-mail inválido.");
        }

        return email;
    }

    /// <summary>Tenta criar um e-mail sem lançar exceção.</summary>
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

    /// <summary>
    /// Validação pragmática: cobre o que aparece em cadastro real sem tentar
    /// implementar a RFC 5322 inteira, que aceita coisas que nenhum provedor usa.
    /// </summary>
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

        // Precisa de parte local antes do @ e de exatamente um @.
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

        // O domínio precisa de um ponto e de um TLD com pelo menos 2 caracteres,
        // o que rejeita "ana@localhost" e "ana@x.c".
        var ultimoPonto = dominio.LastIndexOf('.');
        if (ultimoPonto <= 0)
        {
            return false;
        }

        return dominio.Length - ultimoPonto - 1 >= 2;
    }
}
