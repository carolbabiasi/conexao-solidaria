using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GestorONG.API.Configuracao;

internal sealed class TransformadorDeSeguranca : IOpenApiDocumentTransformer
{
    private const string Esquema = "Bearer";

    public Task TransformAsync(
        OpenApiDocument documento,
        OpenApiDocumentTransformerContext contexto,
        CancellationToken cancellationToken)
    {
        documento.Info = new OpenApiInfo
        {
            Title = "Conexão Solidária",
            Version = "v1",
            Description =
                "API da plataforma da ONG Esperança Solidária.\n\n" +
                "Para usar os endpoints protegidos: chame POST /api/v1/auth/login, " +
                "copie o accessToken e clique em Authorize."
        };

        documento.Components ??= new OpenApiComponents();
        documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        documento.Components.SecuritySchemes[Esquema] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Cole apenas o token, sem o prefixo Bearer."
        };

        var exigencia = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(Esquema, documento)] = []
        };

        foreach (var caminho in documento.Paths)
        {
            var ehPublico =
                caminho.Key.Contains("/publico/", StringComparison.OrdinalIgnoreCase) ||
                caminho.Key.Contains("/auth/", StringComparison.OrdinalIgnoreCase);

            if (ehPublico || caminho.Value.Operations is null)
            {
                continue;
            }

            foreach (var operacao in caminho.Value.Operations)
            {
                operacao.Value.Security = [exigencia];
            }
        }

        return Task.CompletedTask;
    }
}
