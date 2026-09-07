using System.ComponentModel.DataAnnotations;
using GestorONG.Domain.Enums;

namespace GestorONG.API.Contratos;

public sealed record CriarCampanhaRequest
{
    [Required]
    [MaxLength(200)]
    public string Titulo { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Descricao { get; init; } = string.Empty;

    [Required]
    public DateTimeOffset DataInicio { get; init; }

    [Required]
    public DateTimeOffset DataFim { get; init; }

    [Required]
    public decimal MetaFinanceira { get; init; }

    public StatusCampanha Status { get; init; } = StatusCampanha.Ativa;
}

public sealed record CampanhaResponse(
    Guid Id,
    string Titulo,
    string Descricao,
    DateTimeOffset DataInicio,
    DateTimeOffset DataFim,
    decimal MetaFinanceira,
    decimal ValorArrecadado,
    string Status);
