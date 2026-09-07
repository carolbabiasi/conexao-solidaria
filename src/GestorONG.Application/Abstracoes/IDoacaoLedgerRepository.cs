namespace GestorONG.Application.Abstracoes;

public interface IDoacaoLedgerRepository
{
    Task<bool> TentarRegistrarAsync(
        Guid idDoacao,
        Guid idCampanha,
        Guid idDoador,
        decimal valor,
        DateTimeOffset processadoEm,
        CancellationToken cancellationToken = default);

    Task GarantirIndicesAsync(CancellationToken cancellationToken = default);
}
