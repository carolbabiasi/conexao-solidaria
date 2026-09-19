namespace GestorONG.Application.Excecoes;

public sealed class ConflitoException : Exception
{
    public ConflitoException(string mensagem) : base(mensagem)
    {
    }

    public ConflitoException(string mensagem, Exception excecaoInterna)
        : base(mensagem, excecaoInterna)
    {
    }
}

public sealed class NaoEncontradoException(string mensagem) : Exception(mensagem);

public sealed class CredenciaisInvalidasException()
    : Exception("E-mail ou senha inválidos.");

/// <summary>
/// Falha que nao adianta tentar de novo.
///
/// O retry existe para falha transitoria - banco reiniciando, rede oscilando.
/// Uma campanha que nao existe agora nao vai passar a existir em quinze
/// segundos: insistir so atrasa o descarte e segura a fila. Excecoes deste
/// tipo sao ignoradas pela politica de retry e vao direto para a fila de erro.
/// </summary>
public sealed class FalhaPermanenteException(string mensagem) : Exception(mensagem);
