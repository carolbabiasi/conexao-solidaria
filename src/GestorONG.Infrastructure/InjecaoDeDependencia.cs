using GestorONG.Application.Abstracoes;
using GestorONG.Infrastructure.Persistencia;
using GestorONG.Infrastructure.Persistencia.Mongo;
using GestorONG.Infrastructure.Persistencia.Repositorios;
using GestorONG.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

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

        servicos.AddScoped<IPasswordHasher, PasswordHasher>();
        servicos.AddScoped<IJwtTokenService, JwtTokenService>();

        servicos.AddOptions<MongoOptions>()
            .Bind(configuracao.GetSection(MongoOptions.Secao))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        servicos.AddSingleton<IMongoClient>(sp =>
            new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));

        servicos.AddScoped<IDoacaoLedgerRepository, DoacaoLedgerRepository>();

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
