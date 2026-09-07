using GestorONG.Application.Abstracoes;
using GestorONG.Domain.Entities;
using GestorONG.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GestorONG.Infrastructure.Persistencia.Repositorios;

internal sealed class CampanhaRepository(AppDbContext contexto) : ICampanhaRepository
{
    public Task<Campanha?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        contexto.Campanhas.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Campanha>> ListarAtivasAsync(CancellationToken cancellationToken = default) =>
        await contexto.Campanhas
            .AsNoTracking()
            .Where(c => c.Status == StatusCampanha.Ativa)
            .OrderByDescending(c => c.DataInicio)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Campanha>> ListarTodasAsync(CancellationToken cancellationToken = default) =>
        await contexto.Campanhas
            .AsNoTracking()
            .OrderByDescending(c => c.DataInicio)
            .ToListAsync(cancellationToken);

    public async Task AdicionarAsync(Campanha campanha, CancellationToken cancellationToken = default) =>
        await contexto.Campanhas.AddAsync(campanha, cancellationToken);

    public Task<int> IncrementarArrecadadoAsync(
        Guid idCampanha,
        decimal valor,
        CancellationToken cancellationToken = default) =>
        contexto.Campanhas
            .Where(c => c.Id == idCampanha)
            .ExecuteUpdateAsync(
                atualizacao => atualizacao.SetProperty(
                    c => c.ValorArrecadado,
                    c => c.ValorArrecadado + valor),
                cancellationToken);
}
