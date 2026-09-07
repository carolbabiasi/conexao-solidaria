using GestorONG.Application.Abstracoes;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GestorONG.Infrastructure.Persistencia.Mongo;

internal sealed class DoacaoLedgerRepository : IDoacaoLedgerRepository
{
    private const string NomeDaColecao = "doacoes_processadas";
    private const string NomeDoIndice = "ux_doacoes_processadas_id_doacao";
    private const int CodigoChaveDuplicada = 11000;

    private readonly IMongoCollection<DoacaoProcessada> _colecao;

    public DoacaoLedgerRepository(IMongoClient cliente, IOptions<MongoOptions> opcoes)
    {
        var banco = cliente.GetDatabase(opcoes.Value.Database);
        _colecao = banco.GetCollection<DoacaoProcessada>(NomeDaColecao);
    }

    public async Task GarantirIndicesAsync(CancellationToken cancellationToken = default)
    {
        var definicao = new CreateIndexModel<DoacaoProcessada>(
            Builders<DoacaoProcessada>.IndexKeys.Ascending(d => d.IdDoacao),
            new CreateIndexOptions { Unique = true, Name = NomeDoIndice });

        await _colecao.Indexes.CreateOneAsync(definicao, cancellationToken: cancellationToken);
    }

    public async Task<bool> TentarRegistrarAsync(
        Guid idDoacao,
        Guid idCampanha,
        Guid idDoador,
        decimal valor,
        DateTimeOffset processadoEm,
        CancellationToken cancellationToken = default)
    {
        var registro = new DoacaoProcessada
        {
            IdDoacao = idDoacao,
            IdCampanha = idCampanha,
            IdDoador = idDoador,
            Valor = valor,
            ProcessadoEm = processadoEm.UtcDateTime
        };

        try
        {
            await _colecao.InsertOneAsync(registro, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException excecao)
            when (excecao.WriteError?.Code == CodigoChaveDuplicada)
        {
            return false;
        }
    }
}
