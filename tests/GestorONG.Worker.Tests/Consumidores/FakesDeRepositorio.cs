using GestorONG.Application.Abstracoes;
using GestorONG.Domain.Entities;
using GestorONG.Domain.Enums;

namespace GestorONG.Worker.Tests.Consumidores;

/// <summary>
/// Ledger em memoria que imita o indice unico do Mongo: o primeiro registro de
/// um IdDoacao entra, os seguintes sao recusados. E o comportamento que a
/// idempotencia do consumer depende.
/// </summary>
internal sealed class LedgerFake : IDoacaoLedgerRepository
{
    private readonly HashSet<Guid> _registradas = [];

    public int TentativasDeRegistro { get; private set; }

    public Task GarantirIndicesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> TentarRegistrarAsync(
        Guid idDoacao,
        Guid idCampanha,
        Guid idDoador,
        decimal valor,
        DateTimeOffset processadoEm,
        CancellationToken cancellationToken = default)
    {
        TentativasDeRegistro++;
        return Task.FromResult(_registradas.Add(idDoacao));
    }
}

internal sealed class CampanhaRepositorioFake(Campanha? campanha = null) : ICampanhaRepository
{
    public int ChamadasDeIncremento { get; private set; }

    public decimal TotalIncrementado { get; private set; }

    /// <summary>Quando zero, o consumer trata como campanha inexistente.</summary>
    public int LinhasAfetadasPorIncremento { get; init; } = 1;

    public Task<Campanha?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(campanha);

    public Task<IReadOnlyList<Campanha>> ListarAtivasAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Campanha>>([]);

    public Task<(IReadOnlyList<Campanha> Itens, int TotalItens)> ListarPaginadoAsync(
        int pagina,
        int tamanhoPagina,
        StatusCampanha? status = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<(IReadOnlyList<Campanha>, int)>(([], 0));

    public Task AdicionarAsync(Campanha campanha, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<int> IncrementarArrecadadoAsync(
        Guid idCampanha,
        decimal valor,
        CancellationToken cancellationToken = default)
    {
        ChamadasDeIncremento++;
        TotalIncrementado += valor;
        return Task.FromResult(LinhasAfetadasPorIncremento);
    }
}

internal sealed class DoacaoRepositorioFake : IDoacaoRepository
{
    public int MarcadasComoProcessadas { get; private set; }

    public Task<Doacao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Doacao?>(null);

    public Task AdicionarAsync(Doacao doacao, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task MarcarComoProcessadaAsync(Guid idDoacao, CancellationToken cancellationToken = default)
    {
        MarcadasComoProcessadas++;
        return Task.CompletedTask;
    }
}
