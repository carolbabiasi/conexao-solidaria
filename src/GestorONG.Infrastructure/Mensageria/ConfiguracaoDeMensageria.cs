using GestorONG.Infrastructure.Persistencia;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestorONG.Infrastructure.Mensageria;

public static class ConfiguracaoDeMensageria
{
    public static IServiceCollection AdicionarMensageria(
        this IServiceCollection servicos,
        IConfiguration configuracao,
        Action<IBusRegistrationConfigurator>? consumidores = null,
        bool comOutbox = false)
    {
        servicos.AddOptions<RabbitMqOptions>()
            .Bind(configuracao.GetSection(RabbitMqOptions.Secao))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var opcoes = configuracao.GetSection(RabbitMqOptions.Secao).Get<RabbitMqOptions>()
            ?? throw new InvalidOperationException("Seção 'RabbitMq' não configurada.");

        servicos.AddMassTransit(configurador =>
        {
            if (comOutbox)
            {
                configurador.AddEntityFrameworkOutbox<AppDbContext>(outbox =>
                {
                    outbox.UsePostgres();
                    outbox.UseBusOutbox();
                });
            }

            consumidores?.Invoke(configurador);

            configurador.SetKebabCaseEndpointNameFormatter();

            configurador.UsingRabbitMq((contexto, barramento) =>
            {
                barramento.Host(opcoes.Host, opcoes.Porta, opcoes.VirtualHost, anfitriao =>
                {
                    anfitriao.Username(opcoes.Usuario);
                    anfitriao.Password(opcoes.Senha);
                });

                barramento.UseMessageRetry(retry =>
                    retry.Exponential(
                        retryLimit: 3,
                        minInterval: TimeSpan.FromSeconds(1),
                        maxInterval: TimeSpan.FromSeconds(15),
                        intervalDelta: TimeSpan.FromSeconds(2)));

                barramento.ConfigureEndpoints(contexto);
            });
        });

        return servicos;
    }
}
