namespace GestorONG.Domain.Enums;

/// <summary>
/// Estados de uma campanha.
/// Valores explícitos para o armazenamento não depender da ordem de declaração.
/// </summary>
public enum StatusCampanha
{
    Ativa = 1,
    Concluida = 2,
    Cancelada = 3
}
