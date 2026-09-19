using GestorONG.Domain.Enums;

namespace GestorONG.Domain.Exceptions;

/// <summary>
/// Transicao de status que o ciclo de vida da campanha nao permite.
///
/// E conflito, e nao erro de validacao: a requisicao esta bem formada e o
/// status pedido existe - o que impede e o estado atual do recurso. Por isso
/// vira 409, e nao 400.
/// </summary>
public sealed class TransicaoDeStatusInvalidaException(StatusCampanha atual, StatusCampanha pretendido)
    : Exception($"Campanha {atual} não pode transitar para {pretendido}.")
{
    public StatusCampanha Atual { get; } = atual;

    public StatusCampanha Pretendido { get; } = pretendido;
}
