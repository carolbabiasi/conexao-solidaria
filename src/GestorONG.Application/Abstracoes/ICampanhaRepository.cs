using GestorONG.Domain.Entities;

namespace GestorONG.Application.Abstracoes;

public interface ICampanhaRepository
{
    Task<Campanha?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Campanha>> ListarAtivasAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Campanha>> ListarTodasAsync(CancellationToken cancellationToken = default);

    Task AdicionarAsync(Campanha campanha, CancellationToken cancellationToken = default);

    Task<int> IncrementarArrecadadoAsync(
        Guid idCampanha,
        decimal valor,
        CancellationToken cancellationToken = default);
}
