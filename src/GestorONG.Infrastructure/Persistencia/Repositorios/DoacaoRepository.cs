using GestorONG.Application.Abstracoes;
using GestorONG.Domain.Entities;
using GestorONG.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GestorONG.Infrastructure.Persistencia.Repositorios;

internal sealed class DoacaoRepository(AppDbContext contexto) : IDoacaoRepository
{
    public Task<Doacao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        contexto.Doacoes.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task AdicionarAsync(Doacao doacao, CancellationToken cancellationToken = default) =>
        await contexto.Doacoes.AddAsync(doacao, cancellationToken);

    public Task MarcarComoProcessadaAsync(Guid idDoacao, CancellationToken cancellationToken = default) =>
        contexto.Doacoes
            .Where(d => d.Id == idDoacao)
            .ExecuteUpdateAsync(
                atualizacao => atualizacao.SetProperty(d => d.Status, StatusDoacao.Processada),
                cancellationToken);
}
