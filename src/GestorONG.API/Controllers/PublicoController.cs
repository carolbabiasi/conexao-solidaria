using GestorONG.Application.Abstracoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorONG.API.Controllers;

public sealed record CampanhaPublicaResponse(
    Guid Id,
    string Titulo,
    decimal MetaFinanceira,
    decimal ValorArrecadado,
    decimal PercentualAtingido);

[ApiController]
[Route("api/v1/publico")]
[Tags("Publico")]
[AllowAnonymous]
public sealed class PublicoController(ICampanhaRepository campanhas) : ControllerBase
{
    [HttpGet("campanhas")]
    [ProducesResponseType<IReadOnlyList<CampanhaPublicaResponse>>(StatusCodes.Status200OK)]
    [ResponseCache(Duration = 5)]
    public async Task<ActionResult<IReadOnlyList<CampanhaPublicaResponse>>> ListarAtivas(
        CancellationToken cancellationToken)
    {
        var ativas = await campanhas.ListarAtivasAsync(cancellationToken);

        var resposta = ativas
            .Select(c => new CampanhaPublicaResponse(
                c.Id,
                c.Titulo,
                c.MetaFinanceira,
                c.ValorArrecadado,
                Math.Round(c.PercentualAtingido(), 2)))
            .ToList();

        return Ok(resposta);
    }
}
