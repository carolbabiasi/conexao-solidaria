using System.Text.Json.Nodes;
using GestorONG.API.Contratos;
using GestorONG.API.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace GestorONG.API.Configuracao;

/// <summary>
/// Preenche o campo <c>example</c> dos schemas do OpenAPI. Sem isso o Swagger UI
/// monta o corpo de exemplo a partir dos tipos, e entrega "string" em todo campo
/// de texto e 0 em todo decimal - um corpo que o professor precisa reescrever
/// inteiro antes de conseguir chamar o endpoint.
///
/// Os valores sao os mesmos do fluxo de teste do README de proposito: quem segue
/// o passo a passo pelo curl ou pelo Swagger ve exatamente os mesmos dados.
/// </summary>
internal sealed class TransformadorDeExemplos : IOpenApiSchemaTransformer
{
    private const string IdCampanhaExemplo = "01a09d03-bd42-7fb1-abe4-17987d98a8b8";

    // Um JsonNode so pode ter um pai, entao cada schema precisa da sua propria
    // instancia. Dai o dicionario guardar fabricas, e nao os nos prontos.
    private static readonly Dictionary<Type, Func<JsonNode>> Exemplos = new()
    {
        [typeof(RegistrarDoadorRequest)] = () => new JsonObject
        {
            ["nomeCompleto"] = "Maria Doadora",
            ["email"] = "maria@exemplo.com",
            ["cpf"] = "123.456.789-09",
            ["senha"] = "senha12345"
        },

        [typeof(UsuarioResponse)] = () => new JsonObject
        {
            ["id"] = "01a09d04-9d8c-7356-ba32-5320bef05ee6",
            ["nomeCompleto"] = "Maria Doadora",
            ["email"] = "maria@exemplo.com",
            ["role"] = "Doador"
        },

        [typeof(LoginRequest)] = () => new JsonObject
        {
            ["email"] = "gestor@esperancasolidaria.org",
            ["senha"] = "devlocal123"
        },

        [typeof(LoginResponse)] = () => new JsonObject
        {
            ["accessToken"] = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMWEwOWQwMy1iZDQyIn0.assinatura",
            ["expiraEm"] = "2026-09-19T17:30:00+00:00",
            ["role"] = "GestorONG"
        },

        [typeof(CriarCampanhaRequest)] = () => new JsonObject
        {
            ["titulo"] = "Cestas basicas de inverno",
            ["descricao"] = "Arrecadacao para 200 familias.",
            ["dataInicio"] = "2026-01-01T00:00:00+00:00",
            ["dataFim"] = "2026-12-31T23:59:59+00:00",
            ["metaFinanceira"] = 10000.00m,
            // Numerico, e nao "Ativa": nao ha JsonStringEnumConverter configurado,
            // entao a string quebra a desserializacao com 400. A resposta devolve
            // o nome do status, o que torna o contrato assimetrico - copiar uma
            // resposta e reenviar nao funciona. Documentado no EndpointDescription.
            ["status"] = 1
        },

        [typeof(CampanhaResponse)] = () => new JsonObject
        {
            ["id"] = IdCampanhaExemplo,
            ["titulo"] = "Cestas basicas de inverno",
            ["descricao"] = "Arrecadacao para 200 familias.",
            ["dataInicio"] = "2026-01-01T00:00:00+00:00",
            ["dataFim"] = "2026-12-31T23:59:59+00:00",
            ["metaFinanceira"] = 10000.00m,
            ["valorArrecadado"] = 125.50m,
            ["status"] = "Ativa"
        },

        [typeof(AtualizarCampanhaRequest)] = () => new JsonObject
        {
            ["titulo"] = "Cestas basicas de inverno - prorrogada",
            ["descricao"] = "Arrecadacao para 200 familias. Prazo estendido.",
            ["dataInicio"] = "2026-01-01T00:00:00+00:00",
            ["dataFim"] = "2027-03-31T23:59:59+00:00",
            // Depois da primeira doacao, meta e dataInicio precisam vir
            // iguais aos atuais: mudar qualquer um dos dois resulta em 400.
            ["metaFinanceira"] = 10000.00m
        },

        [typeof(AlterarStatusRequest)] = () => new JsonObject
        {
            // 2 Concluida, 3 Cancelada. So a partir de Ativa.
            ["status"] = 3
        },

        [typeof(CriarDoacaoRequest)] = () => new JsonObject
        {
            ["idCampanha"] = IdCampanhaExemplo,
            ["valorDoacao"] = 125.50m
        },

        [typeof(DoacaoAceitaResponse)] = () => new JsonObject
        {
            ["idDoacao"] = "01a09d04-a017-7cf5-aaae-88789f275d72",
            ["status"] = "Pendente",
            ["mensagem"] = "Doação recebida. O valor da campanha será atualizado em instantes."
        },

        [typeof(CampanhaPublicaResponse)] = () => new JsonObject
        {
            ["id"] = IdCampanhaExemplo,
            ["titulo"] = "Cestas basicas de inverno",
            ["metaFinanceira"] = 10000.00m,
            ["valorArrecadado"] = 125.50m,
            ["percentualAtingido"] = 1.26m
        }
    };

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext contexto,
        CancellationToken cancellationToken)
    {
        if (Exemplos.TryGetValue(contexto.JsonTypeInfo.Type, out var criarExemplo))
        {
            schema.Example = criarExemplo();
        }

        return Task.CompletedTask;
    }
}
