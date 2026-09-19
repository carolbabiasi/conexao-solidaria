namespace GestorONG.Application.Excecoes;

public sealed class ConflitoException : Exception
{
    public ConflitoException(string mensagem) : base(mensagem)
    {
    }

    // A violacao de indice unico chega como DbUpdateException. Preservar a
    // causa mantem o SQLSTATE no log sem expor nada na resposta.
    public ConflitoException(string mensagem, Exception excecaoInterna)
        : base(mensagem, excecaoInterna)
    {
    }
}

public sealed class NaoEncontradoException(string mensagem) : Exception(mensagem);

public sealed class CredenciaisInvalidasException()
    : Exception("E-mail ou senha inválidos.");
