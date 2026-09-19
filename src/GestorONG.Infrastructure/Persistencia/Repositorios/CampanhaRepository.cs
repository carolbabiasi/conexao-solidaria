using GestorONG.Application.Abstracoes;
using GestorONG.Domain.Entities;
using GestorONG.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GestorONG.Infrastructure.Persistencia.Repositorios;

internal sealed class CampanhaRepository(AppDbContext contexto) : ICampanhaRepository
{
    public Task<Campanha?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        contexto.Campanhas.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Campanha>> ListarAtivasAsync(CancellationToken cancellationToken = default) =>
        await contexto.Campanhas
            .AsNoTracking()
            .Where(c => c.Status == StatusCampanha.Ativa)
            .OrderByDescending(c => c.DataInicio)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Campanha> Itens, int TotalItens)> ListarPaginadoAsync(
        int pagina,
        int tamanhoPagina,
        StatusCampanha? status = null,
        CancellationToken cancellationToken = default)
    {
        var consulta = contexto.Campanhas.AsNoTracking();

        if (status is not null)
        {
            consulta = consulta.Where(c => c.Status == status);
        }

        // Conta antes de paginar, sobre a mesma consulta filtrada: o total
        // precisa refletir o filtro, e nao a tabela inteira.
        var total = await consulta.CountAsync(cancellationToken);

        // Ordem estavel e obrigatoria com paginacao. Sem ORDER BY deterministico
        // o banco pode devolver a mesma linha em duas paginas diferentes, e o
        // DataInicio sozinho empata quando duas campanhas comecam no mesmo dia.
        var itens = await consulta
            .OrderByDescending(c => c.DataInicio)
            .ThenBy(c => c.Id)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(cancellationToken);

        return (itens, total);
    }

    public async Task AdicionarAsync(Campanha campanha, CancellationToken cancellationToken = default) =>
        await contexto.Campanhas.AddAsync(campanha, cancellationToken);

    public Task<int> IncrementarArrecadadoAsync(
        Guid idCampanha,
        decimal valor,
        CancellationToken cancellationToken = default) =>
        contexto.Campanhas
            .Where(c => c.Id == idCampanha)
            .ExecuteUpdateAsync(
                atualizacao => atualizacao.SetProperty(
                    c => c.ValorArrecadado,
                    c => c.ValorArrecadado + valor),
                cancellationToken);
}
