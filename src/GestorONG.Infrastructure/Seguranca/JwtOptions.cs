using System.ComponentModel.DataAnnotations;

namespace GestorONG.Infrastructure.Seguranca;

public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    [Required]
    [MinLength(32)]
    public string ChaveSecreta { get; init; } = string.Empty;

    [Required]
    public string Emissor { get; init; } = string.Empty;

    [Required]
    public string Audiencia { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiracaoMinutos { get; init; } = 60;
}
