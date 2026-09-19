using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestorONG.Infrastructure.Persistencia;

/// <summary>
/// Reconhece a violacao de indice unico devolvida pelo PostgreSQL.
///
/// Checar duplicidade em codigo antes de inserir nao fecha a janela: dois
/// cadastros simultaneos passam os dois pela checagem e inserem os dois. O
/// indice unico e o unico lugar onde checagem e escrita sao atomicas, entao o
/// caminho correto e tentar escrever e traduzir a violacao - que e o risco R6
/// do backlog.
/// </summary>
public static class ViolacaoDeUnicidade
{
    /// <summary>Codigo SQLSTATE de unique_violation no padrao SQL.</summary>
    private const string UniqueViolation = "23505";

    public static bool Detectada(Exception? excecao) =>
        excecao is DbUpdateException atualizacao &&
        atualizacao.InnerException is PostgresException postgres &&
        postgres.SqlState == UniqueViolation;
}
