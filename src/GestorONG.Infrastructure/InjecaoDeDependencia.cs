using GestorONG.Application.Abstracoes;
using GestorONG.Infrastructure.Persistencia;
using GestorONG.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestorONG.Infrastructure;

public static class InjecaoDeDependencia
{
    public static IServiceCollection AdicionarInfraestrutura(
        this IServiceCollection servicos,
        IConfiguration configuracao)
    {
        var conexaoPostgres = configuracao.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' não configurada.");

        servicos.AddDbContext<AppDbContext>(opcoes =>
            opcoes.UseNpgsql(conexaoPostgres, npgsql => npgsql.EnableRetryOnFailure()));

        servicos.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        servicos.AddScoped<IUsuarioRepository, UsuarioRepository>();
        servicos.AddScoped<ICampanhaRepository, CampanhaRepository>();
        servicos.AddScoped<IDoacaoRepository, DoacaoRepository>();

        servicos.TryAddSingletonTimeProvider();

        return servicos;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection servicos)
    {
        if (servicos.All(s => s.ServiceType != typeof(TimeProvider)))
        {
            servicos.AddSingleton(TimeProvider.System);
        }
    }
}
