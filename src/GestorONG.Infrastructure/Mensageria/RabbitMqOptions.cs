using System.ComponentModel.DataAnnotations;

namespace GestorONG.Infrastructure.Mensageria;

public sealed class RabbitMqOptions
{
    public const string Secao = "RabbitMq";

    [Required]
    public string Host { get; init; } = "localhost";

    public ushort Porta { get; init; } = 5672;

    public string VirtualHost { get; init; } = "/";

    [Required]
    public string Usuario { get; init; } = string.Empty;

    [Required]
    public string Senha { get; init; } = string.Empty;
}
