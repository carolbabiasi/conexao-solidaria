using GestorONG.API.Contratos;
using GestorONG.Application.Abstracoes;
using GestorONG.Application.Excecoes;
using GestorONG.Domain.Entities;
using GestorONG.Domain.Enums;
using GestorONG.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorONG.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Tags("Auth")]
public sealed class AuthController(
    IUsuarioRepository usuarios,
    IPasswordHasher hasher,
    IJwtTokenService tokens,
    IUnitOfWork unitOfWork,
    TimeProvider tempo) : ControllerBase
{
    [HttpPost("registrar")]
    [AllowAnonymous]
    [ProducesResponseType<UsuarioResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioResponse>> Registrar(
        RegistrarDoadorRequest requisicao,
        CancellationToken cancellationToken)
    {
        var email = Email.Criar(requisicao.Email);
        var cpf = Cpf.Criar(requisicao.Cpf);

        if (await usuarios.ObterPorEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflitoException("Já existe um cadastro com os dados informados.");
        }

        var usuario = Usuario.Criar(
            requisicao.NomeCompleto,
            email,
            cpf,
            hasher.GerarHash(requisicao.Senha),
            Role.Doador,
            tempo.GetUtcNow());

        await usuarios.AdicionarAsync(usuario, cancellationToken);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        var resposta = new UsuarioResponse(
            usuario.Id,
            usuario.NomeCompleto,
            usuario.Email.Valor,
            usuario.Role.ToString());

        return CreatedAtAction(nameof(Registrar), new { id = usuario.Id }, resposta);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest requisicao,
        CancellationToken cancellationToken)
    {
        if (!Email.TryParse(requisicao.Email, out var email))
        {
            throw new CredenciaisInvalidasException();
        }

        var usuario = await usuarios.ObterPorEmailAsync(email, cancellationToken);

        if (usuario is null || !hasher.Verificar(requisicao.Senha, usuario.SenhaHash))
        {
            throw new CredenciaisInvalidasException();
        }

        var token = tokens.Gerar(usuario);

        return Ok(new LoginResponse(token.AccessToken, token.ExpiraEm, token.Role));
    }
}
