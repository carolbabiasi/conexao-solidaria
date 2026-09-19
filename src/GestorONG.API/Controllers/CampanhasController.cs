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
    [EndpointSummary("Cria uma campanha")]
    [EndpointDescription(
        "Exclusivo do perfil GestorONG: um token de Doador recebe 403 aqui. " +
        "A data de término não pode estar no passado nem antes da data de início, " +
        "e a meta precisa ser maior que zero — as três violações voltam juntas em " +
        "um único 400, não uma por vez. " +
        "Atenção ao campo status: no envio ele é numérico (1 Ativa, 2 Concluída, " +
        "3 Cancelada), mas a resposta devolve o nome. Reenviar uma resposta sem " +
        "converter esse campo resulta em 400.")]
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
    [EndpointSummary("Consulta uma campanha pelo id")]
    [EndpointDescription(
        "Visão do gestor, com descrição e datas. O painel público expõe outra " +
        "projeção da mesma campanha, sem autenticação, em GET /api/v1/publico/campanhas.")]
    [ProducesResponseType<CampanhaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
    [EndpointSummary("Lista todas as campanhas")]
    [EndpointDescription(
        "Inclui campanhas de qualquer status — ativas, concluídas e canceladas.")]
    [ProducesResponseType<IReadOnlyList<CampanhaResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
