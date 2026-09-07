using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GestorONG.Infrastructure.Observabilidade;

public static class EndpointsDeObservabilidade
{
    public static IEndpointRouteBuilder MapearObservabilidade(this IEndpointRouteBuilder rotas)
    {
        rotas.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = EscreverResposta
        }).AllowAnonymous();

        rotas.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = verificacao => verificacao.Tags.Contains("ready"),
            ResponseWriter = EscreverResposta
        }).AllowAnonymous();

        rotas.MapPrometheusScrapingEndpoint("/metrics").AllowAnonymous();

        return rotas;
    }

    private static async Task EscreverResposta(HttpContext contexto, HealthReport relatorio)
    {
        contexto.Response.ContentType = "application/json; charset=utf-8";

        var corpo = new
        {
            status = relatorio.Status.ToString(),
            duracaoMs = relatorio.TotalDuration.TotalMilliseconds,
            dependencias = relatorio.Entries.ToDictionary(
                entrada => entrada.Key,
                entrada => new
                {
                    status = entrada.Value.Status.ToString(),
                    duracaoMs = entrada.Value.Duration.TotalMilliseconds,
                    erro = entrada.Value.Exception?.Message
                })
        };

        await contexto.Response.WriteAsync(JsonSerializer.Serialize(corpo));
    }
}
