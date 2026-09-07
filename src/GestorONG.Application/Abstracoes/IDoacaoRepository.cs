using GestorONG.Domain.Entities;

namespace GestorONG.Application.Abstracoes;

public interface IDoacaoRepository
{
    Task<Doacao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Doacao doacao, CancellationToken cancellationToken = default);

    Task MarcarComoProcessadaAsync(Guid idDoacao, CancellationToken cancellationToken = default);
}
