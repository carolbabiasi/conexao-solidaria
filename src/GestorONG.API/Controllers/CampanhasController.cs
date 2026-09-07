using GestorONG.API.Contratos;
using GestorONG.Application.Abstracoes;
using GestorONG.Application.Excecoes;
using GestorONG.Domain.Entities;
using GestorONG.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorONG.API.Controllers;

[ApiController]
[Route("api/v1/campanhas")]
[Tags("Campanhas")]
[Authorize(Policy = ClaimsGestorONG.Politica_Gestor)]
public sealed class CampanhasController(
    ICampanhaRepository campanhas,
    IUnitOfWork unitOfWork,
    TimeProvider tempo) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CampanhaResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CampanhaResponse>> Criar(
        CriarCampanhaRequest requisicao,
        CancellationToken cancellationToken)
    {
        var campanha = Campanha.Criar(
            requisicao.Titulo,
            requisicao.Descricao,
            requisicao.DataInicio,
            requisicao.DataFim,
            requisicao.MetaFinanceira,
            tempo,
            requisicao.Status);

        await campanhas.AdicionarAsync(campanha, cancellationToken);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return CreatedAtAction(nameof(ObterPorId), new { id = campanha.Id }, Mapear(campanha));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CampanhaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampanhaResponse>> ObterPorId(
        Guid id,
        CancellationToken cancellationToken)
    {
        var campanha = await campanhas.ObterPorIdAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Campanha não encontrada.");

        return Ok(Mapear(campanha));
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CampanhaResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CampanhaResponse>>> Listar(
        CancellationToken cancellationToken)
    {
        var lista = await campanhas.ListarTodasAsync(cancellationToken);
        return Ok(lista.Select(Mapear).ToList());
    }

    private static CampanhaResponse Mapear(Campanha campanha) => new(
        campanha.Id,
        campanha.Titulo,
        campanha.Descricao,
        campanha.DataInicio,
        campanha.DataFim,
        campanha.MetaFinanceira,
        campanha.ValorArrecadado,
        campanha.Status.ToString());
}
