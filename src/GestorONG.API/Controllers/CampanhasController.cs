using GestorONG.API.Contratos;
using GestorONG.Application.Abstracoes;
using GestorONG.Application.Excecoes;
using GestorONG.Domain.Entities;
using GestorONG.Domain.Enums;
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
    /// <summary>Teto do tamanho de página. Um pageSize absurdo é limitado, não recusado.</summary>
    private const int TamanhoMaximoDePagina = 100;

    private const int TamanhoPadraoDePagina = 20;

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
            requisicao.DataInicio!.Value,
            requisicao.DataFim!.Value,
            requisicao.MetaFinanceira!.Value,
            tempo,
            requisicao.Status);

        await campanhas.AdicionarAsync(campanha, cancellationToken);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return CreatedAtAction(nameof(ObterPorId), new { id = campanha.Id }, Mapear(campanha));
    }

    [HttpPut("{id:guid}")]
    [EndpointSummary("Edita uma campanha")]
    [EndpointDescription(
        "Só campanhas ativas podem ser editadas. " +
        "Depois da primeira doação, meta financeira e data de início ficam " +
        "congeladas: quem doou decidiu com base nos números anunciados. " +
        "Título, descrição e data de término continuam editáveis. " +
        "Não existe caminho por aqui que altere o valor arrecadado — só o Worker escreve esse campo.")]
    [ProducesResponseType<CampanhaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampanhaResponse>> Atualizar(
        Guid id,
        AtualizarCampanhaRequest requisicao,
        CancellationToken cancellationToken)
    {
        var campanha = await campanhas.ObterPorIdAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Campanha não encontrada.");

        campanha.Atualizar(
            requisicao.Titulo,
            requisicao.Descricao,
            requisicao.DataInicio!.Value,
            requisicao.DataFim!.Value,
            requisicao.MetaFinanceira!.Value,
            tempo);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return Ok(Mapear(campanha));
    }

    [HttpPatch("{id:guid}/status")]
    [EndpointSummary("Conclui ou cancela uma campanha")]
    [EndpointDescription(
        "Transições permitidas: Ativa para Concluída e Ativa para Cancelada. " +
        "Concluída e cancelada são estados finais — reabrir devolve 409, porque " +
        "a campanha voltaria ao painel público e voltaria a aceitar doações " +
        "depois de já ter recusado alguém. " +
        "Cancelar faz a campanha sumir do painel público imediatamente.")]
    [ProducesResponseType<CampanhaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CampanhaResponse>> AlterarStatus(
        Guid id,
        AlterarStatusRequest requisicao,
        CancellationToken cancellationToken)
    {
        var campanha = await campanhas.ObterPorIdAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Campanha não encontrada.");

        campanha.AlterarStatus(requisicao.Status!.Value);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return Ok(Mapear(campanha));
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
    [EndpointSummary("Lista campanhas com paginação")]
    [EndpointDescription(
        "Diferente do painel público: o gestor vê campanhas de todos os status. " +
        "Use status para filtrar (1 Ativa, 2 Concluída, 3 Cancelada). " +
        "pageSize é limitado a 100 — um valor acima disso é reduzido ao teto, " +
        "não recusado, para um cliente distraído não derrubar a listagem.")]
    [ProducesResponseType<PaginaResponse<CampanhaResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginaResponse<CampanhaResponse>>> Listar(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = TamanhoPadraoDePagina,
        [FromQuery] StatusCampanha? status = null)
    {
        if (status is not null && !Enum.IsDefined(status.Value))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["status"] = ["Status inválido. Use 1 Ativa, 2 Concluída ou 3 Cancelada."]
            }));
        }

        // Valor fora da faixa e corrigido, e nao recusado: paginacao e detalhe
        // de transporte, e derrubar a listagem por causa de um pageSize
        // distraido nao ajuda ninguem.
        var pagina = Math.Max(page, 1);
        var tamanho = Math.Clamp(pageSize, 1, TamanhoMaximoDePagina);

        var (itens, totalItens) = await campanhas.ListarPaginadoAsync(
            pagina,
            tamanho,
            status,
            cancellationToken);

        var totalPaginas = (int)Math.Ceiling(totalItens / (double)tamanho);

        return Ok(new PaginaResponse<CampanhaResponse>(
            itens.Select(Mapear).ToList(),
            pagina,
            tamanho,
            totalItens,
            totalPaginas));
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
