using GestorONG.Infrastructure.Mensageria;
using GestorONG.Infrastructure.Persistencia.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GestorONG.Infrastructure.Observabilidade;

public static class ConfiguracaoDeObservabilidade
{
    public static IServiceCollection AdicionarObservabilidade(
        this IServiceCollection servicos,
        IConfiguration configuracao,
        string nomeDoServico)
    {
        servicos.AddSingleton<MetricasDeNegocio>();

        servicos.AddOpenTelemetry()
            .ConfigureResource(recurso => recurso.AddService(nomeDoServico))
            // Tracing sem exportador: o objetivo aqui e existir um Activity
            // em curso, para o TraceId entrar no log. O MassTransit propaga
            // esse contexto nos headers da mensagem, entao o Worker continua
            // o mesmo trace que comecou na requisicao HTTP.
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddSource("MassTransit"))
            .WithMetrics(metricas => metricas
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(MetricasDeNegocio.NomeDoMeter)
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                .AddPrometheusExporter());

        var postgres = configuracao.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' não configurada.");

        var mongo = configuracao.GetSection(MongoOptions.Secao).Get<MongoOptions>()
            ?? throw new InvalidOperationException("Seção 'Mongo' não configurada.");

        var rabbit = configuracao.GetSection(RabbitMqOptions.Secao).Get<RabbitMqOptions>()
            ?? throw new InvalidOperationException("Seção 'RabbitMq' não configurada.");

        var uriRabbit = new Uri(
            $"amqp://{rabbit.Usuario}:{rabbit.Senha}@{rabbit.Host}:{rabbit.Porta}{rabbit.VirtualHost}");

        servicos.AddHealthChecks()
            .AddNpgSql(postgres, name: "postgres", tags: ["ready"])
            .AddMongoDb(_ => new MongoDB.Driver.MongoClient(mongo.ConnectionString),
                name: "mongodb",
                tags: ["ready"])
            .AddRabbitMQ(_ => new RabbitMQ.Client.ConnectionFactory { Uri = uriRabbit }.CreateConnectionAsync(),
                name: "rabbitmq",
                tags: ["ready"]);

        return servicos;
    }
}
