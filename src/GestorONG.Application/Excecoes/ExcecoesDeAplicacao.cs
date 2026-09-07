namespace GestorONG.Application.Excecoes;

public sealed class ConflitoException(string mensagem) : Exception(mensagem);

public sealed class NaoEncontradoException(string mensagem) : Exception(mensagem);

public sealed class CredenciaisInvalidasException()
    : Exception("E-mail ou senha inválidos.");
