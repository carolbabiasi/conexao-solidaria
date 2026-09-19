using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace GestorONG.Infrastructure.Observabilidade;

public static class ConfiguracaoDeLogging
{
    /// <summary>
    /// Log estruturado em JSON no console, que é o que o Kubernetes coleta.
    ///
    /// Cada linha carrega TraceId e SpanId quando existe um Activity em curso.
    /// Como o MassTransit propaga o contexto de trace nos headers da mensagem,
    /// o TraceId da requisição HTTP que aceitou a doação é o mesmo que aparece
    /// no log do Worker que a somou — que é o que permite seguir uma doação de
    /// ponta a ponta com um único grep.
    /// </summary>
    public static IHostApplicationBuilder UsarLoggingEstruturado(
        this IHostApplicationBuilder builder,
        string nomeDoServico)
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()

            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("MassTransit", LogEventLevel.Warning)
            .MinimumLevel.Override("Npgsql", LogEventLevel.Warning)

            .Enrich.FromLogContext()
            .Enrich.With<EnriquecedorDeTrace>()
            .Enrich.WithProperty("servico", nomeDoServico)
            .Enrich.WithProperty("ambiente", builder.Environment.EnvironmentName)

            .Destructure.With<RedatorDeDadoPessoal>()

            .WriteTo.Console(new CompactJsonFormatter())
            .CreateLogger();

        Log.Logger = logger;

        builder.Logging.ClearProviders();

        builder.Services.AddSerilog(logger, dispose: true);

        return builder;
    }
}

/// <summary>
/// Copia TraceId e SpanId do Activity em curso para o evento de log.
/// </summary>
internal sealed class EnriquecedorDeTrace : ILogEventEnricher
{
    public void Enrich(LogEvent evento, ILogEventPropertyFactory fabrica)
    {
        var atividade = Activity.Current;

        if (atividade is null)
        {
            return;
        }

        evento.AddPropertyIfAbsent(
            fabrica.CreateProperty("traceId", atividade.TraceId.ToString()));

        evento.AddPropertyIfAbsent(
            fabrica.CreateProperty("spanId", atividade.SpanId.ToString()));
    }
}

/// <summary>
/// Impede que CPF e senha cheguem ao log, venham de onde vierem.
///
/// Não substitui o cuidado de não logar corpo de requisição de autenticação —
/// é a segunda linha, para o dia em que alguém logar um objeto inteiro sem
/// reparar no que tem dentro.
/// </summary>
internal sealed class RedatorDeDadoPessoal : IDestructuringPolicy
{
    private static readonly string[] NomesSensiveis =
        ["senha", "password", "senhahash", "cpf", "accesstoken", "token"];

    public bool TryDestructure(
        object valor,
        ILogEventPropertyValueFactory fabrica,
        [NotNullWhen(true)] out LogEventPropertyValue? resultado)
    {
        resultado = null;

        var tipo = valor.GetType();

        if (tipo.IsPrimitive || valor is string || tipo.Namespace?.StartsWith("System") == true)
        {
            return false;
        }

        var propriedades = tipo.GetProperties()
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        if (propriedades.Length == 0)
        {
            return false;
        }

        var campos = propriedades.Select(propriedade =>
        {
            var sensivel = NomesSensiveis.Contains(
                propriedade.Name.ToLowerInvariant());

            object? conteudo;

            if (sensivel)
            {
                conteudo = "[redigido]";
            }
            else
            {
                try
                {
                    conteudo = propriedade.GetValue(valor);
                }
                catch
                {
                    conteudo = "[indisponivel]";
                }
            }

            return new LogEventProperty(propriedade.Name, fabrica.CreatePropertyValue(conteudo, true));
        });

        resultado = new StructureValue(campos, tipo.Name);
        return true;
    }
}
