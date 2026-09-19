using GestorONG.Application.Excecoes;
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
                {
                    // Falha permanente nao entra no retry. Uma campanha que nao
                    // existe agora nao vai passar a existir em quinze segundos:
                    // insistir so atrasa o descarte e segura a fila atras dela.
                    // Ignorada aqui, a mensagem vai direto para <fila>_error.
                    retry.Ignore<FalhaPermanenteException>();

                    retry.Exponential(
                        retryLimit: 3,
                        minInterval: TimeSpan.FromSeconds(1),
                        maxInterval: TimeSpan.FromSeconds(15),
                        intervalDelta: TimeSpan.FromSeconds(2));
                });

                barramento.ConfigureEndpoints(contexto);
            });
        });

        return servicos;
    }
}
