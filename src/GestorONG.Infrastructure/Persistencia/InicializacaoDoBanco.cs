using GestorONG.Application.Abstracoes;
using GestorONG.Domain.Entities;
using GestorONG.Domain.Enums;
using GestorONG.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestorONG.Infrastructure.Persistencia;

public static class InicializacaoDoBanco
{
    public static async Task MigrarESemearAsync(
        this IServiceProvider provedor,
        CancellationToken cancellationToken = default)
    {
        using var escopo = provedor.CreateScope();
        var servicos = escopo.ServiceProvider;

        var logger = servicos.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(InicializacaoDoBanco));

        var contexto = servicos.GetRequiredService<AppDbContext>();

        await contexto.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Migrations aplicadas.");

        await SemearGestorAsync(servicos, logger, cancellationToken);
    }

    private static async Task SemearGestorAsync(
        IServiceProvider servicos,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var configuracao = servicos.GetRequiredService<IConfiguration>();
        var secao = configuracao.GetSection("Seed");

        var emailBruto = secao["GestorEmail"];
        var senha = secao["GestorSenha"];

        if (string.IsNullOrWhiteSpace(emailBruto) || string.IsNullOrWhiteSpace(senha))
        {
            logger.LogWarning("Seed do gestor não configurado. Nenhum GestorONG foi criado.");
            return;
        }

        var usuarios = servicos.GetRequiredService<IUsuarioRepository>();
        var email = Email.Criar(emailBruto);

        if (await usuarios.ObterPorEmailAsync(email, cancellationToken) is not null)
        {
            logger.LogInformation("Gestor {Email} já existe. Seed ignorado.", email.Valor);
            return;
        }

        var hasher = servicos.GetRequiredService<IPasswordHasher>();
        var tempo = servicos.GetRequiredService<TimeProvider>();
        var unitOfWork = servicos.GetRequiredService<IUnitOfWork>();

        var gestor = Usuario.Criar(
            secao["GestorNome"] ?? "Gestor Esperanca Solidaria",
            email,
            Cpf.Criar(secao["GestorCpf"] ?? "529.982.247-25"),
            hasher.GerarHash(senha),
            Role.GestorONG,
            tempo.GetUtcNow());

        await usuarios.AdicionarAsync(gestor, cancellationToken);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogInformation("Gestor {Email} criado pelo seed.", email.Valor);
    }
}
