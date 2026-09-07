using GestorONG.Domain.Entities;
using GestorONG.Domain.ValueObjects;

namespace GestorONG.Application.Abstracoes;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default);
}
