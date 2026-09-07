using System.ComponentModel.DataAnnotations;

namespace GestorONG.Infrastructure.Persistencia.Mongo;

public sealed class MongoOptions
{
    public const string Secao = "Mongo";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Required]
    public string Database { get; init; } = "conexaosolidaria";
}
