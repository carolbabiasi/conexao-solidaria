using GestorONG.Application.Abstracoes;
using GestorONG.Domain.Entities;
using GestorONG.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace GestorONG.Infrastructure.Persistencia.Repositorios;

internal sealed class UsuarioRepository(AppDbContext contexto) : IUsuarioRepository
{
    public Task<Usuario?> ObterPorEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        contexto.Usuarios.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        contexto.Usuarios.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default) =>
        await contexto.Usuarios.AddAsync(usuario, cancellationToken);
}
