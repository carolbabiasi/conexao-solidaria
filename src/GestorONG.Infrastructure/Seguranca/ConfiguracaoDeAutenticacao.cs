using System.Text;
using GestorONG.Domain.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GestorONG.Infrastructure.Seguranca;

public static class ConfiguracaoDeAutenticacao
{
    public static IServiceCollection AdicionarAutenticacao(
        this IServiceCollection servicos,
        IConfiguration configuracao)
    {
        servicos.AddOptions<JwtOptions>()
            .Bind(configuracao.GetSection(JwtOptions.Secao))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var opcoes = configuracao.GetSection(JwtOptions.Secao).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Seção 'Jwt' não configurada.");

        servicos
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = opcoes.Emissor,
                    ValidAudience = opcoes.Audiencia,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(opcoes.ChaveSecreta)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = ClaimsGestorONG.Role
                };
            });

        servicos.AddAuthorizationBuilder()
            .AddPolicy(
                ClaimsGestorONG.Politica_Gestor,
                politica => politica.RequireRole(nameof(Role.GestorONG)))
            .AddPolicy(
                ClaimsGestorONG.Politica_Doador,
                politica => politica.RequireRole(nameof(Role.Doador)));

        return servicos;
    }
}
