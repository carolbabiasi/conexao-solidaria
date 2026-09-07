using System.ComponentModel.DataAnnotations;

namespace GestorONG.API.Contratos;

public sealed record RegistrarDoadorRequest
{
    [Required]
    [MaxLength(200)]
    public string NomeCompleto { get; init; } = string.Empty;

    [Required]
    [MaxLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Cpf { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(72)]
    public string Senha { get; init; } = string.Empty;
}

public sealed record UsuarioResponse(Guid Id, string NomeCompleto, string Email, string Role);

public sealed record LoginRequest
{
    [Required]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Senha { get; init; } = string.Empty;
}

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiraEm, string Role);
