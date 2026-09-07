namespace GestorONG.Domain.Enums;

/// <summary>
/// Estados de uma doação.
/// <para>
/// A API grava <see cref="Pendente"/> e publica o evento; quem promove para
/// <see cref="Processada"/> é o Worker, depois de somar o valor à campanha.
/// </para>
/// </summary>
public enum StatusDoacao
{
    Pendente = 1,
    Processada = 2
}
