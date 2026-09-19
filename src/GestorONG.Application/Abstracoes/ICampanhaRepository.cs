using GestorONG.Domain.Entities;
using GestorONG.Domain.Enums;

namespace GestorONG.Application.Abstracoes;

public interface ICampanhaRepository
{
    Task<Campanha?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Campanha>> ListarAtivasAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pagina as campanhas do gestor, opcionalmente filtrando por status.
    ///
    /// Devolve o total junto com a pagina porque quem chama precisa dos dois
    /// para montar totalItems e totalPages - e contar em outra viagem ao banco
    /// daria um total que nao corresponde a pagina devolvida.
    /// </summary>
    Task<(IReadOnlyList<Campanha> Itens, int TotalItens)> ListarPaginadoAsync(
        int pagina,
        int tamanhoPagina,
        StatusCampanha? status = null,
        CancellationToken cancellationToken = default);

    Task AdicionarAsync(Campanha campanha, CancellationToken cancellationToken = default);

    Task<int> IncrementarArrecadadoAsync(
        Guid idCampanha,
        decimal valor,
        CancellationToken cancellationToken = default);
}
