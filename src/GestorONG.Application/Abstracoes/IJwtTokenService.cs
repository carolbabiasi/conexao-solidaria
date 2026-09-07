using GestorONG.Domain.Entities;

namespace GestorONG.Application.Abstracoes;

public sealed record TokenGerado(string AccessToken, DateTimeOffset ExpiraEm, string Role);

public interface IJwtTokenService
{
    TokenGerado Gerar(Usuario usuario);
}
