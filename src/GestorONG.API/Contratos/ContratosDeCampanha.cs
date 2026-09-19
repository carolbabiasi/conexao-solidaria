using System.ComponentModel.DataAnnotations;
using GestorONG.Domain.Enums;

namespace GestorONG.API.Contratos;

// Os campos obrigatorios de tipo valor sao anulaveis de proposito. Um
// DateTimeOffset nao anulavel omitido no corpo chega como default, passa pelo
// [Required] - porque tecnicamente tem valor - e so falha la no dominio, com
// uma mensagem sobre data no passado em vez de "campo obrigatorio". Anulavel,
// a ausencia vira o 400 correto, apontando o campo que faltou.
public sealed record CriarCampanhaRequest
{
    [Required]
    [MaxLength(200)]
    public string Titulo { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Descricao { get; init; } = string.Empty;

    [Required]
    public DateTimeOffset? DataInicio { get; init; }

    [Required]
    public DateTimeOffset? DataFim { get; init; }

    [Required]
    public decimal? MetaFinanceira { get; init; }

    public StatusCampanha Status { get; init; } = StatusCampanha.Ativa;
}

public sealed record AtualizarCampanhaRequest
{
    [Required]
    [MaxLength(200)]
    public string Titulo { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Descricao { get; init; } = string.Empty;

    [Required]
    public DateTimeOffset? DataInicio { get; init; }

    [Required]
    public DateTimeOffset? DataFim { get; init; }

    [Required]
    public decimal? MetaFinanceira { get; init; }
}

public sealed record AlterarStatusRequest
{
    [Required]
    public StatusCampanha? Status { get; init; }
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

/// <summary>
/// Envelope de pagina. Traz totalItems e totalPages porque sem eles quem
/// consome nao sabe se chegou ao fim - so descobriria pedindo mais uma pagina
/// e recebendo vazio.
/// </summary>
public sealed record PaginaResponse<T>(
    IReadOnlyList<T> Itens,
    int Pagina,
    int TamanhoPagina,
    int TotalItems,
    int TotalPages);
