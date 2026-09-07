using GestorONG.Application.Excecoes;
using GestorONG.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GestorONG.API.Middleware;

internal sealed class TratadorGlobalDeExcecoes(
    IProblemDetailsService problemDetailsService,
    ILogger<TratadorGlobalDeExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto,
        Exception excecao,
        CancellationToken cancellationToken)
    {
        var problema = Traduzir(excecao, contexto);

        if (problema.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(excecao, "Erro não tratado. TraceId {TraceId}", contexto.TraceIdentifier);
        }
        else
        {
            logger.LogInformation(
                "Requisição rejeitada: {Titulo}. TraceId {TraceId}",
                problema.Title,
                contexto.TraceIdentifier);
        }

        contexto.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excecao,
            ProblemDetails = problema
        });
    }

    private static ProblemDetails Traduzir(Exception excecao, HttpContext contexto)
    {
        switch (excecao)
        {
            case DomainValidationException dominio:
            {
                var problema = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Regra de negócio violada.",
                    Instance = contexto.Request.Path
                };

                problema.Extensions["violacoes"] = dominio.Violacoes
                    .GroupBy(v => v.Propriedade)
                    .ToDictionary(g => g.Key, g => g.Select(v => v.Mensagem).ToArray());

                return problema;
            }

            case ConflitoException conflito:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflito.",
                    Detail = conflito.Message,
                    Instance = contexto.Request.Path
                };

            case NaoEncontradoException naoEncontrado:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Recurso não encontrado.",
                    Detail = naoEncontrado.Message,
                    Instance = contexto.Request.Path
                };

            case CredenciaisInvalidasException:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Não autenticado.",
                    Detail = "E-mail ou senha inválidos.",
                    Instance = contexto.Request.Path
                };

            default:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Erro interno.",
                    Detail = "Ocorreu um erro ao processar a requisição.",
                    Instance = contexto.Request.Path
                };
        }
    }
}
