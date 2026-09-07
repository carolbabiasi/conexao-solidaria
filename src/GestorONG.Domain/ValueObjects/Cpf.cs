using System.Diagnostics.CodeAnalysis;
using GestorONG.Domain.Exceptions;

namespace GestorONG.Domain.ValueObjects;

/// <summary>
/// CPF validado pelos dois dígitos verificadores.
/// Armazena somente dígitos; formatação é responsabilidade da apresentação.
/// </summary>
public sealed record Cpf
{
    private const int Tamanho = 11;

    /// <summary>Os 11 dígitos, sem pontuação. É este valor que vai para o banco.</summary>
    public string Valor { get; }

    private Cpf(string valor) => Valor = valor;

    /// <summary>Cria um CPF ou lança <see cref="DomainValidationException"/>.</summary>
    public static Cpf Criar(string? entrada)
    {
        if (!TryParse(entrada, out var cpf))
        {
            throw new DomainValidationException(nameof(Cpf), "CPF inválido.");
        }

        return cpf;
    }

    /// <summary>Tenta criar um CPF sem lançar exceção.</summary>
    public static bool TryParse(string? entrada, [NotNullWhen(true)] out Cpf? cpf)
    {
        cpf = null;

        var digitos = SomenteDigitos(entrada);
        if (!EhValido(digitos))
        {
            return false;
        }

        cpf = new Cpf(digitos);
        return true;
    }

    /// <summary>Formato de exibição: 000.000.000-00.</summary>
    public string Formatado() =>
        $"{Valor[..3]}.{Valor[3..6]}.{Valor[6..9]}-{Valor[9..]}";

    /// <summary>
    /// Formato mascarado, preservando apenas os dois últimos dígitos.
    /// </summary>
    public string Mascarado() => $"***.***.***-{Valor[9..]}";

    /// <summary>
    /// Mascarado de propósito: CPF é dado pessoal e <c>ToString</c> é o caminho
    /// por onde ele vaza para log estruturado sem ninguém perceber. Quem precisa
    /// do valor real usa <see cref="Valor"/> explicitamente.
    /// </summary>
    public override string ToString() => Mascarado();

    private static string SomenteDigitos(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
        {
            return string.Empty;
        }

        var buffer = new char[entrada.Length];
        var tamanho = 0;

        foreach (var caractere in entrada)
        {
            if (char.IsAsciiDigit(caractere))
            {
                buffer[tamanho++] = caractere;
            }
        }

        return new string(buffer, 0, tamanho);
    }

    private static bool EhValido(string digitos)
    {
        if (digitos.Length != Tamanho)
        {
            return false;
        }

        // 000.000.000-00, 111.111.111-11 e afins passam no cálculo dos dígitos
        // verificadores, mas não são CPFs válidos.
        if (TodosDigitosIguais(digitos))
        {
            return false;
        }

        return DigitoVerificador(digitos, 9) == digitos[9] - '0'
            && DigitoVerificador(digitos, 10) == digitos[10] - '0';
    }

    private static bool TodosDigitosIguais(string digitos)
    {
        for (var i = 1; i < digitos.Length; i++)
        {
            if (digitos[i] != digitos[0])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Soma os primeiros <paramref name="quantidade"/> dígitos com pesos
    /// decrescentes a partir de <c>quantidade + 1</c>, e deriva o verificador
    /// do resto da divisão por 11.
    /// </summary>
    private static int DigitoVerificador(string digitos, int quantidade)
    {
        var soma = 0;
        var peso = quantidade + 1;

        for (var i = 0; i < quantidade; i++)
        {
            soma += (digitos[i] - '0') * peso--;
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
