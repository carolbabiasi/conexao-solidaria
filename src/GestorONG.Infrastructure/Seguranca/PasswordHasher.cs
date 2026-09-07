using GestorONG.Application.Abstracoes;

namespace GestorONG.Infrastructure.Seguranca;

internal sealed class PasswordHasher : IPasswordHasher
{
    private const int FatorDeTrabalho = 12;

    public string GerarHash(string senha) =>
        BCrypt.Net.BCrypt.HashPassword(senha, FatorDeTrabalho);

    public bool Verificar(string senha, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(senha, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
