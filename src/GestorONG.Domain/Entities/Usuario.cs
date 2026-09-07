using GestorONG.Domain.Enums;
using GestorONG.Domain.Exceptions;
using GestorONG.Domain.ValueObjects;

namespace GestorONG.Domain.Entities;

public sealed class Usuario
{
    private const int TamanhoMaximoNome = 200;

    public Guid Id { get; private set; }
    public string NomeCompleto { get; private set; }
    public Email Email { get; private set; }
    public Cpf Cpf { get; private set; }
    public string SenhaHash { get; private set; }
    public Role Role { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private Usuario()
    {
        NomeCompleto = null!;
        Email = null!;
        Cpf = null!;
        SenhaHash = null!;
    }

    private Usuario(
        Guid id,
        string nomeCompleto,
        Email email,
        Cpf cpf,
        string senhaHash,
        Role role,
        DateTimeOffset criadoEm)
    {
        Id = id;
        NomeCompleto = nomeCompleto;
        Email = email;
        Cpf = cpf;
        SenhaHash = senhaHash;
        Role = role;
        CriadoEm = criadoEm;
    }

    public static Usuario Criar(
        string? nomeCompleto,
        Email email,
        Cpf cpf,
        string? senhaHash,
        Role role,
        DateTimeOffset criadoEm)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(cpf);

        var violacoes = new List<Violacao>();

        if (string.IsNullOrWhiteSpace(nomeCompleto))
        {
            violacoes.Add(new Violacao(nameof(NomeCompleto), "Nome completo é obrigatório."));
        }
        else if (nomeCompleto.Trim().Length > TamanhoMaximoNome)
        {
            violacoes.Add(new Violacao(
                nameof(NomeCompleto),
                $"Nome completo deve ter no máximo {TamanhoMaximoNome} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(senhaHash))
        {
            violacoes.Add(new Violacao(nameof(SenhaHash), "Hash da senha é obrigatório."));
        }

        if (!Enum.IsDefined(role))
        {
            violacoes.Add(new Violacao(nameof(Role), "Perfil inválido."));
        }

        if (violacoes.Count > 0)
        {
            throw new DomainValidationException(violacoes);
        }

        return new Usuario(
            Guid.CreateVersion7(),
            nomeCompleto!.Trim(),
            email,
            cpf,
            senhaHash!,
            role,
            criadoEm);
    }

    public override string ToString() => $"Usuario({Id})";
}
