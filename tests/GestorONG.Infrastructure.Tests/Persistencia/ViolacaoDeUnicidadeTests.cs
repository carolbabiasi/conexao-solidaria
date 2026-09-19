using GestorONG.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestorONG.Infrastructure.Tests.Persistencia;

public sealed class ViolacaoDeUnicidadeTests
{
    private static PostgresException ErroPostgres(string sqlState) =>
        new(messageText: "erro simulado", severity: "ERROR", invariantSeverity: "ERROR", sqlState);

    [Fact]
    public void Detecta_violacao_de_indice_unico()
    {
        var excecao = new DbUpdateException("falha ao salvar", ErroPostgres("23505"));

        Assert.True(ViolacaoDeUnicidade.Detectada(excecao));
    }

    [Theory]
    [InlineData("23503")] // foreign_key_violation
    [InlineData("23502")] // not_null_violation
    [InlineData("40001")] // serialization_failure
    public void Ignora_outros_erros_do_postgres(string sqlState)
    {
        var excecao = new DbUpdateException("falha ao salvar", ErroPostgres(sqlState));

        Assert.False(ViolacaoDeUnicidade.Detectada(excecao));
    }

    [Fact]
    public void Ignora_DbUpdateException_sem_causa_do_postgres()
    {
        var excecao = new DbUpdateException("falha ao salvar", new InvalidOperationException());

        Assert.False(ViolacaoDeUnicidade.Detectada(excecao));
    }

    [Fact]
    public void Ignora_excecao_que_nao_vem_do_SaveChanges()
    {
        Assert.False(ViolacaoDeUnicidade.Detectada(ErroPostgres("23505")));
    }

    [Fact]
    public void Ignora_nulo()
    {
        Assert.False(ViolacaoDeUnicidade.Detectada(null));
    }
}
