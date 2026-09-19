using GestorONG.Application.Abstracoes;
using GestorONG.Application.Excecoes;
using GestorONG.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace GestorONG.Infrastructure.Persistencia;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Campanha> Campanhas => Set<Campanha>();
    public DbSet<Doacao> Doacoes => Set<Doacao>();

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await SaveChangesAsync(cancellationToken);
        }
        catch (Exception excecao) when (ViolacaoDeUnicidade.Detectada(excecao))
        {
            // Traduzir aqui, e nao no controller, cobre todo caminho de escrita
            // de uma vez e fecha a corrida que a checagem previa deixa aberta.
            // A mensagem e deliberadamente generica: dizer qual campo colidiu
            // permitiria descobrir quais e-mails e CPFs ja estao cadastrados.
            throw new ConflitoException(
                "Já existe um registro com os dados informados.", excecao);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        base.OnModelCreating(modelBuilder);
    }
}
