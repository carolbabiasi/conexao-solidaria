using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GestorONG.Application.Abstracoes;
using GestorONG.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GestorONG.Infrastructure.Seguranca;

internal sealed class JwtTokenService(IOptions<JwtOptions> opcoes, TimeProvider tempo) : IJwtTokenService
{
    private readonly JwtOptions _opcoes = opcoes.Value;

    public TokenGerado Gerar(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var agora = tempo.GetUtcNow();
        var expiraEm = agora.AddMinutes(_opcoes.ExpiracaoMinutos);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email.Valor),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(ClaimsGestorONG.Role, usuario.Role.ToString())
        };

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.ChaveSecreta));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opcoes.Emissor,
            audience: _opcoes.Audiencia,
            claims: claims,
            notBefore: agora.UtcDateTime,
            expires: expiraEm.UtcDateTime,
            signingCredentials: credenciais);

        return new TokenGerado(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiraEm,
            usuario.Role.ToString());
    }
}
