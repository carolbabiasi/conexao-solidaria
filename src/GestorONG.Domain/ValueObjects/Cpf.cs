using System.Diagnostics.CodeAnalysis;
using GestorONG.Domain.Exceptions;

namespace GestorONG.Domain.ValueObjects;

public sealed record Cpf
{
    private const int Tamanho = 11;

    public string Valor { get; }

    private Cpf(string valor) => Valor = valor;

    public static Cpf Criar(string? entrada)
    {
        if (!TryParse(entrada, out var cpf))
        {
            throw new DomainValidationException(nameof(Cpf), "CPF inválido.");
        }

        return cpf;
    }

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

    public string Formatado() =>
        $"{Valor[..3]}.{Valor[3..6]}.{Valor[6..9]}-{Valor[9..]}";

    public string Mascarado() => $"***.***.***-{Valor[9..]}";

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
