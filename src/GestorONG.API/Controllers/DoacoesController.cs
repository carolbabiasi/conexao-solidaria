using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using GestorONG.Application.Abstracoes;
using GestorONG.Application.Excecoes;
using GestorONG.Contracts.V1;
using GestorONG.Domain.Entities;
using GestorONG.Infrastructure.Observabilidade;
using GestorONG.Infrastructure.Seguranca;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorONG.API.Controllers;

public sealed record CriarDoacaoRequest
{
    [Required]
    public Guid? IdCampanha { get; init; }

    [Required]
    public decimal? ValorDoacao { get; init; }
}

public sealed record DoacaoAceitaResponse(Guid IdDoacao, string Status, string Mensagem);

[ApiController]
[Route("api/v1/doacoes")]
[Tags("Doacoes")]
[Authorize(Policy = ClaimsGestorONG.Politica_Doador)]
public sealed class DoacoesController(
    ICampanhaRepository campanhas,
    IDoacaoRepository doacoes,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publicador,
    MetricasDeNegocio metricas,
    TimeProvider tempo) : ControllerBase
{
    [HttpPost]
    [EndpointSummary("Registra a intenção de doação")]
    [EndpointDescription(
        "Responde 202 Accepted, não 201: a API grava a intenção e publica o evento " +
        "na mesma transação, mas NÃO atualiza o valor arrecadado da campanha. " +
        "Quem soma é o Worker, ao consumir a fila. Por isso o painel público leva " +
        "alguns segundos para refletir o valor — e por isso doar duas vezes a mesma " +
        "mensagem não infla o total: o ledger tem índice único por doação. " +
        "Troque o idCampanha do exemplo pelo id devolvido ao criar a campanha: " +
        "o valor de exemplo é fixo e não existe num banco recém-criado, onde " +
        "devolveria 404. Exige perfil Doador — um token de GestorONG recebe 403.")]
    [ProducesResponseType<DoacaoAceitaResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DoacaoAceitaResponse>> Doar(
        CriarDoacaoRequest requisicao,
        CancellationToken cancellationToken)
    {
        var idDoador = ObterIdDoDoadorAutenticado();

        var campanha = await campanhas.ObterPorIdAsync(requisicao.IdCampanha!.Value, cancellationToken)
            ?? throw new NaoEncontradoException("Campanha não encontrada.");

        var doacao = Doacao.Criar(campanha, idDoador, requisicao.ValorDoacao!.Value, tempo);

        await doacoes.AdicionarAsync(doacao, cancellationToken);

        await publicador.Publish(
            new DoacaoRecebidaEvent
            {
                IdDoacao = doacao.Id,
                IdCampanha = doacao.IdCampanha,
                IdDoador = doacao.IdDoador,
                Valor = doacao.Valor,
                OcorridoEm = doacao.DataCriacao
            },
            cancellationToken);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        metricas.DoacoesRecebidas.Add(1);

        return Accepted(new DoacaoAceitaResponse(
            doacao.Id,
            doacao.Status.ToString(),
            "Doação recebida. O valor da campanha será atualizado em instantes."));
    }

    private Guid ObterIdDoDoadorAutenticado()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        return Guid.TryParse(sub, out var id)
            ? id
            : throw new CredenciaisInvalidasException();
    }
}
